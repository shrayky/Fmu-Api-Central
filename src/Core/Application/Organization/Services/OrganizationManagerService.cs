using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Dto.Responces;
using Domain.Entitys.Interfaces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Dto;
using Domain.Entitys.Organization.Interfaces;
using Domain.Etrn;
using Domain.GisMt.Models;
using Domain.Vetis;
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Dto;
using Domain.TrueApiIntegration.Interfaces;

namespace Application.Organization.Services;

[AutoRegisterService]
public class OrganizationManagerService : IOrganizationManagerService
{
    private readonly IOrganizationRepository _repository;
    private readonly ITrueApiAuthService _trueApiAuthService;
    private readonly IApplicationState _applicationState;
    private readonly IInstanceGroupRepository _instanceGroupRepository;
    private readonly IEtrnSyncStateRepository _etrnSync;

    public OrganizationManagerService(
        IOrganizationRepository repository,
        ITrueApiAuthService trueApiAuthService,
        IApplicationState applicationState,
        IInstanceGroupRepository instanceGroupRepository,
        IEtrnSyncStateRepository etrnSync)
    {
        _repository = repository;
        _trueApiAuthService = trueApiAuthService;
        _applicationState = applicationState;
        _instanceGroupRepository = instanceGroupRepository;
        _etrnSync = etrnSync;
    }

    public async Task<Result> Create(OrganizationView data)
    {
        var prepared = Prepare(data);
        if (prepared.IsFailure)
            return prepared;

        var unique = await EnsureUniqueInn(prepared.Value.Inn, prepared.Value.Id);
        if (unique.IsFailure)
            return unique;

        var exist = await _repository.GetById(prepared.Value.Id);
        if (exist.IsSuccess)
        {
            Apply(exist.Value, prepared.Value);
            return await _repository.Update(exist.Value);
        }

        var entity = new OrganizationEntity { Id = prepared.Value.Id };
        Apply(entity, prepared.Value);
        return await _repository.Create(entity);
    }

    public async Task<Result> Update(OrganizationView data)
    {
        var prepared = Prepare(data);
        if (prepared.IsFailure)
            return prepared;

        var exist = await _repository.GetById(prepared.Value.Id);
        if (exist.IsFailure)
            return Result.Failure(exist.Error);

        var unique = await EnsureUniqueInn(prepared.Value.Inn, prepared.Value.Id);
        if (unique.IsFailure)
            return unique;

        var previous = exist.Value.EtrnConnections ?? [];
        Apply(exist.Value, prepared.Value);
        var saved = await _repository.Update(exist.Value);
        if (saved.IsFailure)
            return saved;

        await ResetCursors(previous, exist.Value.EtrnConnections);
        return saved;
    }

    public async Task<Result> Delete(string id)
    {
        var exist = await _repository.GetById(id);
        if (exist.IsFailure)
            return Result.Failure(exist.Error);

        var clearLinks = await _instanceGroupRepository.ClearOrganizationLink(id);
        if (clearLinks.IsFailure)
            return Result.Failure($"Ошибка очистки ссылок на организацию {id}: {clearLinks.Error}");

        return await _repository.Delete(id);
    }

    public async Task<PaginatedResponse<OrganizationView>> List(int pageNumber, int pageSize)
    {
        var entityList = await _repository.List(pageNumber, pageSize);

        return new PaginatedResponse<OrganizationView>
        {
            Description = entityList.Description,
            ListEnabled = entityList.ListEnabled,
            TotalCount = entityList.TotalCount,
            PageSize = entityList.PageSize,
            CurrentPage = entityList.CurrentPage,
            Content = entityList.Content.Select(ToView)
        };
    }

    public async Task<Result<TrueApiTokenView>> Token(string inn)
    {
        var normalizedInn = NormalizeInn(inn);
        if (string.IsNullOrEmpty(normalizedInn))
            return Result.Failure<TrueApiTokenView>("Укажите ИНН организации");

        var organization = await _repository.GetByInn(normalizedInn);
        if (organization.IsFailure)
            return Result.Failure<TrueApiTokenView>(organization.Error);

        if (!organization.Value.TrueApiIntegrationSettings.Enable)
            return Result.Failure<TrueApiTokenView>("True API не включён для организации");

        var cached = _applicationState.TrueApiToken(normalizedInn);
        if (!string.IsNullOrEmpty(cached.Token))
            return Result.Success(ToTokenView(cached));

        var settings = organization.Value.TrueApiIntegrationSettings;
        var generated = await _trueApiAuthService.GenerateToken(
            normalizedInn,
            settings.Password,
            settings.DigitalSignature);

        if (generated.IsFailure)
            return Result.Failure<TrueApiTokenView>(generated.Error);

        var session = generated.Value;
        _applicationState.UpdateTrueApiToken(normalizedInn, session.Token, session.LiveUntil);

        return Result.Success(ToTokenView(new TrueApiToken
        {
            Inn = normalizedInn,
            Token = session.Token,
            LiveUntil = session.LiveUntil
        }));
    }

