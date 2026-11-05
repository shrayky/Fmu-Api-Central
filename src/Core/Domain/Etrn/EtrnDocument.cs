using System.Globalization;
using System.Text.Json.Serialization;
using Domain.Entitys.Interfaces;

namespace Domain.Etrn;

public static class EtrnDocumentStatus
{
    public const string Waiting = "Waiting";
    public const string Sending = "Sending";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Closed = "Closed";

    public const string NoQuantityError = "В строке груза нет количества";

    public static bool IsFinal(string? status) =>
        status is Accepted or Rejected or Closed;

    public static bool CanAccept(string? status) => status == Waiting;

    public static string Label(string? status, string? action = "") => status switch
    {
        Waiting => "Ждёт приёмки",
        Sending => "Отправляется",
        Accepted => action == EtrnAcceptanceAction.Partially
            ? EtrnAcceptanceAction.Partially
            : EtrnAcceptanceAction.Received,
        Rejected => EtrnAcceptanceAction.Rejected,
        Closed => "Закрыт",
        _ => status ?? ""
    };
}

public static class EtrnAcceptanceAction
{
    public const string Received = "Получен";
    public const string Partially = "Получен частично";
    public const string Rejected = "Не принят";
}

public static class EtrnQuantity
{
    public static bool TryWeight(string? text, out decimal value)
    {
        value = 0;
        var raw = (text ?? "").Trim().Replace(',', '.');
        return raw.Length > 0 && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static bool SameWeight(string? left, string? right) =>
        TryWeight(left, out var one) && TryWeight(right, out var other) && one == other;
}

public sealed class EtrnCargoLine
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("places")]
    public int? Places { get; set; }

    /// <summary>
    /// Килограммы текстом: в титуле бывает "10.000", а сравнение идёт числом.
    /// </summary>
    [JsonPropertyName("grossWeight")]
    public string GrossWeight { get; set; } = "";

    [JsonPropertyName("factPlaces")]
    public int? FactPlaces { get; set; }

    [JsonPropertyName("factWeight")]
    public string FactWeight { get; set; } = "";

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonIgnore]
    public bool HasPlaces => Places != null;

    [JsonIgnore]
    public bool HasWeight => EtrnQuantity.TryWeight(GrossWeight, out _);

    [JsonIgnore]
    public bool HasQuantity => HasPlaces || HasWeight;
}

public sealed class EtrnIncomingDocumentEntity : IHaveStringId
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = "";

    [JsonPropertyName("operator")]
    public EtrnOperator Operator { get; set; } = new();

    [JsonPropertyName("number")]
    public string Number { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("consignor")]
    public string Consignor { get; set; } = "";

    /// <summary>
    /// УИД ГИС ЭПД из титула, если он там есть.
    /// </summary>
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("deliveryAddress")]
    public string DeliveryAddress { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = EtrnDocumentStatus.Waiting;

    [JsonPropertyName("lines")]
    public List<EtrnCargoLine> Lines { get; set; } = [];

    [JsonPropertyName("generalComment")]
    public string GeneralComment { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("signedAt")]
    public string SignedAt { get; set; } = "";

    [JsonPropertyName("sendingAt")]
    public DateTimeOffset? SendingAt { get; set; }

    [JsonPropertyName("lastError")]
    public string LastError { get; set; } = "";

    [JsonPropertyName("rawXml")]
    public string RawXml { get; set; } = "";

    [JsonPropertyName("sortAt")]
    public long SortAt { get; set; }

    [JsonIgnore]
    public bool HasQuantity => Lines.Count > 0 && Lines.All(line => line.HasQuantity);

    [JsonIgnore]
    public bool CanDecide => EtrnDocumentStatus.CanAccept(Status) && HasQuantity;

    [JsonIgnore]
    public string StatusLabel => EtrnDocumentStatus.Label(Status, Action);

    public static string BuildId(string organizationId, string connectionId, string operatorDocumentId) =>
        $"doc:{organizationId}:{connectionId}:{operatorDocumentId}";

    /// <summary>
    /// Полночь UTC календарного дня: порядок дат не зависит от пояса хоста.
    /// </summary>
    public static long SortDate(string? date)
    {
        if (!DateOnly.TryParseExact((date ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return 0;

        return new DateTimeOffset(day, TimeOnly.MinValue, TimeSpan.Zero).ToUnixTimeSeconds();
    }
}
