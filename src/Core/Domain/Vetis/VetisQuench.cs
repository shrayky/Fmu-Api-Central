using System.Globalization;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

namespace Domain.Vetis;

public static class VetisQuench
{
    public const string AcceptAll = "ACCEPT_ALL";

    public const string Partially = "PARTIALLY";

    public const string ReturnAll = "RETURN_ALL";

    public static bool CanQuench(string? status) =>
        string.Equals(status, "CONFIRMED", StringComparison.OrdinalIgnoreCase);

    public static Result<VetisQuenchOrder> Decide(string? incoming, string? actual)
    {
        if (!TryParse(incoming, out var volume))
            return Result.Failure<VetisQuenchOrder>("В ВСД нет объёма, гашение не отправлено.");

        if (actual != null && string.IsNullOrWhiteSpace(actual))
            return Result.Failure<VetisQuenchOrder>("Фактическое количество должно быть от 0 до объёма поступления.");

        var factText = actual ?? incoming;
        if (!TryParse(factText, out var fact) || fact < 0 || fact > volume)
            return Result.Failure<VetisQuenchOrder>("Фактическое количество должно быть от 0 до объёма поступления.");

        var decision = fact == 0 ? ReturnAll : fact == volume ? AcceptAll : Partially;
        return Result.Success(new VetisQuenchOrder
        {
            Decision = decision,
            IncomingVolume = Format(volume),
            ActualVolume = Format(fact),
            KeepIncomingVolume = decision != Partially
        });
    }

    private static bool TryParse(string? text, out decimal volume)
    {
        var normalized = (text ?? "").Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out volume);
    }

    private static string Format(decimal volume) =>
        volume.ToString("0.#############################", CultureInfo.InvariantCulture);
}

/// <summary>
/// Ноль в объёме Меркурий не принимает: возврат всей партии задаётся решением RETURN_ALL, а не нулевым объёмом.
/// </summary>
public sealed class VetisQuenchOrder
{
    public string Decision { get; init; } = "";

    public string IncomingVolume { get; init; } = "";

    public string ActualVolume { get; init; } = "";

    public bool KeepIncomingVolume { get; init; }
}

public sealed class VetisQuenchFact
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("actualVolume")]
    public string ActualVolume { get; set; } = "";
}

public sealed class VetisQuenchCommand
{
    [JsonPropertyName("facts")]
    public List<VetisQuenchFact> Facts { get; set; } = [];
}

public sealed class VetisQuenchError
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public sealed class VetisQuenchProgress
{
    [JsonPropertyName("running")]
    public bool Running { get; init; }

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("uuid")]
    public string Uuid { get; init; } = "";

    [JsonPropertyName("issueNumber")]
    public string IssueNumber { get; init; } = "";

    [JsonPropertyName("stage")]
    public string Stage { get; init; } = "";
}

public sealed class VetisQuenchReport
{
    [JsonPropertyName("quenched")]
    public int Quenched { get; set; }

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; }

    [JsonPropertyName("errors")]
    public List<VetisQuenchError> Errors { get; set; } = [];

    [JsonPropertyName("document")]
    public VetisIncomingView? Document { get; set; }
}

public sealed class VetisIncomingKey
{
    public string OrganizationId { get; init; } = "";

    public string EnterpriseGuid { get; init; } = "";
}
