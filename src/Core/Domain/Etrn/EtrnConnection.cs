using System.Globalization;
using System.Text.Json.Serialization;

namespace Domain.Etrn;

public static class EtrnOperatorType
{
    public const string Saby = "saby";
    public const string Ofd = "ofd";

    private const string SabyLabel = "Saby";
    private const string OfdLabel = "Платформа ЭТрН";

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
    /// Идентификатор участника ЭДО в Saby. Второй идентификатор имени ON_TRNACLGROT сверяется с ним.
    /// </summary>
    [JsonPropertyName("edoId")]
    public string EdoId { get; set; } = "";

    /// <summary>
    /// С этой даты ЭТрН обязательна. Пустая дата подключения означает её.
    /// </summary>
    public const string DefaultIncomingFrom = "2026-09-01";

    /// <summary>
    /// Пустой адрес не подменяется стендом: контур задаётся в карточке.
    /// </summary>
    public string ResolvedKeycloakUrl() => TrimAddress(KeycloakUrl);

    public string ResolvedRealm() => (Realm ?? "").Trim();

    public string ResolvedApiUrl() => TrimAddress(ApiUrl);

    /// <summary>
    /// Токен по сертификату запрашивается на боевом хосте ЭДО, не на хосте списка ЭТрН.
    /// </summary>
    public string ResolvedEdoUrl()
    {
        if (!Uri.TryCreate(ResolvedApiUrl(), UriKind.Absolute, out var api))
            return "";

        if (api.Host.Equals("api.platformaofd.ru", StringComparison.OrdinalIgnoreCase)
            || api.Host.Equals("epd.platformaofd.ru", StringComparison.OrdinalIgnoreCase))
            return "https://edo.platformaofd.ru";
        return "";
    }

    public string MissingOfdAddress()
    {
        if (ResolvedApiUrl().Length == 0)
            return "Не задан адрес API Платформы ЭТрН";
        if (ResolvedEdoUrl().Length == 0)
            return "Для этого адреса API нет хоста токена по сертификату";
        return "";
    }

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

    private static string TrimAddress(string? value) => (value ?? "").Trim().TrimEnd('/');
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
