using System.Text.Json.Serialization;

namespace Domain.Vetis;

public class VetisIncomingRow
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("number")]
    public string Number { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("loose")]
    public bool Loose { get; set; }

    [JsonPropertyName("consignor")]
    public string Consignor { get; set; } = "";

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = "";

    [JsonPropertyName("organizationName")]
    public string OrganizationName { get; set; } = "";

    [JsonPropertyName("enterpriseGuid")]
    public string EnterpriseGuid { get; set; } = "";

    [JsonPropertyName("enterpriseName")]
    public string EnterpriseName { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("vsdCount")]
    public int VsdCount { get; set; }

    [JsonPropertyName("acceptedCount")]
    public int AcceptedCount { get; set; }

    [JsonPropertyName("sortAt")]
    public long SortAt { get; set; }

    [JsonPropertyName("receivedAt")]
    public string ReceivedAt { get; set; } = "";
}

public sealed class VetisIncomingView : VetisIncomingRow
{
    [JsonPropertyName("vsds")]
    public List<VetisVsdView> Vsds { get; set; } = [];
}

public sealed class VetisVsdView
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("issueNumber")]
    public string IssueNumber { get; set; } = "";

    [JsonPropertyName("issueDate")]
    public string IssueDate { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "";

    [JsonPropertyName("volume")]
    public string Volume { get; set; } = "";

    [JsonPropertyName("transport")]
    public string Transport { get; set; } = "";

    [JsonPropertyName("consignorName")]
    public string ConsignorName { get; set; } = "";

    [JsonPropertyName("receivedAt")]
    public string ReceivedAt { get; set; } = "";

    [JsonPropertyName("rawXml")]
    public string RawXml { get; set; } = "";
}

public class VetisApplicationRow
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "";

    [JsonPropertyName("applicationId")]
    public string ApplicationId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("hasResponse")]
    public bool HasResponse { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("parameters")]
    public string Parameters { get; set; } = "";

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = "";

    [JsonPropertyName("organizationName")]
    public string OrganizationName { get; set; } = "";

    [JsonPropertyName("enterpriseGuid")]
    public string EnterpriseGuid { get; set; } = "";

    [JsonPropertyName("enterpriseName")]
    public string EnterpriseName { get; set; } = "";
}

public sealed class VetisApplicationView : VetisApplicationRow
{
    [JsonPropertyName("requestXml")]
    public string RequestXml { get; set; } = "";

    [JsonPropertyName("responseXml")]
    public string ResponseXml { get; set; } = "";
}

public sealed class VetisRowsPage<T>
{
    [JsonPropertyName("rows")]
    public List<T> Rows { get; set; } = [];

    [JsonPropertyName("skip")]
    public int Skip { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }
}

public sealed class VetisFilterEnterprise
{
    [JsonPropertyName("guid")]
    public string Guid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class VetisFilterOrganization
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("enterprises")]
    public List<VetisFilterEnterprise> Enterprises { get; set; } = [];
}