    private async Task<Result> EnsureUniqueInn(string inn, string id)
    {
        var existing = await _repository.GetByInn(inn);
        if (existing.IsSuccess)
        {
            if (existing.Value.Id != id)
                return Result.Failure($"Организация с ИНН {inn} уже существует");

            return Result.Success();
        }

        if (!_applicationState.DbState())
            return Result.Failure(existing.Error);

        return Result.Success();
    }

    private static Result<OrganizationView> Prepare(OrganizationView data)
    {
        var name = (data.Name ?? string.Empty).Trim();
        var inn = NormalizeInn(data.Inn);

        if (string.IsNullOrEmpty(name))
            return Result.Failure<OrganizationView>("Укажите наименование организации");

        if (string.IsNullOrEmpty(inn))
            return Result.Failure<OrganizationView>("Укажите ИНН организации");

        if (data.VetisSettings != null)
        {
            var delay = data.VetisSettings.CheckDelay();
            if (delay.IsFailure)
                return Result.Failure<OrganizationView>(delay.Error);
        }

        data.Name = name;
        data.Inn = inn;
        data.TrueApiIntegrationSettings ??= new TrueApiIntegrationSettings();
        return Result.Success(data);
    }

    private static string NormalizeInn(string? inn) => (inn ?? string.Empty).Trim();

    /// <summary>
    /// Пустое поле и маска не стирают ключ: карточка не отличает «не меняли» от очистки, а выпустить ключ заново можно только в ОФД.
    /// </summary>
    private static string TrimAddress(string? value) => (value ?? "").Trim().TrimEnd('/');

    /// <summary>
    /// Пустое поле и маска «***» оставляют уже сохранённый секрет: пароль на карточке не присылает старое значение.
    /// </summary>
    private static string RestoreSecret(string? incoming, string stored)
    {
        var value = (incoming ?? "").Trim();
        if (value.Length == 0 || SecretMask.IsPlaceholder(value))
            return stored;
        return value;
    }

    /// <summary>
    /// Пустая дата раньше означала «30 дней», теперь это 1 сентября 2026. Такая запись сдвигает опрос.
    /// </summary>
    private async Task ResetCursors(IReadOnlyList<EtrnConnection> previous, IReadOnlyList<EtrnConnection>? current)
    {
        foreach (var connection in current ?? [])
        {
            var before = previous.FirstOrDefault(item =>
                string.Equals(item.Id, connection.Id, StringComparison.OrdinalIgnoreCase));
            if (before == null)
                continue;

            var stored = string.IsNullOrWhiteSpace(before.IncomingFrom)
                ? ""
                : EtrnConnection.NormalizeIncomingFrom(before.IncomingFrom);
            if (stored == connection.IncomingFrom)
                continue;

            var found = await _etrnSync.Find(connection.Id);
            if (found.IsFailure || found.Value == null)
                continue;

            found.Value.EventId = "";
            found.Value.DocumentId = "";
            found.Value.EventAt = "";
            var written = await _etrnSync.Save(found.Value);
            if (written.IsFailure)
                continue;
        }
    }

