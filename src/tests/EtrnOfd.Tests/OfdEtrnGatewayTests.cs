using System.Net;
using System.Text;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Domain.Etrn.Xml;
using EtrnOfd;
using Microsoft.Extensions.Logging;

namespace EtrnOfd.Tests;

public class OfdEtrnGatewayTests
{
    private const string TitleXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Файл ВерсФорм="5.01">
          <Документ КНД="1110339">
            <СодИнфГО>
              <СвГруз><ОпГруз НомерГруз="1" НаимГруз="Ящики" КолМестГр="2" ВесГруз="10"/></СвГруз>
            </СодИнфГО>
          </Документ>
        </Файл>
        """;

    [Fact]
    public async Task Опрос_берёт_только_груз_в_пути_и_не_пишет_токен_в_журнал()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json("""
                    [
                      {"id":308924,"uid":"uid-card","status":"CARGO_ON_WAY","number":"API-20","destAddress":{"postCode":"352570","regionCode":"23","district":"Мостовский р-н","settlement":"пгт Мостовской","street":"ул Николая Свистунова","building":"3"}},
                      {"id":400,"status":"CARGO_DELIVERED","number":"закрыта"}
                    ]
                    """);
            if (call.Url.EndsWith("/etrns/308924/documents", StringComparison.Ordinal))
                return OfdHandler.Json("""[{"id":1569904,"knd":"1110339","formVersion":"5.01"}]""");
            if (call.Url.EndsWith("/documents/1569904/content", StringComparison.Ordinal))
                return OfdHandler.Xml(TitleXml);
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(world.Context.Position, result.Value.Position);
        Assert.Equal(["400"], result.Value.ClosedDocumentIds);
        var document = Assert.Single(result.Value.Documents);
        Assert.Equal("308924", document.OperatorDocumentId);
        Assert.Equal("API-20", document.Number);
        Assert.Equal("uid-card", document.Uid);
        Assert.Equal("352570, 23, Мостовский р-н, пгт Мостовской, ул Николая Свистунова, 3", document.DeliveryAddress);
        Assert.Equal(2, Assert.Single(document.Lines).Places);
        var list = Assert.Single(world.Handler.Calls, call => call.Url.Contains("/etrns?", StringComparison.Ordinal));
        Assert.Contains("subscriber-id=2775225", list.Url, StringComparison.Ordinal);
        Assert.Contains("number=-30", list.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("from-id=", list.Url, StringComparison.Ordinal);
        Assert.Equal("Bearer token-1", list.Authorization);
        Assert.DoesNotContain(world.Handler.Calls, call => call.Url.Contains("/etrns/400", StringComparison.Ordinal));
        Assert.Equal("application/xml", world.Handler.Calls.Single(call => call.Url.Contains("/content", StringComparison.Ordinal)).Accept);
        Assert.DoesNotContain(result.Value.Exchanges, exchange =>
            exchange.RequestBody.Contains("token-1", StringComparison.Ordinal)
            || exchange.ResponseBody.Contains("token-1", StringComparison.Ordinal)
            || exchange.RequestBody.Contains("plain-key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Список_в_поле_content_тоже_читается()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json("""{"content":[{"id":7,"status":"CARGO_RECEIVED"}]}""");
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(["7"], result.Value.ClosedDocumentIds);
        Assert.Empty(result.Value.Documents);
    }

    [Fact]
    public async Task Чужой_корень_списка_не_сдвигает_пару_Saby()
    {
        var world = World.Ready();
        world.Handler.Respond = call => call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal)
            ? OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""")
            : OfdHandler.Json("""{"items":[]}""");

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal("Платформа ЭТрН вернула не список ЭТрН", result.Value.Error);
        Assert.Equal(world.Context.Position, result.Value.Position);
        Assert.Empty(result.Value.ClosedDocumentIds);
    }

    [Fact]
    public async Task Обрыв_второго_документа_оставляет_первый_и_не_закрывает_хвост()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json("""
                    [
                      {"id":1,"status":"CARGO_ON_WAY","number":"первый"},
                      {"id":2,"status":"CARGO_ON_WAY","number":"второй"}
                    ]
                    """);
            if (call.Url.EndsWith("/etrns/1/documents", StringComparison.Ordinal))
                return OfdHandler.Json("""[{"id":11,"knd":"1110339"}]""");
            if (call.Url.EndsWith("/documents/11/content", StringComparison.Ordinal))
                return OfdHandler.Xml(TitleXml);
            if (call.Url.EndsWith("/etrns/2/documents", StringComparison.Ordinal))
                return OfdHandler.Status(HttpStatusCode.InternalServerError);
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal("1", Assert.Single(result.Value.Documents).OperatorDocumentId);
        Assert.DoesNotContain("2", result.Value.ClosedDocumentIds);
        Assert.Equal(world.Context.Position, result.Value.Position);
    }

    [Fact]
    public async Task Без_абонента_и_сертификата_токен_не_запрашивается()
    {
        var world = World.Ready();
        world.Context = Context(subscriberId: "", apiKey: "plain-key");
        var noSubscriber = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);
        world.Context = Context(subscriberId: "2775225", apiKey: "plain-key", certificate: "");
        var noCertificate = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.Equal("Не указан абонент ЭДО", noSubscriber.Value.Error);
        Assert.Equal("Не задан сертификат организации", noCertificate.Value.Error);
        world.Context = Context(subscriberId: "2775225", apiKey: "plain-key");
        world.Context.Connection.ApiUrl = "";
        var noAddress = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.Equal("Не задан адрес API Платформы ЭТрН", noAddress.Value.Error);
        Assert.Empty(world.Handler.Calls);
    }

    [Fact]
    public async Task Приёмка_кладёт_XML_и_затем_байты_подписи()
    {
        var world = World.Ready();
        var signature = new byte[] { 1, 2, 3 };
        var stored = Encoding.UTF8.GetBytes("<Файл/>");
        var listReads = 0;
        world.Signer.Signature = Convert.ToBase64String(signature);
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Method == "PUT" && call.Url.EndsWith("/documents/1110341v5.01", StringComparison.Ordinal))
                return OfdHandler.Json("""{"status":"ok"}""");
            if (call.Method == "GET" && call.Url.EndsWith("/etrns/308924/documents", StringComparison.Ordinal))
                return ++listReads == 1
                    ? OfdHandler.Json("[]")
                    : OfdHandler.Json("""[{"id":77,"knd":"1110341v5.01","formVersion":"5.01"}]""");
            if (call.Method == "GET" && call.Url.EndsWith("/documents/77/content", StringComparison.Ordinal))
                return OfdHandler.XBytes(stored);
            if (call.Method == "PUT" && call.Url.EndsWith("/documents/1110341v5.01/signature", StringComparison.Ordinal))
                return OfdHandler.Json("""{"status":"ok"}""");
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };
        var document = new EtrnIncomingDocumentEntity
        {
            OperatorDocumentId = "308924",
            Number = "ТН-77",
            Date = "2026-11-04",
            Uid = "uid-1",
            Lines = [new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" }]
        };
        var decision = new EtrnDecision { Action = EtrnAcceptanceAction.Received };

        var result = await world.Gateway.ExecuteAsync(
            world.Context,
            new EtrnExecuteCommand { Document = document, Decision = decision },
            CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        var puts = world.Handler.Calls.Where(call => call.Method == "PUT").ToList();
        Assert.Equal(2, puts.Count);
        Assert.EndsWith("/etrns/308924/documents/1110341v5.01", puts[0].Url, StringComparison.Ordinal);
        Assert.StartsWith("application/xml", puts[0].ContentType, StringComparison.Ordinal);
        var xml = ConsigneeTitleWriter.ToWindows1251(
            ConsigneeTitleWriter.Build(document, decision, World.SignedAt, world.Names.Value.Value));
        Assert.Equal(xml, puts[0].Body);
        Assert.Equal(stored, world.Signer.Last.Content);
        Assert.NotEqual(xml, world.Signer.Last.Content);
        Assert.Equal("", world.Signer.Last.Hash);
        Assert.Equal("", world.Signer.Last.FileUrl);
        Assert.Equal(2, world.Handler.Calls.Count(call =>
            call.Url.EndsWith("/etrns/308924/documents", StringComparison.Ordinal)));
        Assert.Equal("application/xml", world.Handler.Calls.Single(call =>
            call.Url.EndsWith("/documents/77/content", StringComparison.Ordinal)).Accept);
        Assert.EndsWith("/documents/1110341v5.01/signature", puts[1].Url, StringComparison.Ordinal);
        Assert.StartsWith("application/octet-stream", puts[1].ContentType, StringComparison.Ordinal);
        Assert.Equal(signature, puts[1].Body);
        Assert.DoesNotContain(result.Value.Exchanges, exchange =>
            exchange.RequestBody.Contains("token-1", StringComparison.Ordinal)
            || exchange.ResponseBody.Contains("openid-connect", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Приёмка_подписывает_новый_титул_а_не_старый()
    {
        var world = World.Ready();
        var signature = new byte[] { 4, 5, 6 };
        var oldContent = Encoding.UTF8.GetBytes("<Старый/>");
        var newContent = Encoding.UTF8.GetBytes("<Новый/>");
        var listReads = 0;
        world.Signer.Signature = Convert.ToBase64String(signature);
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Method == "PUT" && call.Url.EndsWith("/documents/1110341v5.01", StringComparison.Ordinal))
                return OfdHandler.Json("""{"status":"ok"}""");
            if (call.Method == "GET" && call.Url.EndsWith("/etrns/308924/documents", StringComparison.Ordinal))
            {
                listReads++;
                return listReads switch
                {
                    1 => OfdHandler.Json("""[{"id":70,"knd":"1110341v5.01"}]"""),
                    2 => OfdHandler.Json("""[{"id":70,"knd":"1110341v5.01"}]"""),
                    _ => OfdHandler.Json("""[{"id":70,"knd":"1110341v5.01"},{"id":71,"knd":"1110341v5.01"}]""")
                };
            }
            if (call.Method == "GET" && call.Url.EndsWith("/documents/70/content", StringComparison.Ordinal))
                return OfdHandler.XBytes(oldContent);
            if (call.Method == "GET" && call.Url.EndsWith("/documents/71/content", StringComparison.Ordinal))
                return OfdHandler.XBytes(newContent);
            if (call.Method == "PUT" && call.Url.EndsWith("/documents/1110341v5.01/signature", StringComparison.Ordinal))
                return OfdHandler.Json("""{"status":"ok"}""");
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };
        var document = new EtrnIncomingDocumentEntity
        {
            OperatorDocumentId = "308924",
            Number = "ТН-77",
            Date = "2026-11-04",
            Lines = [new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" }]
        };

        var result = await world.Gateway.ExecuteAsync(
            world.Context,
            new EtrnExecuteCommand
            {
                Document = document,
                Decision = new EtrnDecision { Action = EtrnAcceptanceAction.Received }
            },
            CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(newContent, world.Signer.Last.Content);
        Assert.NotEqual(oldContent, world.Signer.Last.Content);
        Assert.Equal(3, listReads);
        Assert.DoesNotContain(world.Handler.Calls, call =>
            call.Url.EndsWith("/documents/70/content", StringComparison.Ordinal));
        Assert.Equal(signature, world.Handler.Calls.Last(call => call.Method == "PUT").Body);
    }

    [Fact]
    public async Task Приёмка_не_подписывает_пустой_титул()
    {
        var world = World.Ready();
        world.Signer.Signature = Convert.ToBase64String(new byte[] { 7, 8, 9 });
        var listReads = 0;
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Method == "PUT" && call.Url.EndsWith("/documents/1110341v5.01", StringComparison.Ordinal))
                return OfdHandler.Json("""{"status":"ok"}""");
            if (call.Method == "GET" && call.Url.EndsWith("/etrns/308924/documents", StringComparison.Ordinal))
            {
                listReads++;
                return listReads == 1
                    ? OfdHandler.Json("[]")
                    : OfdHandler.Json("""[{"id":71,"knd":"1110341v5.01"}]""");
            }
            if (call.Method == "GET" && call.Url.EndsWith("/documents/71/content", StringComparison.Ordinal))
                return OfdHandler.XBytes([]);
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.ExecuteAsync(
            world.Context,
            new EtrnExecuteCommand
            {
                Document = new EtrnIncomingDocumentEntity
                {
                    OperatorDocumentId = "308924",
                    Number = "ТН-77",
                    Date = "2026-11-04",
                    Lines = [new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" }]
                },
                Decision = new EtrnDecision { Action = EtrnAcceptanceAction.Received }
            },
            CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal("Платформа ЭТрН вернула пустой титул грузополучателя", result.Value.Error);
        Assert.Empty(world.Signer.Last.Content);
        Assert.DoesNotContain(world.Handler.Calls, call =>
            call.Method == "PUT" && call.Url.EndsWith("/signature", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Приёмка_при_400_пишет_тело_ответа_в_обмен_и_лог()
    {
        var world = World.Ready();
        const string body = """{"message":"Некорректный титул"}""";
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Method == "GET" && call.Url.EndsWith("/etrns/308924/documents", StringComparison.Ordinal))
                return OfdHandler.Json("[]");
            if (call.Method == "PUT" && call.Url.EndsWith("/documents/1110341v5.01", StringComparison.Ordinal))
                return OfdHandler.Json(HttpStatusCode.BadRequest, body);
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.ExecuteAsync(
            world.Context,
            new EtrnExecuteCommand
            {
                Document = new EtrnIncomingDocumentEntity
                {
                    OperatorDocumentId = "308924",
                    Number = "ТН-77",
                    Date = "2026-11-04",
                    Lines = [new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" }]
                },
                Decision = new EtrnDecision { Action = EtrnAcceptanceAction.Received }
            },
            CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal("Платформа ЭТрН ответила 400", result.Value.Error);
        var title = Assert.Single(result.Value.Exchanges, item => item.Method == EtrnMethods.OfdConsigneeTitle);
        Assert.Contains(EtrnMethods.OfdConsigneeTitle, EtrnMethods.Service);
        Assert.Equal("400", title.Error);
        Assert.Equal(body, title.ResponseBody);
        Assert.Contains(world.Logger.Warnings, message =>
            message.Contains("400", StringComparison.Ordinal)
            && message.Contains("Некорректный титул", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Исправление_уходит_в_титул_с_номером_и_датой()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Method == "GET" && call.Url.EndsWith("/etrns/308924/documents", StringComparison.Ordinal))
                return OfdHandler.Json("[]");
            if (call.Method == "PUT" && call.Url.EndsWith("/documents/1110341v5.01", StringComparison.Ordinal))
                return OfdHandler.Json(HttpStatusCode.BadRequest, "{}");
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        await world.Gateway.ExecuteAsync(
            world.Context,
            new EtrnExecuteCommand
            {
                Document = new EtrnIncomingDocumentEntity
                {
                    OperatorDocumentId = "308924",
                    Number = "ТН-77",
                    Date = "2026-11-04",
                    Lines = [new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" }]
                },
                Decision = new EtrnDecision { Action = EtrnAcceptanceAction.Received },
                Correction = new EtrnCorrection { Number = 1, Date = "20.11.2026" }
            },
            CancellationToken.None);

        var title = world.Handler.Calls.Single(call =>
            call.Method == "PUT" && call.Url.EndsWith("/documents/1110341v5.01", StringComparison.Ordinal));
        var xml = Encoding.GetEncoding("windows-1251").GetString(title.Body);
        Assert.Contains("НомИспр=\"1\"", xml, StringComparison.Ordinal);
        Assert.Contains("ДатаИспр=\"20.11.2026\"", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Приёмка_без_ФИО_сертификата_не_отправляет_титул()
    {
        var world = World.Ready();
        world.Names.Value = Result.Failure<EtrnSignerName>("В сертификате нет фамилии и имени подписанта");

        var result = await world.Gateway.ExecuteAsync(
            world.Context,
            new EtrnExecuteCommand
            {
                Document = new EtrnIncomingDocumentEntity
                {
                    OperatorDocumentId = "308924",
                    Number = "ТН-77",
                    Date = "2026-11-04",
                    Lines = [new EtrnCargoLine { Index = 1, Places = 10, GrossWeight = "100" }]
                },
                Decision = new EtrnDecision { Action = EtrnAcceptanceAction.Received }
            },
            CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal("В сертификате нет фамилии и имени подписанта", result.Value.Error);
        Assert.Empty(world.Handler.Calls);
    }

    [Fact]
    public async Task Опрос_листает_страницу_назад_по_минимальному_id()
    {
        var world = World.Ready();
        var first = string.Join(',', Enumerable.Range(0, 30)
            .Select(i => $$"""{"id":{{1000 + i}},"status":"CARGO_DELIVERED","number":"с"}"""));
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal) && !call.Url.Contains("from-id=", StringComparison.Ordinal))
                return OfdHandler.Json("[" + first + "]");
            if (call.Url.Contains("from-id=1000", StringComparison.Ordinal))
                return OfdHandler.Json("""[{"id":999,"status":"CARGO_ON_WAY","number":"живая"}]""");
            if (call.Url.EndsWith("/etrns/999/documents", StringComparison.Ordinal))
                return OfdHandler.Json("""[{"id":15,"knd":"1110339","formVersion":"5.01"}]""");
            if (call.Url.EndsWith("/documents/15/content", StringComparison.Ordinal))
                return OfdHandler.Xml(TitleXml);
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal("999", Assert.Single(result.Value.Documents).OperatorDocumentId);
        Assert.Equal(30, result.Value.ClosedDocumentIds.Count);
        Assert.Contains("1000", result.Value.ClosedDocumentIds);
        var pages = world.Handler.Calls.Where(call => call.Url.Contains("/etrns?", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, pages.Count);
        Assert.Contains("number=-30", pages[1].Url, StringComparison.Ordinal);
        Assert.Contains("from-id=1000", pages[1].Url, StringComparison.Ordinal);
        Assert.Equal(world.Context.Position, result.Value.Position);
    }

    [Fact]
    public async Task Опрос_останавливается_если_набор_id_повторился()
    {
        var world = World.Ready();
        var page = "[" + string.Join(',', Enumerable.Range(0, 30)
            .Select(i => $$"""{"id":{{1000 + i}},"status":"CARGO_DELIVERED"}""")) + "]";
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json(page);
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(30, result.Value.ClosedDocumentIds.Count);
        Assert.Equal(2, world.Handler.Calls.Count(call => call.Url.Contains("/etrns?", StringComparison.Ordinal)));
        Assert.Equal(world.Context.Position, result.Value.Position);
    }

    [Fact]
    public async Task Опрос_останавливается_если_id_не_уменьшился()
    {
        var world = World.Ready();
        var first = Page(1000);
        var second = Page(1030);
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json(call.Url.Contains("from-id=", StringComparison.Ordinal) ? second : first);
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(30, result.Value.ClosedDocumentIds.Count);
        Assert.Equal(2, world.Handler.Calls.Count(call => call.Url.Contains("/etrns?", StringComparison.Ordinal)));
        Assert.Equal(world.Context.Position, result.Value.Position);
    }

    [Fact]
    public async Task Опрос_на_101_странице_не_считает_список_законченным()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            if (!call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Status(HttpStatusCode.NotFound);

            var from = call.Url.Contains("from-id=", StringComparison.Ordinal)
                ? long.Parse(call.Url[(call.Url.IndexOf("from-id=", StringComparison.Ordinal) + 8)..])
                : 10_000;
            var cards = Enumerable.Range(0, 30)
                .Select(i => $$"""{"id":{{from - 1 - i}},"status":"CARGO_DELIVERED"}""");
            return OfdHandler.Json("[" + string.Join(',', cards) + "]");
        };

        var result = await world.Gateway.FetchAsync(world.Context, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal("Платформа ЭТрН не отдала конец списка", result.Value.Error);
        Assert.Equal(world.Context.Position, result.Value.Position);
        Assert.Equal(101, world.Handler.Calls.Count(call => call.Url.Contains("/etrns?", StringComparison.Ordinal)));
        Assert.Equal(3030, result.Value.ClosedDocumentIds.Count);
    }

    [Fact]
    public async Task Сверка_считает_приёмкой_только_груз_в_пути()
    {
        var world = World.Ready();
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"token-1","expires_in":900}""");
            return OfdHandler.Json(call.Url.EndsWith("/etrns/1", StringComparison.Ordinal)
                ? """{"id":1,"status":"CARGO_ON_WAY"}"""
                : """{"id":2,"status":"CARGO_RECEIVED"}""");
        };

        var waiting = await world.Gateway.ReadStageAsync(world.Context, "1", CancellationToken.None);
        var closed = await world.Gateway.ReadStageAsync(world.Context, "2", CancellationToken.None);

        Assert.True(waiting.Value.StillAcceptance);
        Assert.Equal("CARGO_ON_WAY", waiting.Value.Stage);
        Assert.False(closed.Value.StillAcceptance);
    }

    [Fact]
    public async Task Проверка_берёт_новый_токен_и_пустой_список_считает_успехом()
    {
        var world = World.Ready();
        world.Store.Remember(world.Context.Connection.Id, "cached-token", "refresh", 900, DateTimeOffset.Now);
        world.Context.Connection.ClientId = "central";
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"fresh-token","expires_in":900}""");
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json("[]");
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.ProbeAsync(world.Context, CancellationToken.None);

        Assert.True(result.Value.Ok, result.Value.Message);
        Assert.Equal("Платформа ЭТрН приняла подключение", result.Value.Message);
        var token = Assert.Single(world.Handler.Calls, call => call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal));
        Assert.Contains("grant_type=signature", Encoding.UTF8.GetString(token.Body), StringComparison.Ordinal);
        var list = Assert.Single(world.Handler.Calls, call => call.Url.Contains("/etrns?", StringComparison.Ordinal));
        Assert.Contains("subscriber-id=2775225", list.Url, StringComparison.Ordinal);
        Assert.Contains("number=-30", list.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("from-id=", list.Url, StringComparison.Ordinal);
        Assert.Equal("Bearer fresh-token", list.Authorization);
        Assert.DoesNotContain(world.Handler.Calls, call => call.Url.Contains("/documents", StringComparison.Ordinal));
        var exchange = Assert.Single(result.Value.Exchanges);
        Assert.Equal("Проверка подключения", exchange.Method);
        Assert.Equal("Токен получен, действует 900 с", exchange.RequestBody);
        Assert.Equal("[]", exchange.ResponseBody);
        Assert.DoesNotContain("fresh-token", exchange.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("plain-key", exchange.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Проверка_без_сертификата_не_ходит_в_площадку()
    {
        var world = World.Ready();
        world.Context = Context("2775225", "plain-key", certificate: "");

        var result = await world.Gateway.ProbeAsync(world.Context, CancellationToken.None);

        Assert.False(result.Value.Ok);
        Assert.Equal("Не задан сертификат организации", result.Value.Message);
        Assert.Empty(world.Handler.Calls);
    }

    [Fact]
    public async Task Отказ_списка_при_проверке_не_читает_титулы()
    {
        var world = World.Ready();
        world.Context.Connection.ClientId = "central";
        world.Handler.Respond = call => call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal)
            ? OfdHandler.Json("""{"access_token":"fresh-token","expires_in":900}""")
            : OfdHandler.Status(HttpStatusCode.Unauthorized);

        var result = await world.Gateway.ProbeAsync(world.Context, CancellationToken.None);

        Assert.False(result.Value.Ok);
        Assert.Equal("Платформа ЭТрН ответила 401", result.Value.Message);
        Assert.DoesNotContain(world.Handler.Calls, call => call.Url.Contains("/documents", StringComparison.Ordinal));
        Assert.Equal("Проверка подключения", Assert.Single(result.Value.Exchanges).Method);
    }

    [Fact]
    public async Task Проверка_по_адресу_ЭДО_берёт_числовой_id_абонента()
    {
        var world = World.Ready();
        const string gid = "2VO-246109570307-00000000000000000000000000000";
        world.Context = Context(gid, "plain-key");
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"fresh-token","expires_in":900}""");
            if (call.Url.Contains("/edm-subscriber/public/api/v3/subscribers?", StringComparison.Ordinal))
                return OfdHandler.Json("""
                    [
                      {"id":1,"gid":"2BE-other"},
                      {"id":9442394,"gid":"2VO-246109570307-00000000000000000000000000000"}
                    ]
                    """);
            if (call.Url.Contains("/etrns?", StringComparison.Ordinal))
                return OfdHandler.Json("[]");
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.ProbeAsync(world.Context, CancellationToken.None);

        Assert.True(result.Value.Ok, result.Value.Message);
        var lookup = Assert.Single(world.Handler.Calls, call => call.Url.Contains("/subscribers?", StringComparison.Ordinal));
        Assert.Contains("inn=7700000000", lookup.Url, StringComparison.Ordinal);
        Assert.Equal("Bearer fresh-token", lookup.Authorization);
        var list = Assert.Single(world.Handler.Calls, call => call.Url.Contains("/etrns?", StringComparison.Ordinal));
        Assert.Contains("subscriber-id=9442394", list.Url, StringComparison.Ordinal);
        var exchange = Assert.Single(result.Value.Exchanges, item => item.Method == "Абонент ЭДО");
        Assert.Equal(gid, exchange.Parameters);
        Assert.Contains("/edm-subscriber/public/api/v3/subscribers?inn=7700000000", exchange.RequestBody, StringComparison.Ordinal);
        Assert.Contains("9442394", exchange.ResponseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("fresh-token", exchange.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("fresh-token", exchange.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Проверка_без_совпадения_адреса_ЭДО_не_читает_список()
    {
        var world = World.Ready();
        world.Context = Context("2VO-missing", "plain-key");
        world.Handler.Respond = call =>
        {
            if (call.Url.Contains("/security/public/api/v3/oauth/token", StringComparison.Ordinal))
                return OfdHandler.Json("""{"access_token":"fresh-token","expires_in":900}""");
            if (call.Url.Contains("/subscribers?", StringComparison.Ordinal))
                return OfdHandler.Json("""[{"id":9442394,"gid":"2VO-other"}]""");
            return OfdHandler.Status(HttpStatusCode.NotFound);
        };

        var result = await world.Gateway.ProbeAsync(world.Context, CancellationToken.None);

        Assert.False(result.Value.Ok);
        Assert.Equal("Абонент ЭДО не найден", result.Value.Message);
        Assert.DoesNotContain(world.Handler.Calls, call => call.Url.Contains("/etrns?", StringComparison.Ordinal));
    }

    private static string Page(int start) =>
        "[" + string.Join(',', Enumerable.Range(0, 30)
            .Select(i => $$"""{"id":{{start + i}},"status":"CARGO_DELIVERED"}""")) + "]";

    private static EtrnOperatorContext Context(string subscriberId, string apiKey, string certificate = "serial") => new()
    {
        OrganizationId = "o1",
        Inn = "7700000000",
        CertificateNumber = certificate,
        CertificatePassword = "pwd",
        Position = new EtrnPollPosition("e-old", "d-old"),
        Connection = new EtrnConnection
        {
            Id = "c1",
            Type = EtrnOperatorType.Ofd,
            Name = "Платформа ЭТрН",
            Enable = true,
            SubscriberId = subscriberId,
            ApiKey = apiKey,
            KeycloakUrl = "https://oauth-test.platformaofd.ru",
            Realm = "playground",
            ApiUrl = "https://api.platformaofd.ru"
        }
    };

    private sealed class World
    {
        public static readonly DateTimeOffset SignedAt = new(2026, 11, 10, 13, 5, 0, TimeSpan.FromHours(7));

        public OfdHandler Handler { get; }
        public RecordingSigner Signer { get; }
        public OfdTokenStore Store { get; }
        public LineLogger Logger { get; }
        public SignerNames Names { get; }
        public OfdEtrnGateway Gateway { get; }
        public EtrnOperatorContext Context { get; set; } = OfdEtrnGatewayTests.Context("2775225", "plain-key");

        private World(
            OfdHandler handler,
            RecordingSigner signer,
            OfdTokenStore store,
            LineLogger logger,
            SignerNames names,
            OfdEtrnGateway gateway)
        {
            Handler = handler;
            Signer = signer;
            Store = store;
            Logger = logger;
            Names = names;
            Gateway = gateway;
        }

        public static World Ready()
        {
            var handler = new OfdHandler();
            var signer = new RecordingSigner();
            var store = new OfdTokenStore();
            var logger = new LineLogger();
            var names = new SignerNames();
            var tokens = new OfdTokenClient(
                new HttpClient(handler, disposeHandler: false),
                new FixedAttachedSignature(),
                store,
                () => SignedAt,
                () => "nonce-1");
            var gateway = new OfdEtrnGateway(
                new SingleClientFactory(handler),
                tokens,
                signer,
                () => SignedAt,
                _ => Task.CompletedTask,
                logger,
                _ => names.Value);
            return new World(handler, signer, store, logger, names, gateway);
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FixedAttachedSignature : IOfdAttachedSignature
    {
        public Task<Result<string>> SignAsync(
            string certificateNumber,
            string? password,
            byte[] content,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success("Q1NT"));
    }

    private sealed class SignerNames
    {
        public Result<EtrnSignerName> Value { get; set; } = Result.Success(new EtrnSignerName
        {
            Surname = "Большаков",
            Name = "Сергей",
            Patronymic = "Александрович"
        });
    }

    private sealed class LineLogger : ILogger<OfdEtrnGateway>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }

    private sealed class RecordingSigner : IEtrnSignaturePort
    {
        public string Signature { get; set; } = Convert.ToBase64String([9]);
        public EtrnSignatureRequest Last { get; private set; } = new();

        public Task<Result<string>> SignAsync(EtrnSignatureRequest request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(Result.Success(Signature));
        }
    }

    private sealed record OfdCall(string Method, string Url, byte[] Body, string ContentType, string? Authorization, string Accept);

    private sealed class OfdHandler : HttpMessageHandler
    {
        public List<OfdCall> Calls { get; } = [];
        public Func<OfdCall, HttpResponseMessage> Respond { get; set; } = _ => Status(HttpStatusCode.NotFound);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var call = new OfdCall(
                request.Method.Method,
                request.RequestUri?.ToString() ?? "",
                body,
                request.Content?.Headers.ContentType?.MediaType ?? "",
                request.Headers.Authorization?.ToString(),
                request.Headers.Accept.ToString());
            Calls.Add(call);
            return Respond(call);
        }

        public static HttpResponseMessage Json(string json) => Json(HttpStatusCode.OK, json);

        public static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        public static HttpResponseMessage Xml(string xml) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml")
        };

        public static HttpResponseMessage XBytes(byte[] bytes) => new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        };

        public static HttpResponseMessage Status(HttpStatusCode status) => new(status);
    }
}
