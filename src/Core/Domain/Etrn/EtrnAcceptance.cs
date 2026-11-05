using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

namespace Domain.Etrn;

public static class EtrnAcceptanceIntent
{
    public const string Sign = "sign";
    public const string Reject = "reject";
}

public sealed class EtrnAcceptanceFact
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("factPlaces")]
    public int? FactPlaces { get; set; }

    [JsonPropertyName("factWeight")]
    public string FactWeight { get; set; } = "";

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";
}

public sealed class EtrnAcceptCommand
{
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = EtrnAcceptanceIntent.Sign;

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonPropertyName("lines")]
    public List<EtrnAcceptanceFact> Lines { get; set; } = [];
}

public sealed class EtrnDecidedLine
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("factPlaces")]
    public int? FactPlaces { get; set; }

    [JsonPropertyName("factWeight")]
    public string FactWeight { get; set; } = "";

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = "";

    [JsonPropertyName("differs")]
    public bool Differs { get; set; }
}

public sealed class EtrnDecision
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("generalComment")]
    public string GeneralComment { get; set; } = "";

    [JsonPropertyName("lines")]
    public List<EtrnDecidedLine> Lines { get; set; } = [];
}

public static class EtrnAcceptance
{
    public const string RejectNeedsComment = "Для отказа нужен общий комментарий";
    public const string DifferenceNeedsComment = "При расхождении факта нужен общий комментарий";
    public const string NegativeFact = "Факт не может быть меньше нуля";
    public const string FactAboveInvoice = "Факт не может быть больше значения в накладной";
    public const string InvalidWeight = "Факт массы должен быть числом";
    public const string NoLines = "В документе нет строк груза";

    public static Result<EtrnDecision> Decide(
        IReadOnlyList<EtrnCargoLine> lines,
        IReadOnlyList<EtrnAcceptanceFact> facts,
        string? generalComment,
        string? intent)
    {
        var comment = (generalComment ?? "").Trim();
        if (intent == EtrnAcceptanceIntent.Reject)
            return Reject(lines, comment);
        if (intent != EtrnAcceptanceIntent.Sign)
            return Result.Failure<EtrnDecision>("Неизвестное действие.");
        if (lines == null || lines.Count == 0)
            return Result.Failure<EtrnDecision>(NoLines);
        if (lines.Any(line => !line.HasQuantity))
            return Result.Failure<EtrnDecision>(EtrnDocumentStatus.NoQuantityError);

        var given = (facts ?? [])
            .GroupBy(fact => fact.Index)
            .ToDictionary(group => group.Key, group => group.Last());
        var decided = new List<EtrnDecidedLine>(lines.Count);

        foreach (var line in lines)
        {
            given.TryGetValue(line.Index, out var fact);
            fact ??= new EtrnAcceptanceFact { Index = line.Index };

            int? factPlaces = null;
            if (line.Places is int places)
            {
                if (fact.FactPlaces is int actual)
                {
                    if (actual < 0)
                        return Result.Failure<EtrnDecision>(NegativeFact);
                    if (actual > places)
                        return Result.Failure<EtrnDecision>(FactAboveInvoice);
                    factPlaces = actual;
                }
            }

            var factWeight = "";
            if (line.HasWeight)
            {
                var raw = (fact.FactWeight ?? "").Trim().Replace(',', '.');
                if (raw.Length > 0)
                {
                    if (!EtrnQuantity.TryWeight(raw, out var actual))
                        return Result.Failure<EtrnDecision>(InvalidWeight);
                    if (actual < 0)
                        return Result.Failure<EtrnDecision>(NegativeFact);
                    if (!EtrnQuantity.TryWeight(line.GrossWeight, out var invoice) || actual > invoice)
                        return Result.Failure<EtrnDecision>(FactAboveInvoice);
                    factWeight = raw;
                }
            }

            decided.Add(new EtrnDecidedLine
            {
                Index = line.Index,
                FactPlaces = factPlaces,
                FactWeight = factWeight,
                Comment = (fact.Comment ?? "").Trim(),
                Differs = Differs(line, factPlaces, factWeight)
            });
        }

        if (decided.All(line => !line.Differs))
        {
            // При полном совпадении комментарии в титул не пишутся.
            foreach (var line in decided)
                line.Comment = "";
            return Result.Success(new EtrnDecision
            {
                Action = EtrnAcceptanceAction.Received,
                Status = EtrnDocumentStatus.Accepted,
                Lines = decided
            });
        }

        if (comment.Length == 0)
            return Result.Failure<EtrnDecision>(DifferenceNeedsComment);

        foreach (var line in decided)
        {
            if (line.Comment.Length == 0)
                line.Comment = comment;
        }

        return Result.Success(new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Partially,
            Status = EtrnDocumentStatus.Accepted,
            GeneralComment = comment,
            Lines = decided
        });
    }

    private static Result<EtrnDecision> Reject(IReadOnlyList<EtrnCargoLine> lines, string comment)
    {
        if (comment.Length == 0)
            return Result.Failure<EtrnDecision>(RejectNeedsComment);

        return Result.Success(new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Rejected,
            Status = EtrnDocumentStatus.Rejected,
            GeneralComment = comment,
            Lines = (lines ?? []).Select(line => new EtrnDecidedLine { Index = line.Index }).ToList()
        });
    }

    private static bool Differs(EtrnCargoLine line, int? factPlaces, string factWeight) =>
        (line.Places is int places && factPlaces is int actualPlaces && actualPlaces != places)
        || (line.HasWeight && factWeight.Length > 0 && !EtrnQuantity.SameWeight(line.GrossWeight, factWeight));
}
