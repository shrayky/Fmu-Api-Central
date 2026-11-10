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
            [EtrnMethods.ReadDocument, EtrnMethods.WriteEnclosure, EtrnMethods.PrepareAction, EtrnMethods.ExecuteAction],
            world.Handler.Calls.Select(call => call.Method));
        Assert.Equal("hash-1", Assert.Single(world.Signer.Requests).Hash);
        var enclosure = world.Handler.Calls.Single(call => call.Method == EtrnMethods.WriteEnclosure);
        Assert.Equal("d1", enclosure.At("Документ", "Идентификатор"));
        Assert.Equal("", enclosure.At("Документ", "Этап", "Название"));
        Assert.Equal("ON_TRNACLGRPO.xml", enclosure.At("Документ", "Вложение", "Файл", "Имя"));
        Assert.NotEqual("", enclosure.At("Документ", "Вложение", "Файл", "ДвоичныеДанные"));
        var prepare = world.Handler.Calls.Single(call => call.Method == EtrnMethods.PrepareAction);
        Assert.Equal("Приемка груза", prepare.At("Документ", "Этап", "Название"));
        Assert.Equal("Получен", prepare.At("Документ", "Этап", "Действие", "Название"));
        Assert.Equal("THUMB-1", prepare.At("Документ", "Этап", "Действие", "Сертификат", "Отпечаток"));
        var execute = world.Handler.Calls.Single(call => call.Method == EtrnMethods.ExecuteAction);
        Assert.Equal("Получен", execute.At("Документ", "Этап", "Действие", "Название"));
        Assert.Equal("att-1", execute.At("Документ", "Этап", "Вложение", "Идентификатор"));
        Assert.Equal("SIGNED", execute.At("Документ", "Этап", "Вложение", "Подпись", "Файл", "ДвоичныеДанные"));
    }

    [Theory]
    [InlineData("7", "Выполнение завершено успешно")]
    [InlineData("9", "Выполнение завершено с проблемами")]
    public async Task Завершённый_документ_не_пишет_вложение(string code, string name)
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method == EtrnMethods.ReadDocument
            ? SabyHandler.Result($$$"""{"Состояние":{"Код":"{{{code}}}","Название":"{{{name}}}"}}""")
            : SabyHandler.Result("true");

        var result = await world.Gateway.ExecuteAsync(ContextEmpty, Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Completed);
        Assert.True(result.Value.CloseDocument);
        Assert.Equal(name, result.Value.Error);
        Assert.Equal([EtrnMethods.ReadDocument], world.Handler.Calls.Select(call => call.Method));
    }

    [Fact]
    public async Task Чужой_этап_не_пишет_вложение_и_не_закрывает_карточку()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method == EtrnMethods.ReadDocument
            ? SabyHandler.Result("""{"Состояние":{"Код":"10","Название":"В обработке"},"Этап":{"Название":"Получение груза"}}""")
            : SabyHandler.Result("true");

        var result = await world.Gateway.ExecuteAsync(ContextEmpty, Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Completed);
        Assert.False(result.Value.CloseDocument);
        Assert.Equal("Сейчас этап «Получение груза», приёмка недоступна", result.Value.Error);
        Assert.Equal([EtrnMethods.ReadDocument], world.Handler.Calls.Select(call => call.Method));
    }

    [Fact]
    public async Task Этап_приёмки_без_действий_не_пишет_вложение()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method == EtrnMethods.ReadDocument
            ? SabyHandler.Result("""
                {"Состояние":{"Код":"10","Название":"В обработке"},"Этап":[{"Название":"Приемка груза"}]}
                """)
            : SabyHandler.Result("true");

        var result = await world.Gateway.ExecuteAsync(ContextEmpty, Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Completed);
        Assert.False(result.Value.CloseDocument);
        Assert.Equal("Сейчас этап «Приемка груза», приёмка недоступна", result.Value.Error);
        Assert.Equal([EtrnMethods.ReadDocument], world.Handler.Calls.Select(call => call.Method));
    }

    [Fact]
    public async Task Этап_приёмки_пишет_вложение()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.ReadDocument => SabyHandler.Result("""
                {"Состояние":{"Код":"10","Название":"В обработке"},"Этап":[{"Название":"Приемка груза","Действие":[{"Название":"Получен"}]}]}
                """),
            EtrnMethods.PrepareAction => SabyHandler.Result("""
                {"Документ":{"Вложение":[{"Идентификатор":"att-1","Файл":{"Хеш":"hash-1"}}]}}
                """),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.ExecuteAsync(ContextEmpty, Command(), CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        Assert.Equal(
            [EtrnMethods.ReadDocument, EtrnMethods.WriteEnclosure, EtrnMethods.PrepareAction, EtrnMethods.ExecuteAction],
            world.Handler.Calls.Select(call => call.Method));
    }

    [Fact]
    public async Task Роуминг_возвращает_текст_Saby_и_не_закрывает_карточку()
    {
        var world = new World();
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.ReadDocument => SabyHandler.Result("""
                {"Состояние":{"Код":"10","Название":"В обработке"},"Этап":{"Название":"Приемка груза","Действие":[{"Название":"Получен"}]}}
                """),
            EtrnMethods.WriteEnclosure => SabyHandler.Error("Документ находится в роуминге, действия невозможны"),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.ExecuteAsync(ContextEmpty, Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Completed);
        Assert.False(result.Value.CloseDocument);
        Assert.Equal("Документ находится в роуминге, действия невозможны", result.Value.Error);
        Assert.DoesNotContain(world.Handler.Calls, call => call.Method == EtrnMethods.PrepareAction);
    }

    [Fact]
    public async Task Ссылка_подготовки_подписывается_байтами_файла_а_не_хешем()
    {
        var world = new World();
        var file = new byte[] { 9, 8, 7 };
        world.Handler.Files["https://files.saby.ru/1.bin"] = file;
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.WriteEnclosure => SabyHandler.Result("true"),
            EtrnMethods.PrepareAction => SabyHandler.Result("""
                {"Документ":{"Вложение":[{"Файл":{"Ссылка":"https://files.saby.ru/1.bin","Хеш":"aGFzaA=="}}]}}
                """),
            EtrnMethods.ExecuteAction => SabyHandler.Result("true"),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.ExecuteAsync(ContextEmpty, Command(), CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        var request = Assert.Single(world.Signer.Requests);
        Assert.Equal(file, request.Content);
        Assert.Equal("", request.Hash);
        Assert.Equal("", request.FileUrl);
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
        Assert.Equal("e1", call.Filter("ИдентификаторСобытия"));
        Assert.Equal("d1", call.Filter("ИдентификаторДокумента"));
        Assert.Equal("", call.Filter("ДатаВремяС"));
        Assert.Equal("7700000000", call.OrganizationInn("СвЮЛ"));
        Assert.Equal("", call.OrganizationInn("СвФЛ"));
        Assert.Equal("ConsignmentNote", call.Filter("Тип"));
        Assert.Equal("Нет", call.Filter("ПолныйСертификатЭП"));
        Assert.Equal("50", call.PageSize());

        await world.Gateway.FetchAsync(Context(new EtrnPollPosition("e1", "d1"), "123456789012"), CancellationToken.None);
        var person = world.Handler.Calls.Last(item => item.Method == EtrnMethods.ChangesList);
        Assert.Equal("123456789012", person.OrganizationInn("СвФЛ"));
        Assert.Equal("", person.OrganizationInn("СвЮЛ"));

        await world.Gateway.FetchAsync(
            Context(new EtrnPollPosition("e1", "d1", "11.10.2026 12.00.00")),
            CancellationToken.None);
        var dated = world.Handler.Calls.Last(item => item.Method == EtrnMethods.ChangesList);
        Assert.Equal("e1", dated.Filter("ИдентификаторСобытия"));
        Assert.Equal("d1", dated.Filter("ИдентификаторДокумента"));
        Assert.Equal("11.10.2026 12.00.00", dated.Filter("ДатаВремяС"));
    }

    [Fact]
    public async Task Первый_запуск_без_даты_берёт_1_сентября_2026()
    {
        var world = new World();

        await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        var call = world.Handler.Calls.Single(item => item.Method == EtrnMethods.ChangesList);
        Assert.Equal("", call.Filter("ИдентификаторСобытия"));
        Assert.Equal("", call.Filter("ИдентификаторДокумента"));
        Assert.Equal("01.09.2026 00.00.00", call.Filter("ДатаВремяС"));
    }

    [Fact]
    public async Task Пустая_пара_берёт_дату_подключения()
    {
        var world = new World();
        var context = Context(EtrnPollPosition.Empty);
        context.Connection.IncomingFrom = "2026-10-15";

        await world.Gateway.FetchAsync(context, CancellationToken.None);

        var call = world.Handler.Calls.Single(item => item.Method == EtrnMethods.ChangesList);
        Assert.Equal("", call.Filter("ИдентификаторСобытия"));
        Assert.Equal("", call.Filter("ИдентификаторДокумента"));
        Assert.Equal("15.10.2026 00.00.00", call.Filter("ДатаВремяС"));
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
    public async Task Страница_документов_сохраняет_накладную_и_не_закрывает_её()
    {
        var world = new World();
        world.Handler.Files["https://files.test/title.xml"] = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "XmlSamples", "sender-simple.xml"));
        world.Handler.Respond = (call, _) => call.Method == EtrnMethods.ChangesList
            ? SabyHandler.Result("""
                {
                  "Документ": [
                    {
                      "Идентификатор": "d1",
                      "Событие": [{
                        "Идентификатор": "e1",
                        "Название": "Пересылка вложений",
                        "Вложение": [{
                          "Подтип": "1110339",
                          "Файл": {"Имя": "ON_TRNACLGROT_1.xml", "Ссылка": "https://files.test/title.xml"}
                        }]
                      }]
                    },
                    {
                      "Идентификатор": "d1",
                      "Событие": [{ "Идентификатор": "e2", "Название": "Получение груза" }]
                    }
                  ],
                  "Навигация": {"ЕстьЕще": "Нет", "РазмерСтраницы": "50", "СледующаяПозиция": {}}
                }
                """)
            : SabyHandler.Result("true");

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.True(result.Value.Completed, result.IsFailure ? result.Error : result.Value.Error);
        var document = Assert.Single(result.Value.Documents);
        Assert.Equal("d1", document.OperatorDocumentId);
        Assert.Equal("ТН-77", document.Number);
        Assert.Empty(result.Value.ClosedDocumentIds);
        Assert.Equal(new EtrnPollPosition("e2", "d1"), result.Value.Position);
        Assert.Contains(world.Handler.Calls, call => call.Method == EtrnMethods.ReadDocument);
        var download = Assert.Single(world.Handler.Calls, call => call.Method == "GET");
        var list = Assert.Single(world.Handler.Calls, call => call.Method == EtrnMethods.ChangesList);
        Assert.Equal(list.SessionHeader, download.SessionHeader);
        Assert.Equal(SabyUserAgent.Value, download.UserAgent);
    }

    [Fact]
    public async Task ЕстьЕще_дочитывает_страницы_по_времени_последнего_события()
    {
        var world = new World();
        var xml = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "XmlSamples", "sender-simple.xml"));
        world.Handler.Files["https://files.test/title.xml"] = xml;
        world.Handler.Respond = (call, count) => call.Method == EtrnMethods.ChangesList
            ? count == 1
                ? SabyHandler.Result("""
                    {
                      "Документ": [{
                        "Идентификатор": "d1",
                        "Событие": [{
                          "Идентификатор": "e1",
                          "ДатаВремя": "01.10.2026 10.00.00",
                          "Название": "Пересылка вложений",
                          "Вложение": [{
                            "Подтип": "1110339",
                            "Файл": {"Имя": "ON_TRNACLGROT_1.xml", "Ссылка": "https://files.test/title.xml"}
                          }]
                        }]
                      }],
                      "Навигация": {"ЕстьЕще": "Да", "РазмерСтраницы": "50", "СледующаяПозиция": {}}
                    }
                    """)
                : SabyHandler.Result("""
                    {
                      "Документ": [{
                        "Идентификатор": "d2",
                        "Событие": [{
                          "Идентификатор": "e2",
                          "ДатаВремя": "02.10.2026 11.00.00",
                          "Название": "Пересылка вложений",
                          "Вложение": [{
                            "Подтип": "1110339",
                            "Файл": {"Имя": "ON_TRNACLGROT_2.xml", "Ссылка": "https://files.test/title.xml"}
                          }]
                        }]
                      }],
                      "Навигация": {"ЕстьЕще": "Нет", "РазмерСтраницы": "50"}
                    }
                    """)
            : SabyHandler.Result("true");

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.True(result.Value.Completed, result.IsFailure ? result.Error : result.Value.Error);
        Assert.Equal(["d1", "d2"], result.Value.Documents.Select(item => item.OperatorDocumentId));
        Assert.Equal(new EtrnPollPosition("e2", "d2", "02.10.2026 11.00.00"), result.Value.Position);
        var lists = world.Handler.Calls.Where(call => call.Method == EtrnMethods.ChangesList).ToList();
        Assert.Equal(2, lists.Count);
        Assert.Equal("e1", lists[1].Filter("ИдентификаторСобытия"));
        Assert.Equal("d1", lists[1].Filter("ИдентификаторДокумента"));
        Assert.Equal("01.10.2026 10.00.00", lists[1].Filter("ДатаВремяС"));
        Assert.Equal(2, world.Handler.Calls.Count(call => call.Method == EtrnMethods.ReadDocument));
    }

    [Fact]
    public async Task Страница_закрывает_накладную_если_состояние_уже_завершено()
    {
        var world = new World();
        world.Handler.Files["https://files.test/title.xml"] = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "XmlSamples", "sender-simple.xml"));
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.ChangesList => OneDocumentPage(),
            EtrnMethods.ReadDocument => SabyHandler.Result("""
                {"Состояние":{"Код":"7","Название":"Выполнение завершено успешно"}}
                """),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        var document = Assert.Single(result.Value.Documents);
        Assert.Equal(EtrnDocumentStatus.Closed, document.Status);
        Assert.Equal("Выполнение завершено успешно", document.Error);
    }

    [Fact]
    public async Task Страница_чужого_этапа_оставляет_ожидание()
    {
        var world = new World();
        world.Handler.Files["https://files.test/title.xml"] = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "XmlSamples", "sender-simple.xml"));
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.ChangesList => OneDocumentPage(),
            EtrnMethods.ReadDocument => SabyHandler.Result("""
                {"Состояние":{"Код":"10","Название":"В обработке"},"Этап":{"Название":"Получение груза"}}
                """),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.True(result.Value.Completed, result.Value.Error);
        var document = Assert.Single(result.Value.Documents);
        Assert.Equal(EtrnDocumentStatus.Waiting, document.Status);
        Assert.Equal("Сейчас этап «Получение груза», приёмка недоступна", document.Error);
    }

    [Fact]
    public async Task Обрыв_чтения_состояния_не_сохраняет_накладную()
    {
        var world = new World();
        world.Handler.Files["https://files.test/title.xml"] = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "XmlSamples", "sender-simple.xml"));
        world.Handler.Respond = (call, _) => call.Method switch
        {
            EtrnMethods.ChangesList => OneDocumentPage(hasMore: true),
            EtrnMethods.ReadDocument => SabyHandler.Error("Saby недоступна"),
            _ => SabyHandler.Result("true")
        };

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Empty(result.Value.Documents);
        Assert.Equal(new EtrnPollPosition("e1", "d1", "01.10.2026 10.00.00"), result.Value.Position);
        Assert.Single(world.Handler.Calls, call => call.Method == EtrnMethods.ChangesList);
    }

    [Fact]
    public async Task Повтор_того_же_события_останавливает_дочитывание()
    {
        var world = new World();
        world.Handler.Files["https://files.test/title.xml"] = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "XmlSamples", "sender-simple.xml"));
        world.Handler.Respond = (call, _) => call.Method == EtrnMethods.ChangesList
            ? SabyHandler.Result("""
                {
                  "Документ": [{
                    "Идентификатор": "d1",
                    "Событие": [{
                      "Идентификатор": "e1",
                      "ДатаВремя": "01.10.2026 10.00.00",
                      "Название": "Пересылка вложений",
                      "Вложение": [{
                        "Подтип": "1110339",
                        "Файл": {"Имя": "ON_TRNACLGROT_1.xml", "Ссылка": "https://files.test/title.xml"}
                      }]
                    }]
                  }],
                  "Навигация": {"ЕстьЕще": "Да"}
                }
                """)
            : SabyHandler.Result("true");

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.True(result.Value.Completed, result.IsFailure ? result.Error : result.Value.Error);
        Assert.Equal(2, world.Handler.Calls.Count(call => call.Method == EtrnMethods.ChangesList));
    }

    [Fact]
    public async Task Обрыв_скачивания_не_запрашивает_следующую_страницу()
    {
        var world = new World();
        world.Handler.Files["https://files.test/title.xml"] = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "XmlSamples", "sender-simple.xml"));
        world.Handler.Respond = (call, _) => call.Method == EtrnMethods.ChangesList
            ? SabyHandler.Result("""
                {
                  "Документ": [
                    {
                      "Идентификатор": "d1",
                      "Событие": [{
                        "Идентификатор": "e1",
                        "ДатаВремя": "01.10.2026 10.00.00",
                        "Название": "Пересылка вложений",
                        "Вложение": [{
                          "Подтип": "1110339",
                          "Файл": {"Имя": "ON_TRNACLGROT_1.xml", "Ссылка": "https://files.test/title.xml"}
                        }]
                      }]
                    },
                    {
                      "Идентификатор": "d2",
                      "Событие": [{
                        "Идентификатор": "e2",
                        "ДатаВремя": "02.10.2026 11.00.00",
                        "Название": "Пересылка вложений",
                        "Вложение": [{
                          "Подтип": "1110339",
                          "Файл": {"Имя": "ON_TRNACLGROT_2.xml", "Ссылка": "https://files.test/missing.xml"}
                        }]
                      }]
                    }
                  ],
                  "Навигация": {"ЕстьЕще": "Да"}
                }
                """)
            : SabyHandler.Result("true");

        var result = await world.Gateway.FetchAsync(ContextEmpty, CancellationToken.None);

        Assert.False(result.Value.Completed);
        Assert.Equal("d1", Assert.Single(result.Value.Documents).OperatorDocumentId);
        Assert.Equal(new EtrnPollPosition("e2", "d2", "02.10.2026 11.00.00"), result.Value.Position);
        Assert.Single(world.Handler.Calls, call => call.Method == EtrnMethods.ChangesList);
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
            EtrnMethods.ReadDocument => call.DocumentId() == "d1"
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
            ? SabyHandler.Result("""{"Этап":{"Название":"Приемка груза","Действие":[{"Название":"Получен"}]}}""")
            : SabyHandler.Result("true");

        var result = await world.Gateway.ReadStageAsync(ContextEmpty, "d1", CancellationToken.None);

        Assert.True(result.Value.Completed);
        Assert.True(result.Value.StillAcceptance);
        Assert.Single(result.Value.Exchanges);
        Assert.Equal("d1", world.Handler.Calls.Single(call => call.Method == EtrnMethods.ReadDocument).DocumentId());
    }

    [Fact]
    public async Task Проверка_входит_по_сертификату_и_не_кладёт_сессию_в_кэш()
    {
        var world = new World();
        var context = Context(EtrnPollPosition.Empty);

        var result = await world.Gateway.ProbeAsync(context, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : "");
        Assert.True(result.Value.Ok);
        Assert.Equal("Saby приняла сертификат", result.Value.Message);
        Assert.Empty(result.Value.Exchanges);
        Assert.Empty(world.Handler.Calls);
        Assert.Equal(1, world.Auth.Logins);
        Assert.Equal("serial-1", world.Auth.Last?.CertificateNumber);
        await world.Sessions.GetAsync(
            SabySessionStore.Key(context.OrganizationId, context.CertificateNumber),
            token => world.Auth.LoginAsync(context, token),
            CancellationToken.None);
        Assert.Equal(2, world.Auth.Logins);
    }

    [Fact]
    public async Task Проверка_без_сертификата_не_ходит_в_Saby()
    {
        var world = new World();
        var context = Context(EtrnPollPosition.Empty);
        context = new EtrnOperatorContext
        {
            OrganizationId = context.OrganizationId,
            Connection = context.Connection,
            CertificateNumber = " "
        };

        var result = await world.Gateway.ProbeAsync(context, CancellationToken.None);

        Assert.False(result.Value.Ok);
        Assert.Equal("Не выбран сертификат организации", result.Value.Message);
        Assert.Equal(0, world.Auth.Logins);
        Assert.Empty(world.Handler.Calls);
    }

    [Fact]
    public async Task Отказ_входа_Saby_возвращается_текстом()
    {
        var world = new World();
        world.Auth.Failure = "Сертификат не принят";

        var result = await world.Gateway.ProbeAsync(Context(EtrnPollPosition.Empty), CancellationToken.None);

        Assert.False(result.Value.Ok);
        Assert.Equal("Сертификат не принят", result.Value.Message);
        Assert.Empty(result.Value.Exchanges);
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

    private static EtrnOperatorContext Context(EtrnPollPosition position, string inn = "7700000000") => new()
    {
        OrganizationId = "o1",
        Inn = inn,
        Connection = new EtrnConnection { Id = "c1", Type = EtrnOperatorType.Saby, Name = "СБИС" },
        CertificateNumber = "serial-1",
        CertificatePassword = "pwd",
        Position = position
    };

    private static string OneDocumentPage(bool hasMore = false) =>
        SabyHandler.Result($$"""
            {
              "Документ": [{
                "Идентификатор": "d1",
                "Событие": [{
                  "Идентификатор": "e1",
                  "ДатаВремя": "01.10.2026 10.00.00",
                  "Название": "Пересылка вложений",
                  "Вложение": [{
                    "Подтип": "1110339",
                    "Файл": {"Имя": "ON_TRNACLGROT_1.xml", "Ссылка": "https://files.test/title.xml"}
                  }]
                }]
              }],
              "Навигация": {"ЕстьЕще": "{{(hasMore ? "Да" : "Нет")}}"}
            }
            """);

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
                ? SabyHandler.Result("""
                    {"Документ":{"Вложение":[{"Идентификатор":"att-1","Файл":{"Хеш":"hash-1"}}]}}
                    """)
                : SabyHandler.Result("true");
            Rpc = new SabyJsonRpc(new SabyHttpClientFactory(Handler), NullLogger<SabyJsonRpc>.Instance);
            Gateway = new EtrnSabyGateway(
                Rpc,
                Sessions,
                Auth,
                new FakeSabyCertificates(),
                Signer,
                new SabyHttpClientFactory(Handler),
                NullLogger<EtrnSabyGateway>.Instance);
        }
    }
}
