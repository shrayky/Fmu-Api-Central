namespace Domain.Vetis;

public sealed class VetisCall
{
    public string Endpoint { get; init; } = "";

    public string ServiceId { get; init; } = VetisEndpoints.ServiceId;

    public string ApiKey { get; init; } = "";

    public string BasicLogin { get; init; } = "";

    public string BasicPassword { get; init; } = "";

    public string IssuerId { get; init; } = "";

    public string InitiatorLogin { get; init; } = "";

    public int TimeoutSeconds { get; init; } = 60;

    public int ResultPollAttempts { get; init; } = 8;

    public int ResultPollDelaySeconds { get; init; } = 3;
}

public sealed class VetisExchange
{
    public bool Completed { get; init; }

    public string? ApplicationId { get; init; }

    public string Status { get; init; } = "";

    public string? Error { get; init; }

    public string Operation { get; init; } = "";

    public string LocalTransactionId { get; init; } = "";

    public string Parameters { get; init; } = "";

    /// <summary>
    /// Ключ API в сохранённом запросе заменён маской.
    /// </summary>
    public string RequestXml { get; init; } = "";

    public string ResponseXml { get; init; } = "";
}

public sealed class VetisEnterpriseList
{
    public bool Completed { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<VetisEnterprise> Enterprises { get; init; } = [];

    public IReadOnlyList<VetisExchange> Exchanges { get; init; } = [];
}

public sealed class VetisIncomingRequest
{
    public string EnterpriseGuid { get; init; } = "";

    public DateTimeOffset Begin { get; init; }

    public DateTimeOffset End { get; init; }

    public int Offset { get; init; }

    public int Count { get; init; } = IncomingChangesWindow.PageSize;
}

public sealed class VetisIncomingResult
{
    public VetisExchange Exchange { get; init; } = new();

    public IReadOnlyList<VetisIncomingVsd> Documents { get; init; } = [];

    /// <summary>
    /// История изменений содержит и исходящие ВСД: страница полна, даже если входящих в ней меньше.
    /// </summary>
    public int PageCount { get; init; }
}

public sealed class VetisProbeRequest
{
    public string ApiKey { get; set; } = "";

    public string BasicLogin { get; set; } = "";

    public string? BasicPassword { get; set; }

    public string IssuerId { get; set; } = "";

    public string InitiatorLogin { get; set; } = "";
}

public sealed class VetisProbeOutcome
{
    public bool Ok { get; init; }

    public string Message { get; init; } = "";

    public IReadOnlyList<Domain.Entitys.Organization.OrganizationEnterprise> Enterprises { get; init; } = [];
}

public interface IVetisGateway
{
    Task<VetisExchange> CheckConnectionAsync(VetisCall call, CancellationToken cancellationToken);

    Task<VetisEnterpriseList> ListEnterprisesAsync(VetisCall call, CancellationToken cancellationToken);

    Task<VetisIncomingResult> GetIncomingAsync(VetisCall call, VetisIncomingRequest request, CancellationToken cancellationToken);
}
