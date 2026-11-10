using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Etrn;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Etrn;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class EtrnProbeService
{
    private readonly IReadOnlyList<IEtrnOperator> _operators;
    private readonly IOrganizationRepository _organizations;
    private readonly EtrnRequestRecorder _recorder;

    public EtrnProbeService(
        IEnumerable<IEtrnOperator> operators,
        IOrganizationRepository organizations,
        EtrnRequestRecorder recorder)
    {
        _operators = operators.ToList();
        _organizations = organizations;
        _recorder = recorder;
    }

    public async Task<Result<EtrnProbeOutcome>> CheckAsync(
        string organizationId,
        EtrnProbeRequest request,
        CancellationToken cancellationToken)
    {
        var type = (request.Type ?? "").Trim();
        if (type.Length == 0)
            return Result.Failure<EtrnProbeOutcome>("Выберите подключение");

        var op = _operators.FirstOrDefault(item =>
            string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase));
        if (op == null)
            return Result.Failure<EtrnProbeOutcome>("Оператор ЭТрН не подключён");

        var organization = await Find(organizationId);
        var connection = Build(request, organization, type);
        var context = new EtrnOperatorContext
        {
            OrganizationId = organization?.Id ?? (organizationId ?? "").Trim(),
            Inn = organization?.Inn ?? "",
            Connection = connection,
            CertificateNumber = (request.CertificateNumber ?? "").Trim(),
            CertificatePassword = Restore(request.CertificatePassword, organization?.TrueApiIntegrationSettings?.Password ?? "")
        };

        var probe = await op.ProbeAsync(context, cancellationToken);
        if (probe.IsFailure)
        {
            return Result.Success(new EtrnProbeOutcome
            {
                Ok = false,
                Message = probe.Error
            });
        }

        await _recorder.RecordAsync(probe.Value.Exchanges, context.OrganizationId, context.Operator);
        return Result.Success(new EtrnProbeOutcome
        {
            Ok = probe.Value.Ok,
            Message = probe.Value.Message
        });
    }

    private async Task<OrganizationEntity?> Find(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
            return null;

        var found = await _organizations.GetById(organizationId);
        return found.IsSuccess ? found.Value : null;
    }

    private static EtrnConnection Build(EtrnProbeRequest request, OrganizationEntity? organization, string type)
    {
        var ofd = string.Equals(type, EtrnOperatorType.Ofd, StringComparison.OrdinalIgnoreCase);
        var id = (request.Id ?? "").Trim();
        var previous = organization?.EtrnConnections?.FirstOrDefault(item =>
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        return new EtrnConnection
        {
            Id = id,
            Type = type,
            Name = (request.Name ?? "").Trim(),
            SubscriberId = ofd ? (request.SubscriberId ?? "").Trim() : "",
            ApiKey = ofd ? Restore(request.ApiKey, previous?.ApiKey ?? "") : "",
            ClientId = ofd ? (request.ClientId ?? "").Trim() : "",
            ClientSecret = ofd ? Restore(request.ClientSecret, previous?.ClientSecret ?? "") : "",
            KeycloakUrl = ofd ? TrimAddress(request.KeycloakUrl) : "",
            Realm = ofd ? (request.Realm ?? "").Trim() : "",
            ApiUrl = ofd ? TrimAddress(request.ApiUrl) : ""
        };
    }

    /// <summary>
    /// Пустое поле и маска «***» оставляют уже сохранённый секрет: пароль на карточке не присылает старое значение.
    /// </summary>
    private static string Restore(string? incoming, string stored)
    {
        var value = (incoming ?? "").Trim();
        if (value.Length == 0 || SecretMask.IsPlaceholder(value))
            return stored;
        return value;
    }

    private static string TrimAddress(string? value) => (value ?? "").Trim().TrimEnd('/');
}
