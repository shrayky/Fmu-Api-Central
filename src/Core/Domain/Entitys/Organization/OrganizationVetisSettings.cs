using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

namespace Domain.Entitys.Organization;

public sealed class OrganizationEnterprise
{
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonPropertyName("presentation")]
    public string Presentation { get; set; } = "";

    [JsonPropertyName("incomingFrom")]
    public string IncomingFrom { get; set; } = "";

    [JsonPropertyName("participates")]
    public bool Participates { get; set; }
}

public sealed class OrganizationVetisSettings
{
    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("basicLogin")]
    public string BasicLogin { get; set; } = "";

    [JsonPropertyName("basicPassword")]
    public string BasicPassword { get; set; } = "";

    [JsonPropertyName("issuerId")]
    public string IssuerId { get; set; } = "";

    [JsonPropertyName("initiatorLogin")]
    public string InitiatorLogin { get; set; } = "";

    [JsonPropertyName("incomingFrom")]
    public string IncomingFrom { get; set; } = "";

    [JsonPropertyName("enterprises")]
    public List<OrganizationEnterprise> Enterprises { get; set; } = [];

    [JsonPropertyName("autoQuench")]
    public bool AutoQuench { get; set; }

    [JsonPropertyName("autoQuenchDelayHours")]
    public int AutoQuenchDelayHours { get; set; } = 1;

    /// <summary>
    /// Уже лежащий ноль не должен гасить в ту же минуту: запись такой задержки не принимает.
    /// </summary>
    public int DelayHours() => AutoQuenchDelayHours > 0 ? AutoQuenchDelayHours : 1;

    public Result CheckDelay() =>
        AutoQuenchDelayHours > 0
            ? Result.Success()
            : Result.Failure("Задержка перед автогашением: нужно целое число больше нуля.");
}
