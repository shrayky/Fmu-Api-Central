using System.Text.Json;
using Application.Dadata;
using Application.Tests.Fakes;
using Domain.Dadata;

namespace Application.Tests;

public class DadataAddressReaderTests
{
    [Fact]
    public void Houses_несколько_строк_с_одним_ФИАС_дают_один_дом_и_полный_адрес_уровня_дома()
    {
        var rows = new[]
        {
            DadataTestData.Suggestion("f1", "Москва, Ленина, 1"),
            DadataTestData.Suggestion("f1", "Москва, Ленина, 1, кв. 5", "8")
        };

        var result = DadataAddressReader.Houses(rows);

        var house = Assert.Single(result.Value);
        Assert.Equal("f1", house.FiasId);
        Assert.Equal("Москва, Ленина, 1, кв. 5", house.Address);
    }

    [Fact]
    public void Houses_без_строки_уровня_дома_берут_первую()
    {
        var rows = new[]
        {
            DadataTestData.Suggestion("f1", "Москва, Ленина, 1", "7"),
            DadataTestData.Suggestion("f1", "Москва, Ленина, 1, стр. 2", "6")
        };

        var house = Assert.Single(DadataAddressReader.Houses(rows).Value);

        Assert.Equal("Москва, Ленина, 1", house.Address);
    }

    [Fact]
    public void Houses_несколько_строк_уровня_дома_берут_первую()
    {
        var rows = new[]
        {
            DadataTestData.Suggestion("f1", "Москва, Ленина, 1", "8"),
            DadataTestData.Suggestion("f1", "Москва, Ленина, 1, кв. 5", "8")
        };

        var house = Assert.Single(DadataAddressReader.Houses(rows).Value);

        Assert.Equal("Москва, Ленина, 1", house.Address);
    }

    [Fact]
    public void Houses_два_разных_ФИАС_дают_две_строки()
    {
        var rows = new[]
        {
            DadataTestData.Suggestion("f1", "Москва, Ленина, 1", "8"),
            DadataTestData.Suggestion("f2", "Москва, Ленина, 3", "8")
        };

        var houses = DadataAddressReader.Houses(rows).Value;

        Assert.Equal(2, houses.Count);
        Assert.Equal(new[] { "f1", "f2" }, houses.Select(house => house.FiasId));
    }

    [Fact]
    public void Houses_строка_без_ФИАС_в_список_не_входит()
    {
        var rows = new[]
        {
            DadataTestData.Suggestion("", "Москва", "8"),
            DadataTestData.Suggestion("f1", "Москва, Ленина, 1", "8")
        };

        var houses = DadataAddressReader.Houses(rows).Value;

        Assert.Equal("f1", Assert.Single(houses).FiasId);
    }

    [Fact]
    public void Houses_без_домов_отказывают()
    {
        var result = DadataAddressReader.Houses([]);

        Assert.True(result.IsFailure);
        Assert.Equal("Дом по адресу не найден", result.Error);
    }

    [Fact]
    public void Houses_адрес_длиннее_250_символов_отказывает()
    {
        var longAddress = new string('а', 251);
        var result = DadataAddressReader.Houses([DadataTestData.Suggestion("f1", longAddress, "8")]);

        Assert.True(result.IsFailure);
        Assert.Equal("Адрес DaData длиннее 250 символов", result.Error);
    }

    [Fact]
    public void Houses_длинный_дом_не_прячет_короткий()
    {
        var rows = new[]
        {
            DadataTestData.Suggestion("f1", new string('а', 251), "8"),
            DadataTestData.Suggestion("f2", "Москва, Ленина, 1", "8")
        };

        var house = Assert.Single(DadataAddressReader.Houses(rows).Value);

        Assert.Equal("f2", house.FiasId);
        Assert.Equal("Москва, Ленина, 1", house.Address);
    }

    [Fact]
    public void Houses_пустой_полный_адрес_оставляет_дом()
    {
        var house = Assert.Single(DadataAddressReader.Houses([
            DadataTestData.Suggestion("f1", "", "8")
        ]).Value);

        Assert.Equal("f1", house.FiasId);
        Assert.Equal("", house.Address);
    }

    [Fact]
    public void Houses_читают_имена_полей_ответа_подсказок()
    {
        const string json = """
            {
              "suggestions": [
                {
                  "value": "г Москва, ул Ленина, д 1",
                  "unrestricted_value": "101000, г Москва, ул Ленина, д 1",
                  "data": { "house_fias_id": "f1", "fias_level": "8" }
                }
              ]
            }
            """;

        var response = JsonSerializer.Deserialize<DadataSuggestionResponse>(json);

        var house = Assert.Single(DadataAddressReader.Houses(response?.Suggestions).Value);
        Assert.Equal("f1", house.FiasId);
        Assert.Equal("101000, г Москва, ул Ленина, д 1", house.Address);
    }

    [Fact]
    public void ConfidentFias_уверенный_дом_отдаёт_ФИАС()
    {
        var results = new[]
        {
            new DadataCleanResult { HouseFiasId = " f1 ", FiasLevel = "8", Qc = 0 }
        };

        Assert.Equal("f1", DadataAddressReader.ConfidentFias(results));
    }

    [Theory]
    [InlineData(3, "f1")]
    [InlineData(2, "f1")]
    [InlineData(0, "")]
    public void ConfidentFias_неуверенный_разбор_или_пустой_дом_результата_не_дают(int qc, string houseFiasId)
    {
        var results = new[] { new DadataCleanResult { HouseFiasId = houseFiasId, Qc = qc } };

        Assert.Equal("", DadataAddressReader.ConfidentFias(results));
    }

    [Fact]
    public void ConfidentFias_читает_имена_полей_ответа_стандартизации()
    {
        const string json = """
            [
              {
                "source": "москва ленина 1",
                "result": "г Москва, ул Ленина, д 1",
                "house_fias_id": "f1",
                "fias_level": "8",
                "qc": 0
              }
            ]
            """;

        var results = JsonSerializer.Deserialize<List<DadataCleanResult>>(json);

        Assert.Equal("f1", DadataAddressReader.ConfidentFias(results));
    }

    [Fact]
    public void ConfidentFias_без_кода_качества_результата_не_даёт()
    {
        const string json = """
            [
              {
                "house_fias_id": "f1",
                "result": "г Москва, ул Ленина, д 1"
              }
            ]
            """;

        var results = JsonSerializer.Deserialize<List<DadataCleanResult>>(json);

        Assert.Equal("", DadataAddressReader.ConfidentFias(results));
    }
}
