using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Entitys.Organization;
using Domain.Entitys.Organization.Interfaces;
using Domain.Vetis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Vetis;

[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisProbeService
{
    private readonly IVetisGateway _gateway;
    private readonly IOrganizationRepository _organizations;
    private readonly IParametersService _parameters;
    private readonly ILogger<VetisProbeService> _logger;

    public VetisProbeService(
        IVetisGateway gateway,
        IOrganizationRepository organizations,
        IParametersService parameters,
        ILogger<VetisProbeService> logger)
    {
        _gateway = gateway;
        _organizations = organizations;
        _parameters = parameters;
        _logger = logger;
    }

    public async Task<Result<VetisProbeOutcome>> CheckAsync(
        string organizationId,
        VetisProbeRequest request,
        CancellationToken cancellationToken)
    {
        var prepared = await Prepare(organizationId, request, initiatorRequired: true, apiKeyRequired: true);
        if (prepared.IsFailure)
            return Result.Failure<VetisProbeOutcome>(prepared.Error);

        var exchange = await _gateway.CheckConnectionAsync(prepared.Value, cancellationToken);
        _logger.LogInformation(
            "Проверка соединения Ветис: {Status} {ApplicationId}",
            exchange.Status,
            exchange.ApplicationId);
        if (!exchange.Completed)
        {
            return Result.Success(new VetisProbeOutcome
            {
                Ok = false,
                Message = exchange.Error ?? "Ветис не подтвердил соединение."
            });
        }

        var application = string.IsNullOrWhiteSpace(exchange.ApplicationId)
            ? ""
            : $", заявка {exchange.ApplicationId}";
        return Result.Success(new VetisProbeOutcome
        {
            Ok = true,
            Message = $"Ветис подтвердил GetBusinessEntityUser, статус {exchange.Status}{application}."
        });
    }

    public async Task<Result<VetisProbeOutcome>> ListEnterprisesAsync(
        string organizationId,
        VetisProbeRequest request,
        CancellationToken cancellationToken)
    {
        var prepared = await Prepare(organizationId, request, initiatorRequired: false, apiKeyRequired: false);
        if (prepared.IsFailure)
            return Result.Failure<VetisProbeOutcome>(prepared.Error);

        var list = await _gateway.ListEnterprisesAsync(prepared.Value, cancellationToken);
        if (!list.Completed)
        {
            return Result.Success(new VetisProbeOutcome
            {
                Ok = false,
                Message = list.Error ?? "Ветис не вернул список площадок."
            });
        }

        var stored = await Stored(organizationId);
        var merged = OrganizationVetisScope.Merge(stored, list.Enterprises);
        return Result.Success(new VetisProbeOutcome
        {
            Ok = true,
            Message = merged.Count == 0
                ? "Ветис вернул пустой список площадок."
                : $"Ветис вернул площадок: {list.Enterprises.Count}. Отметьте нужные.",
            Enterprises = merged
        });
    }

    private async Task<Result<VetisCall>> Prepare(
        string organizationId,
        VetisProbeRequest request,
        bool initiatorRequired,
        bool apiKeyRequired)
    {
        var settings = (await _parameters.Current()).VetisSettings ?? new();
        if (!settings.Enable)
            return Result.Failure<VetisCall>("Ветис API выключен в настройках сервера.");

        if (!VetisEndpoints.Accept(settings.Endpoint))
            return Result.Failure<VetisCall>("Адрес сервиса Ветис должен начинаться с http:// или https://.");

        var organization = await Find(organizationId);
        var password = request.BasicPassword ?? "";
        if (password.Length == 0 || SecretMask.IsPlaceholder(password))
            password = organization?.VetisSettings.BasicPassword ?? "";

        var apiKey = (request.ApiKey ?? "").Trim();
        if (apiKey.Length == 0 || SecretMask.IsPlaceholder(apiKey))
            apiKey = (organization?.VetisSettings.ApiKey ?? "").Trim();

        var missing = new List<string>();
        if (apiKeyRequired && string.IsNullOrWhiteSpace(apiKey))
            missing.Add("API-ключ");
        if (string.IsNullOrWhiteSpace(request.BasicLogin))
            missing.Add("пользователя API");
        if (string.IsNullOrWhiteSpace(password))
            missing.Add("пароль API");
        if (string.IsNullOrWhiteSpace(request.IssuerId))
            missing.Add("IssuerId");
        if (initiatorRequired && string.IsNullOrWhiteSpace(request.InitiatorLogin))
            missing.Add("логин оператора");
        if (missing.Count > 0)
            return Result.Failure<VetisCall>("Заполните " + string.Join(", ", missing) + ".");

        return Result.Success(new VetisCall
        {
            Endpoint = VetisEndpoints.Resolve(settings.Endpoint),
            ServiceId = VetisEndpoints.ServiceId,
            ApiKey = apiKey,
            BasicLogin = request.BasicLogin.Trim(),
            BasicPassword = password,
            IssuerId = request.IssuerId.Trim(),
            InitiatorLogin = (request.InitiatorLogin ?? "").Trim(),
            TimeoutSeconds = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 60,
            ResultPollAttempts = settings.ResultPollAttempts > 0 ? settings.ResultPollAttempts : 8,
            ResultPollDelaySeconds = settings.ResultPollDelaySeconds > 0 ? settings.ResultPollDelaySeconds : 3
        });
    }

    private async Task<OrganizationEntity?> Find(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
            return null;

        var found = await _organizations.GetById(organizationId);
        return found.IsSuccess ? found.Value : null;
    }

    private async Task<IReadOnlyList<OrganizationEnterprise>> Stored(string organizationId)
    {
        var organization = await Find(organizationId);
        return organization?.VetisSettings.Enterprises ?? [];
    }
}
