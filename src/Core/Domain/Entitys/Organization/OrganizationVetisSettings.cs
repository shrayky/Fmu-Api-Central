using System.Text.Json.Serialization;

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
}
