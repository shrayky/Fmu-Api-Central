using System.Text;
using Domain.Etrn;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtrnSaby.Tests;

public class EtrnSabyGatewayTests
{
    private static readonly EtrnOperatorContext ContextEmpty = Context(EtrnPollPosition.Empty);

    [Fact]
    public async Task Приёмка_идёт_в_порядке_вложение_подготовка_выполнение()
    {
        var world = new World();

        var result = await world.Gateway.ExecuteAsync(ContextEmpty, Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(
            [EtrnMethods.WriteEnclosure, EtrnMethods.PrepareAction, EtrnMethods.ExecuteAction],
            world.Handler.Calls.Select(call => call.Method));
        Assert.Equal("hash-1", Assert.Single(world.Signer.Requests).Hash);
        Assert.Equal("THUMB-1", world.Handler.Calls.Single(call => call.Method == EtrnMethods.PrepareAction).Param("Отпечаток"));
        Assert.Equal("SIGNED", world.Handler.Calls.Single(call => call.Method == EtrnMethods.ExecuteAction).Param("Подпись"));
    }

    [Fact]
    public async Task Ответ_только_со_ссылкой_подписывает_байты_по_ссылке()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.WriteEnclosure => SabyHandler.Result("true"),
            EtrnMethods.PrepareAction => SabyHandler.Result("""{"Ссылка":"https://files.saby.ru/1.bin"}"""),
            EtrnMethods.ExecuteAction => SabyHandler.Result("true"),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.ExecuteAsync(ContextEmpty, Command(), CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        var request = Assert.Single(world.Signer.Requests);
        Assert.Equal("", request.Hash);
        Assert.Equal("https://files.saby.ru/1.bin", request.FileUrl);
    }

    [Fact]
    public async Task Два_последовательных_опроса_делают_один_вход()
    {
        var world = new World();

        await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);
        await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.Equal(1, world.Auth.Logins);
    }

