using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Domain.Dadata;

namespace Application.Dadata;

/// <summary>
/// Дом из ответа DaData: ФИАС и текст, который пишется в адрес торговой точки.
/// </summary>
public sealed class DadataHouse
{
    [JsonPropertyName("fiasId")]
    public string FiasId { get; set; } = "";

    [JsonPropertyName("address")]
    public string Address { get; set; } = "";

    [JsonIgnore]
    public string FiasLevel { get; set; } = "";
}

/// <summary>
/// Разбор ответов DaData без HTTP. Подсказки дают дома, стандартизация — один уверенный ФИАС.
/// </summary>
public static class DadataAddressReader
{
    public const int MaxAddressLength = 250;

    public const string LongAddressError = DadataMessages.LongAddress;
    public const string HouseNotFoundError = DadataMessages.HouseNotFound;

    /// <summary>
    /// Одна строка на дом. Текст — строка уровня дома, а если её нет — первая строка с этим ФИАС.
    /// </summary>
    public static Result<IReadOnlyList<DadataHouse>> Houses(IEnumerable<DadataSuggestion>? suggestions)
    {
        var houses = new List<DadataHouse>();
        var byFias = new Dictionary<string, DadataHouse>(StringComparer.OrdinalIgnoreCase);

        foreach (var suggestion in suggestions ?? [])
        {
            var fiasId = (suggestion.Data?.HouseFiasId ?? "").Trim();
            if (fiasId.Length == 0)
                continue;

            var address = (suggestion.UnrestrictedValue ?? "").Trim();
            var level = (suggestion.Data?.FiasLevel ?? "").Trim();
            if (!byFias.TryGetValue(fiasId, out var house))
            {
                house = new DadataHouse { FiasId = fiasId, Address = address, FiasLevel = level };
                byFias[fiasId] = house;
                houses.Add(house);
                continue;
            }

            // fias_level 8 — строка дома. Пустой текст уже выбранный адрес не затирает.
            if (address.Length == 0)
                continue;

            if (house.Address.Length == 0 || (house.FiasLevel != "8" && level == "8"))
            {
                house.Address = address;
                house.FiasLevel = level;
            }
        }

        var fits = houses.Where(house => house.Address.Length <= MaxAddressLength).ToList();
        if (fits.Count > 0)
            return Result.Success<IReadOnlyList<DadataHouse>>(fits);

        var error = houses.Count > 0 ? LongAddressError : HouseNotFoundError;
        return Result.Failure<IReadOnlyList<DadataHouse>>(error);
    }

    /// <summary>
    /// Стандартизация отдаёт один адрес. Точку ищут только по уверенному дому: qc = 0 и непустой house_fias_id.
    /// </summary>
    public static string ConfidentFias(IEnumerable<DadataCleanResult>? results)
    {
        var first = (results ?? []).FirstOrDefault();
        if (first == null || first.Qc != 0)
            return "";

        return (first.HouseFiasId ?? "").Trim();
    }
}