    private static void Apply(OrganizationEntity entity, OrganizationView data)
    {
        entity.Name = data.Name;
        entity.Inn = data.Inn;
        var incoming = data.TrueApiIntegrationSettings ?? new TrueApiIntegrationSettings();
        incoming.Password = SecretMask.Restore(
            incoming.Password,
            entity.TrueApiIntegrationSettings?.Password ?? string.Empty);
        entity.TrueApiIntegrationSettings = incoming;
        if (data.VetisSettings != null)
            OrganizationVetisScope.Apply(entity.VetisSettings ??= new(), data.VetisSettings);
        if (data.EtrnConnections != null)
        {
            var stored = entity.EtrnConnections ?? [];
            entity.EtrnConnections = data.EtrnConnections
                .Select(connection =>
                {
                    var id = string.IsNullOrWhiteSpace(connection.Id) ? Guid.NewGuid().ToString() : connection.Id;
                    var previous = stored.FirstOrDefault(item =>
                        string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
                    var type = string.IsNullOrWhiteSpace(connection.Type) ? EtrnOperatorType.Saby : connection.Type.Trim();
                    var ofd = string.Equals(type, EtrnOperatorType.Ofd, StringComparison.OrdinalIgnoreCase);
                    return new EtrnConnection
                    {
                        Id = id,
                        Type = type,
                        Name = (connection.Name ?? "").Trim(),
                        Enable = connection.Enable,
                        SubscriberId = ofd ? (connection.SubscriberId ?? "").Trim() : "",
                        EdoId = ofd ? "" : (connection.EdoId ?? "").Trim(),
                        ApiKey = ofd ? RestoreSecret(connection.ApiKey, previous?.ApiKey ?? "") : "",
                        ClientId = ofd ? (connection.ClientId ?? "").Trim() : "",
                        ClientSecret = ofd ? RestoreSecret(connection.ClientSecret, previous?.ClientSecret ?? "") : "",
                        KeycloakUrl = ofd ? TrimAddress(connection.KeycloakUrl) : "",
                        Realm = ofd ? (connection.Realm ?? "").Trim() : "",
                        ApiUrl = ofd ? TrimAddress(connection.ApiUrl) : "",
                        IncomingFrom = EtrnConnection.NormalizeIncomingFrom(connection.IncomingFrom)
                    };
                })
                .ToList();
        }
    }

    private OrganizationView ToView(OrganizationEntity entity)
    {
        entity.NormalizeGisMtLastStatus();
        var cached = _applicationState.TrueApiToken(entity.Inn);
        var received = !string.IsNullOrEmpty(cached.Token);

        return new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Inn = entity.Inn,
            TrueApiEnabled = entity.TrueApiIntegrationSettings?.Enable ?? false,
            TrueApiTokenReceived = received,
            TrueApiTokenExpired = received ? cached.LiveUntil : null,
            XApiKeyExpiresAt = entity.XApiKeyExpiresAt,
            GisMtLastStatus = CopyStatus(entity.GisMtLastStatus),
            GisMtProductGroups = CopyGroups(entity.GisMtProductGroups),
            TrueApiIntegrationSettings = ForClient(entity.TrueApiIntegrationSettings),
            VetisSettings = ForClient(entity.VetisSettings),
            EtrnConnections = (entity.EtrnConnections ?? [])
                .Select(connection => new EtrnConnection
                {
                    Id = connection.Id,
                    Type = connection.Type,
                    Name = connection.Name,
                    Enable = connection.Enable,
                    SubscriberId = connection.SubscriberId,
                    EdoId = connection.EdoId,
                    ApiKey = SecretMask.ForClient(connection.ApiKey),
                    ClientId = connection.ClientId,
                    ClientSecret = SecretMask.ForClient(connection.ClientSecret),
                    KeycloakUrl = connection.KeycloakUrl,
                    Realm = connection.Realm,
                    ApiUrl = connection.ApiUrl,
                    IncomingFrom = connection.ResolvedIncomingFrom()
                })
                .ToList()
        };
    }

    private static TrueApiIntegrationSettings ForClient(TrueApiIntegrationSettings? settings)
    {
        settings ??= new();
        return new TrueApiIntegrationSettings
        {
            Enable = settings.Enable,
            Password = SecretMask.ForClient(settings.Password),
            DigitalSignature = settings.DigitalSignature
        };
    }

    private static OrganizationVetisSettings ForClient(OrganizationVetisSettings? settings)
    {
        settings ??= new();
        return new OrganizationVetisSettings
        {
            Enable = settings.Enable,
            ApiKey = SecretMask.ForClient(settings.ApiKey),
            BasicLogin = settings.BasicLogin,
            BasicPassword = SecretMask.ForClient(settings.BasicPassword),
            IssuerId = settings.IssuerId,
            InitiatorLogin = settings.InitiatorLogin,
            IncomingFrom = settings.IncomingFrom,
            AutoQuench = settings.AutoQuench,
            AutoQuenchDelayHours = settings.AutoQuenchDelayHours,
            Enterprises = settings.Enterprises ?? []
        };
    }

    private static GisMtLastStatus CopyStatus(GisMtLastStatus? status)
    {
        status ??= new();
        return new GisMtLastStatus
        {
            Code = status.Code,
            Description = status.Description ?? string.Empty,
            At = status.At
        };
    }

    private static List<GisMtConnectedProductGroup> CopyGroups(List<GisMtConnectedProductGroup>? groups)
        => (groups ?? [])
            .Select(item => new GisMtConnectedProductGroup
            {
                Code = item.Code,
                Name = item.Name ?? string.Empty,
                GroupName = item.GroupName ?? string.Empty
            })
            .ToList();

    private static TrueApiTokenView ToTokenView(TrueApiToken token) => new()
    {
        Token = token.Token,
        Expired = token.LiveUntil
    };
}
