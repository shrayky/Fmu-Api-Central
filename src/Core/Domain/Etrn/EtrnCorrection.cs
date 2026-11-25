using System.Globalization;
using CSharpFunctionalExtensions;

namespace Domain.Etrn;

public sealed class EtrnCorrection
{
    public const string BadNumber = "Номер исправления: целое число от 1 до 999";

    public const string BadDate = "Дата исправления: дата в формате ДД.ММ.ГГГГ";

    public int Number { get; init; }

    public string Date { get; init; } = "";

    public static Result<EtrnCorrection> Read(int number, string? date)
    {
        if (number is < 1 or > 999)
            return Result.Failure<EtrnCorrection>(BadNumber);

        var text = (date ?? "").Trim();
        if (!DateOnly.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return Result.Failure<EtrnCorrection>(BadDate);

        return Result.Success(new EtrnCorrection { Number = number, Date = text });
    }
}
