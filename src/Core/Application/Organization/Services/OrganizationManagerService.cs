using CSharpFunctionalExtensions;
using Domain.AppState.Interfaces;
using Domain.Attributes;
using Domain.Dto.Responces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Dto;
using Domain.Entitys.Organization.Interfaces;
using Domain.GisMt.Models;
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

    public OrganizationManagerService(
        IOrganizationRepository repository,
        ITrueApiAuthService trueApiAuthService,
        IApplicationState applicationState)
    {
        _repository = repository;
        _trueApiAuthService = trueApiAuthService;
        _applicationState = applicationState;
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

        Apply(exist.Value, prepared.Value);
        return await _repository.Update(exist.Value);
    }

    public async Task<Result> Delete(string id)
    {
        var exist = await _repository.GetById(id);
        if (exist.IsFailure)
            return Result.Failure(exist.Error);

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

        var lifeUntil = DateTime.Now.AddHours(TrueApiTokenDefaults.LifeHours);
        _applicationState.UpdateTrueApiToken(normalizedInn, generated.Value, lifeUntil);

        return Result.Success(ToTokenView(new TrueApiToken
        {
            Inn = normalizedInn,
            Token = generated.Value,
            LiveUntil = lifeUntil
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

        data.Name = name;
        data.Inn = inn;
        data.TrueApiIntegrationSettings ??= new TrueApiIntegrationSettings();
        return Result.Success(data);
    }

    private static string NormalizeInn(string? inn) => (inn ?? string.Empty).Trim();

    private static void Apply(OrganizationEntity entity, OrganizationView data)
    {
        entity.Name = data.Name;
        entity.Inn = data.Inn;
        entity.TrueApiIntegrationSettings = data.TrueApiIntegrationSettings ?? new TrueApiIntegrationSettings();
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
            GisMtLastStatus = CopyStatus(entity.GisMtLastStatus),
            GisMtProductGroups = CopyGroups(entity.GisMtProductGroups),
            TrueApiIntegrationSettings = entity.TrueApiIntegrationSettings ?? new TrueApiIntegrationSettings()
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
