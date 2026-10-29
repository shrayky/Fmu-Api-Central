using System.Text.Json.Serialization;
using Domain.Entitys.Interfaces;

namespace Domain.Vetis;

public sealed class VetisApplicationEntity : IHaveStringId
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "";

    [JsonPropertyName("applicationId")]
    public string ApplicationId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = "";

    [JsonPropertyName("enterpriseGuid")]
    public string EnterpriseGuid { get; set; } = "";

    [JsonPropertyName("parameters")]
    public string Parameters { get; set; } = "";

    [JsonPropertyName("requestXml")]
    public string RequestXml { get; set; } = "";

    [JsonPropertyName("responseXml")]
    public string ResponseXml { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    // Хранится отдельно: список заявок читается без тяжёлых XML.
    [JsonPropertyName("hasResponse")]
    public bool HasResponse { get; set; }

    public static VetisApplicationEntity From(
        VetisExchange exchange,
        string organizationId,
        string enterpriseGuid,
        DateTimeOffset now) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(exchange.LocalTransactionId)
                ? Guid.NewGuid().ToString("N")
                : exchange.LocalTransactionId,
            Operation = exchange.Operation,
            ApplicationId = exchange.ApplicationId ?? "",
            Status = string.IsNullOrWhiteSpace(exchange.Status) ? "NOT_SENT" : exchange.Status,
            Completed = exchange.Completed,
            Error = exchange.Error ?? "",
            OrganizationId = organizationId,
            EnterpriseGuid = enterpriseGuid,
            Parameters = exchange.Parameters,
            RequestXml = exchange.RequestXml,
            ResponseXml = exchange.ResponseXml,
            HasResponse = !string.IsNullOrWhiteSpace(exchange.ResponseXml),
            CreatedAt = now.ToUnixTimeSeconds()
        };
}
