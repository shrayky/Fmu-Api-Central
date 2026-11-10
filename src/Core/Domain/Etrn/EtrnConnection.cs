using System.Globalization;
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

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = "";

    [JsonPropertyName("clientSecret")]
    public string ClientSecret { get; set; } = "";

    [JsonPropertyName("keycloakUrl")]
    public string KeycloakUrl { get; set; } = "";

    [JsonPropertyName("realm")]
    public string Realm { get; set; } = "";

    [JsonPropertyName("apiUrl")]
    public string ApiUrl { get; set; } = "";

    [JsonPropertyName("incomingFrom")]
    public string IncomingFrom { get; set; } = "";

    /// <summary>
    /// С этой даты ЭТрН обязательна. Пустая дата подключения означает её.
    /// </summary>
    public const string DefaultIncomingFrom = "2026-09-01";

    public const string DefaultKeycloakUrl = "https://oauth-test.platformaofd.ru";
    public const string DefaultRealm = "playground";
    public const string DefaultApiUrl = "https://api-test.platformaofd.ru";

    public string ResolvedKeycloakUrl() => AddressOr(KeycloakUrl, DefaultKeycloakUrl);

    public string ResolvedRealm() => string.IsNullOrWhiteSpace(Realm) ? DefaultRealm : Realm.Trim();

    public string ResolvedApiUrl() => AddressOr(ApiUrl, DefaultApiUrl);

    public static string NormalizeIncomingFrom(string? value)
    {
        var text = (value ?? "").Trim();
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
            return iso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (DateOnly.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var russian))
            return russian.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return DefaultIncomingFrom;
    }

    public string ResolvedIncomingFrom() => NormalizeIncomingFrom(IncomingFrom);

    private static string AddressOr(string? value, string fallback)
    {
        var text = (value ?? "").Trim().TrimEnd('/');
        return text.Length == 0 ? fallback : text;
    }
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
