using System.Text.Json.Serialization;

namespace Domain.Dto.fmuApiStateInformation;

public record FmuApiParameters
{
    [JsonPropertyName("appName")]
    public string AppName { get; init; } = string.Empty;

    [JsonPropertyName("appVersion")]
    public int AppVersion { get; init; }

    [JsonPropertyName("assembly")]
    public int Assembly { get; init; }

    [JsonPropertyName("nodeName")]
    public string NodeName { get; init; } = string.Empty;

    [JsonPropertyName("serverConfig")]
    public ServerConfig ServerConfig { get; init; } = new();

    [JsonPropertyName("hostsToPing")]
    public List<string> HostsToPing { get; init; } = [];

    [JsonPropertyName("minimalPrices")]
    public MinimalPrices MinimalPrices { get; init; } = new();

    [JsonPropertyName("organisationConfig")]
    public OrganisationConfig OrganisationConfig { get; init; } = new();

    [JsonPropertyName("frontolAlcoUnit")]
    public FrontolAlcoUnit FrontolAlcoUnit { get; init; } = new();

    [JsonPropertyName("database")]
    public DatabaseConfig Database { get; init; } = new();

    [JsonPropertyName("trueSignTokenService")]
    public TrueSignTokenService TrueSignTokenService { get; init; } = new();

    [JsonPropertyName("httpRequestTimeouts")]
    public HttpRequestTimeouts HttpRequestTimeouts { get; init; } = new();

    [JsonPropertyName("logging")]
    public LoggingConfig Logging { get; init; } = new();

    [JsonPropertyName("frontolConnectionSettings")]
    public FrontolConnectionSettings FrontolConnectionSettings { get; init; } = new();

    [JsonPropertyName("saleControlConfig")]
    public SaleControlConfig SaleControlConfig { get; init; } = new();

    [JsonPropertyName("fmuApiCentralServer")]
    public FmuApiCentralServer FmuApiCentralServer { get; init; } = new();

    [JsonPropertyName("autoUpdate")]
    public AutoUpdate AutoUpdate { get; init; } = new();
}

public record ServerConfig
{
    [JsonPropertyName("apiIpPort")]
    public int ApiIpPort { get; init; }
}

public record MinimalPrices
{
    [JsonPropertyName("tabaco")]
    public int Tabaco { get; init; }
}

public record OrganisationConfig
{
    [JsonPropertyName("printGroups")]
    public List<PrintGroup> PrintGroups { get; init; } = [];
}

public record PrintGroup
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("xapikey")]
    public string XApiKey { get; init; } = string.Empty;

    [JsonPropertyName("inn")]
    public string Inn { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("localModuleConnection")]
    public LocalModuleConnection LocalModuleConnection { get; init; } = new();
}

public record LocalModuleConnection
{
    [JsonPropertyName("enable")]
    public bool Enable { get; init; }

    [JsonPropertyName("connectionAddress")]
    public string ConnectionAddress { get; init; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;
}

public record FrontolAlcoUnit
{
    [JsonPropertyName("netAdres")]
    public string NetAdres { get; init; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;
}

public record DatabaseConfig
{
    [JsonPropertyName("enable")]
    public bool Enable { get; init; }

    [JsonPropertyName("netAddress")]
    public string NetAddress { get; init; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;

    [JsonPropertyName("bulkBatchSize")]
    public int BulkBatchSize { get; init; }

    [JsonPropertyName("bulkParallelTasks")]
    public int BulkParallelTasks { get; init; }

    [JsonPropertyName("queryLimit")]
    public int QueryLimit { get; init; }

    [JsonPropertyName("netAdres")]
    public string NetAdres { get; init; } = string.Empty;

    [JsonPropertyName("marksStateDbName")]
    public string MarksStateDbName { get; init; } = string.Empty;

    [JsonPropertyName("frontolDocumentsDbName")]
    public string FrontolDocumentsDbName { get; init; } = string.Empty;

    [JsonPropertyName("alcoStampsDbName")]
    public string AlcoStampsDbName { get; init; } = string.Empty;
}

public record TrueSignTokenService
{
    [JsonPropertyName("enable")]
    public bool Enable { get; init; }

    [JsonPropertyName("connectionAddress")]
    public string ConnectionAddress { get; init; } = string.Empty;
}

public record HttpRequestTimeouts
{
    [JsonPropertyName("cdnRequestTimeout")]
    public int CdnRequestTimeout { get; init; }

    [JsonPropertyName("checkMarkRequestTimeout")]
    public int CheckMarkRequestTimeout { get; init; }

    [JsonPropertyName("checkInternetConnectionTimeout")]
    public int CheckInternetConnectionTimeout { get; init; }
}

public record LoggingConfig
{
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; init; }

    [JsonPropertyName("logLevel")]
    public string LogLevel { get; init; } = string.Empty;

    [JsonPropertyName("logDepth")]
    public int LogDepth { get; init; }
}

public record FrontolConnectionSettings
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;
}

public record SaleControlConfig
{
    [JsonPropertyName("banSalesReturnedWares")]
    public bool BanSalesReturnedWares { get; init; }

    [JsonPropertyName("ignoreVerificationErrorForTrueApiGroups")]
    public string IgnoreVerificationErrorForTrueApiGroups { get; init; } = string.Empty;

    [JsonPropertyName("checkReceiptReturn")]
    public bool CheckReceiptReturn { get; init; }

    [JsonPropertyName("correctExpireDateInSaleReturn")]
    public bool CorrectExpireDateInSaleReturn { get; init; }

    [JsonPropertyName("sendEmptyTrueApiAnswerWhenTimeoutError")]
    public bool SendEmptyTrueApiAnswerWhenTimeoutError { get; init; }

    [JsonPropertyName("checkIsOwnerField")]
    public bool CheckIsOwnerField { get; init; }

    [JsonPropertyName("sendLocalModuleInformationalInRequestId")]
    public bool SendLocalModuleInformationalInRequestId { get; init; }

    [JsonPropertyName("rejectSalesWithoutCheckInformationFrom")]
    public DateTime RejectSalesWithoutCheckInformationFrom { get; init; }

    [JsonPropertyName("resetSoldStatusForReturn")]
    public bool ResetSoldStatusForReturn { get; init; }
}

public record FmuApiCentralServer
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;

    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("exchangeRequestInterval")]
    public int ExchangeRequestInterval { get; init; }
}

public record AutoUpdate
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("updateFilesCatalog")]
    public string UpdateFilesCatalog { get; init; } = string.Empty;

    [JsonPropertyName("fromHour")]
    public int FromHour { get; init; }

    [JsonPropertyName("untilHour")]
    public int UntilHour { get; init; }

    [JsonPropertyName("checkUpdateIntervalMinutes")]
    public int CheckUpdateIntervalMinutes { get; init; }
}