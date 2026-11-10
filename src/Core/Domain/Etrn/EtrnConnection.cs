using System.Text.Json.Serialization;

namespace Domain.Etrn;

public static class EtrnOperatorType
{
    public const string Saby = "saby";
    public const string Ofd = "ofd";

    private const string SabyLabel = "Saby";
    private const string OfdLabel = "Платформа ОФД";

    public static string Label(string? type)
    {
        var normalized = (type ?? "").Trim();
        if (string.Equals(normalized, Saby, StringComparison.OrdinalIgnoreCase))
            return SabyLabel;
        if (string.Equals(normalized, Ofd, StringComparison.OrdinalIgnoreCase))
            return OfdLabel;
        return normalized;
    }
}

public sealed class EtrnConnection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = EtrnOperatorType.Saby;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("subscriberId")]
    public string SubscriberId { get; set; } = "";

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";
}

public sealed class EtrnOperator
{
    [JsonPropertyName("connectionId")]
    public string ConnectionId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = EtrnOperatorType.Saby;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Снимок на момент загрузки: переименование или удаление подключения уже записанный объект не меняет.
    /// </summary>
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? EtrnOperatorType.Label(Type) : Name.Trim();

    public static EtrnOperator For(EtrnConnection connection) => new()
    {
        ConnectionId = connection.Id,
        Type = connection.Type,
        Name = connection.Name
    };
}
