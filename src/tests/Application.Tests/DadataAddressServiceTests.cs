using Application.Dadata;
using Application.Tests.Fakes;

namespace Application.Tests;

public class DadataAddressServiceTests
{
    [Fact]
    public async Task Suggest_пустой_адрес_не_доходит_до_DaData()
    {
        var client = new ScriptedDadataClient();
        var service = new DadataAddressService(client, Parameters());

        var result = await service.Suggest("  ", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Укажите адрес", result.Error);
        Assert.Equal(0, client.SuggestCalls);
    }

    [Fact]
    public async Task Suggest_выключенный_флаг_не_доходит_до_DaData()
    {
        var client = new ScriptedDadataClient();
        var service = new DadataAddressService(client, Parameters(enable: false));

        var result = await service.Suggest("Москва, Ленина, 1", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Интеграция с DaData выключена", result.Error);
        Assert.Equal(0, client.SuggestCalls);
    }

    [Fact]
    public async Task Suggest_пустой_API_ключ_не_доходит_до_DaData()
    {
        var client = new ScriptedDadataClient();
        var service = new DadataAddressService(client, Parameters(apiKey: " "));

        var result = await service.Suggest("Москва, Ленина, 1", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Укажите API-ключ DaData", result.Error);
        Assert.Equal(0, client.SuggestCalls);
    }

    [Fact]
    public async Task Suggest_отдаёт_дома_из_ответа()
    {
        var client = new ScriptedDadataClient();
        client.EnqueueSuggest(
            DadataTestData.Suggestion("f1", "Москва, Ленина, 1", "8"),
            DadataTestData.Suggestion("f2", "Москва, Ленина, 3", "8"));
        var service = new DadataAddressService(client, Parameters());

        var result = await service.Suggest(" Москва, Ленина ", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("Москва, Ленина", Assert.Single(client.SuggestRequests));
    }

    [Fact]
    public async Task Suggest_ошибку_DaData_отдаёт_её_текстом()
    {
        var client = new ScriptedDadataClient();
        client.EnqueueSuggestFailure("Лимит запросов исчерпан");
        var service = new DadataAddressService(client, Parameters());

        var result = await service.Suggest("Москва", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Лимит запросов исчерпан", result.Error);
    }

    [Fact]
    public async Task Clean_без_секрета_не_доходит_до_DaData()
    {
        var client = new ScriptedDadataClient();
        var service = new DadataAddressService(client, Parameters(secret: ""));

        var result = await service.Clean("Москва, Ленина, 1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Confident);
        Assert.Equal(0, client.CleanCalls);
    }

    [Fact]
    public async Task Clean_выключенный_флаг_не_доходит_до_DaData()
    {
        var client = new ScriptedDadataClient();
        var service = new DadataAddressService(client, Parameters(enable: false));

        var result = await service.Clean("Москва, Ленина, 1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Confident);
        Assert.Equal(0, client.CleanCalls);
    }

    [Fact]
    public async Task Clean_отдаёт_уверенный_ФИАС_и_канонический_адрес()
    {
        var client = new ScriptedDadataClient();
        client.EnqueueClean("f1", result: "г Москва, ул Ленина, д 1");
        var service = new DadataAddressService(client, Parameters());

        var result = await service.Clean("Москва, Ленина, 1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Confident);
        Assert.Equal("f1", result.Value.FiasId);
        Assert.Equal("г Москва, ул Ленина, д 1", result.Value.Address);
        Assert.Equal("Москва, Ленина, 1", Assert.Single(client.CleanRequests));
    }

    [Fact]
    public async Task Clean_неуверенный_разбор_не_даёт_ни_ФИАС_ни_строки()
    {
        var client = new ScriptedDadataClient();
        client.EnqueueClean("f1", qc: 3, result: "г Москва, ул Ленина, д 1");
        var service = new DadataAddressService(client, Parameters());

        var result = await service.Clean("Москва, Ленина, 1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Confident);
        Assert.Equal("", result.Value.FiasId);
        Assert.Equal("", result.Value.Address);
    }

    [Fact]
    public async Task Clean_ошибку_DaData_отдаёт_наружу()
    {
        var client = new ScriptedDadataClient();
        client.EnqueueCleanFailure("DaData недоступна");
        var service = new DadataAddressService(client, Parameters());

        var result = await service.Clean("Москва, Ленина, 1", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DaData недоступна", result.Error);
    }

    private static MemoryEtrnParameters Parameters(
        bool enable = true,
        string apiKey = "api-key",
        string secret = "secret")
    {
        var parameters = new MemoryEtrnParameters();
        parameters.Value.DadataSettings.Enable = enable;
        parameters.Value.DadataSettings.ApiKey = apiKey;
        parameters.Value.DadataSettings.Secret = secret;
        return parameters;
    }
}