    [Fact]
    public async Task Два_параллельных_опроса_делают_один_вход()
    {
        var world = new World();

        await Task.WhenAll(
            world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None),
            world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None));

        Assert.Equal(1, world.Auth.Logins);
    }

    [Fact]
    public async Task СБИС_Выход_не_вызывается()
    {
        var world = new World();

        await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.DoesNotContain(world.Handler.Calls, call => call.Method.Contains("Выход", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Сессия_кончилась_один_повторный_вход_и_один_повтор_команды()
    {
        var world = new World();
        var listCalls = 0;
        world.Handler.Respond = (call, _) =>
            call.Method == EtrnMethods.ChangesList && ++listCalls == 1
                ? SabyHandler.Error("Сессия не найдена")
                : SabyHandler.Result("[]");

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(2, world.Auth.Logins);
        Assert.Equal(2, world.Handler.Calls.Count(call => call.Method == EtrnMethods.ChangesList));
    }

    [Fact]
    public async Task Второй_ответ_про_сессию_заканчивает_круг()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method == EtrnMethods.ChangesList
            ? SabyHandler.Error("Сессия истекла")
            : SabyHandler.Result("[]");

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal(2, world.Auth.Logins);
        Assert.Equal(2, world.Handler.Calls.Count(call => call.Method == EtrnMethods.ChangesList));
    }

    [Fact]
    public async Task Заголовок_сессии_есть_на_service_и_отсутствует_на_входе()
    {
        var world = new World();
        var rpc = world.Rpc;

        await rpc.SendAsync(SabyJsonRpc.AuthUrl, EtrnMethods.AuthByCertificate, new { }, null, "ua", CancellationToken.None);
        await rpc.SendAsync(SabyJsonRpc.ServiceUrl, EtrnMethods.ChangesList, new { }, "s-1", "ua", CancellationToken.None);

        Assert.Null(world.Handler.Calls[0].SessionHeader);
        Assert.Equal("s-1", world.Handler.Calls[1].SessionHeader);
    }

    [Fact]
    public async Task Тело_списка_изменений_содержит_пару_ИНН_и_тип()
    {
        var world = new World();
        var context = Context(new EtrnPollPosition("e1", "d1"));

        await world.Gateway.FetchAsync(context, CancellationToken.None);

        var call = world.Handler.Calls.Single(item => item.Method == EtrnMethods.ChangesList);
        Assert.Equal("e1", call.Param("ИдентификаторСобытия"));
        Assert.Equal("d1", call.Param("ИдентификаторДокумента"));
        Assert.Equal("7700000000", call.Param("ИНН"));
        Assert.Equal("ConsignmentNote", call.Param("Тип"));
        Assert.Equal("Нет", call.Param("ПолныйСертификатЭП"));
    }

    [Fact]
    public async Task Первый_запуск_передаёт_дату_минус_30_суток_без_пары()
    {
        var world = new World();

        await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        var call = world.Handler.Calls.Single(item => item.Method == EtrnMethods.ChangesList);
        Assert.Equal("", call.Param("ИдентификаторСобытия"));
        Assert.Equal("", call.Param("ИдентификаторДокумента"));
        Assert.NotEqual("", call.Param("ДатаВремяС"));
    }

    [Fact]
    public async Task Журнал_получает_команды_service_и_не_получает_вход()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.ChangesList => SabyHandler.Result("""
                [{"ИдентификаторСобытия":"e1","ИдентификаторДокумента":"d1","Этап":"Приемка груза"}]
                """),
            EtrnMethods.ReadDocument => ReadTitle(),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal([EtrnMethods.ChangesList, EtrnMethods.ReadDocument], result.Value.Exchanges.Select(item => item.Method));
        Assert.All(result.Value.Exchanges, exchange =>
        {
            Assert.Contains(exchange.Method, EtrnMethods.Service);
            Assert.NotEqual("", exchange.RequestBody);
            Assert.NotEqual("", exchange.ResponseBody);
        });
    }

    [Fact]
    public async Task Пустая_страница_пару_не_двигает()
    {
        var world = new World();
        var context = Context(new EtrnPollPosition("e1", "d1"));

        var result = await world.Gateway.FetchAsync(context, CancellationToken.None);

        Assert.True(result.Value.Completed);
        Assert.Equal(context.Position, result.Value.Position);
    }

    [Fact]
    public async Task Событие_мимо_приёмки_документ_не_читает()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method == EtrnMethods.ChangesList
            ? SabyHandler.Result("""
                [{"ИдентификаторСобытия":"e2","ИдентификаторДокумента":"d2","Этап":"Подписание"}]
                """)
            : SabyHandler.Result("true");

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.True(result.Value.Completed);
        Assert.Empty(result.Value.Documents);
        Assert.Equal(new EtrnPollPosition("e2", "d2"), result.Value.Position);
        Assert.Equal(["d2"], result.Value.ClosedDocumentIds);
        Assert.DoesNotContain(world.Handler.Calls, call => call.Method == EtrnMethods.ReadDocument);
    }

    [Fact]
    public async Task Непрочитанная_середина_оставляет_документы_до_неё()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.ChangesList => SabyHandler.Result("""
                [
                  {"ИдентификаторСобытия":"e1","ИдентификаторДокумента":"d1","Этап":"Приемка груза"},
                  {"ИдентификаторСобытия":"e2","ИдентификаторДокумента":"d2","Этап":"Приемка груза"}
                ]
                """),
            EtrnMethods.ReadDocument => call.Param("ИдентификаторДокумента") == "d1"
                ? ReadTitle()
                : SabyHandler.Error("Документ недоступен"),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Single(result.Value.Documents);
        Assert.Equal(new EtrnPollPosition("e2", "d2"), result.Value.Position);
        Assert.Equal("Документ недоступен", result.Value.Error);
    }

    [Fact]
    public async Task Чужой_корень_не_двигает_пару()
    {
        var world = new World();
        var context = Context(new EtrnPollPosition("e0", "d0"));
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.ChangesList => SabyHandler.Result("""
                [{"ИдентификаторСобытия":"e1","ИдентификаторДокумента":"d1","Этап":"Приемка груза"}]
                """),
            EtrnMethods.ReadDocument => SabyHandler.Result($$"""
                {"Этап":"Приемка груза","Вложения":[{"Имя":"ON_TRNACLGROT.xml","Данные":"{{Base64("<Документ КНД=\"1110339\"/>")}}"}]}
                """),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.FetchAsync(context, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal(context.Position, result.Value.Position);
        Assert.Equal("Титул грузоотправителя не разобран", result.Value.Error);
    }

    [Fact]
    public async Task Сверка_этапа_читает_документ_и_возвращает_этап()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method == EtrnMethods.ReadDocument
            ? SabyHandler.Result("""{"Этап":"Приемка груза"}""")
            : SabyHandler.Result("true");

        var result = await world.Gateway.ReadStageAsync(ContextEmpty, "d1", CancellationToken.None);

        Assert.True(result.Value.Completed);
        Assert.True(result.Value.StillAcceptance);
        Assert.Single(result.Value.Exchanges);
    }

    private static EtrnExecuteCommand Command() => new()
    {
        Document = new EtrnIncomingDocumentEntity
        {
            Number = "ТН-1",
            Date = "2026-11-04",
            Uid = "uid-1",
            OperatorDocumentId = "d1",
            Status = EtrnDocumentStatus.Sending,
            Lines = [new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" }]
        },
        Decision = new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Received,
            Status = EtrnDocumentStatus.Accepted
        }
    };

    private static EtrnOperatorContext Context(EtrnPollPosition position) => new()
    {
        OrganizationId = "o1",
        Inn = "7700000000",
        Connection = new EtrnConnection { Id = "c1", Type = EtrnOperatorType.Saby, Name = "СБИС" },
        CertificateNumber = "serial-1",
        CertificatePassword = "pwd",
        Position = position
    };

    private static string ReadTitle()
    {
        var xml = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "XmlSamples", "sender-simple.xml"));
        return SabyHandler.Result(
            $$"""
              {"Этап":"Приемка груза","Вложения":[{"Имя":"ON_TRNACLGROT_1.xml","Данные":"{{Convert.ToBase64String(xml)}}"}]}
              """);
    }

    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private sealed class World
    {
        public SabyHandler Handler { get; } = new();

        public SabyJsonRpc Rpc { get; }

        public FakeSabyAuthenticator Auth { get; } = new();

        public FakeSignaturePort Signer { get; } = new();

        public SabySessionStore Sessions { get; } = new();

        public EtrnSabyGateway Gateway { get; }

        public World()
        {
            Handler.Respond = (call, _) => call.Method == EtrnMethods.PrepareAction
                ? SabyHandler.Result("""{"Хеш":"hash-1"}""")
                : SabyHandler.Result("true");
            Rpc = new SabyJsonRpc(new SabyHttpClientFactory(Handler), NullLogger<SabyJsonRpc>.Instance);
            Gateway = new EtrnSabyGateway(
                Rpc,
                Sessions,
                Auth,
                new FakeSabyCertificates(),
                Signer,
                NullLogger<EtrnSabyGateway>.Instance);
        }
    }
}
