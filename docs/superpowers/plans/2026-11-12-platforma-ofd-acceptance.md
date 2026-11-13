# Приёмка Платформы ОФД: список, подпись, титул 3

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Опрос и подпись приёмки грузополучателя совпадают с рабочим клиентом расширения `ПлатформаЭПЛ`: страницы списка, подпись сохранённого XML и титул `1110341` в его разметке.

**Architecture:** Меняется только `OfdEtrnGateway` и `ConsigneeTitleWriter`. Роль одна — грузополучатель, статус в списке приёмки по-прежнему только `CARGO_ON_WAY`. Курсор страниц живёт внутри одного `FetchAsync` и в `EtrnPollPosition` не пишется: следующий опрос снова начинается с новых накладных. Подпись берётся с байт `GET .../content` после `PUT`, как `ПодписатьДокументВСервисе`.

**Tech Stack:** .NET 10, xUnit, тот же `IEtrnOperator`. Живая Платформа ОФД в тестах не вызывается.

## Global Constraints

- В текстах для пользователя и в комментариях имя оператора — `Платформа ОФД`. Типы и файлы `Ofd*` не переименовывать. Имя подключения в тестовых данных (`Name = "ОФД"`) — подпись карточки, не текст продукта.
- Цепочка Т1, Т2, Т4–Т8, приглашения, `document-changes`, `actions`, свой HTTP `epd_test` не переносятся.
- Вход по API-ключу (ГОСТ Р 34.11-94, `grant_type=password`) остаётся. Интерактивный Keycloak расширения не копируется.
- `number` в списке — размер страницы, не номер накладной. Первая страница: `subscriber-id` и `number=-30`, без `from-id`. Следующая: тот же запрос плюс `from-id` = минимальный числовой `id` предыдущей страницы.
- Страницы кончаются, когда ответ пустой, карточек меньше 30, минимальный id не стал меньше курсора, либо набор id повторился. Больше 100 страниц — ошибка `Платформа ОФД не отдала конец списка`, опрос не помечается завершённым.
- Отсутствие id на страницах документ не закрывает. Закрывается только id, который вернулся не в `CARGO_ON_WAY`.
- `PUT .../documents/1110341v5.01` — байты windows-1251, `Content-Type: application/xml`. Подписываются не они, а тело `GET /transedm/public/api/v1/documents/{id}/content` того документа, у которого `knd` содержит `1110341`. До 5 чтений списка документов; между неудачными попытками пауза. В тестах пауза нулевая.
- `PUT .../signature` — сырые байты CMS, `application/octet-stream`.
- Титул 3 без `Подписант` и без идентификатора участника ЭДО.
- Приём: атрибут `СодОпПр`, узла `ПричОтк` нет. Отказ: атрибут `ПричОтк`, атрибута `СодОпПр` нет. Строки факта только у частичной приёмки, путь `СодИнфГП/ПриемГрузГП/СвПринПоНаим`. Комментарии расхождения — `СодИнфГП/ОтметГП/@ОбстОтмет`, по одному узлу на уникальный непустой комментарий.

---

### Task 1: Тексты «Платформа ОФД»

**Files:**
- Modify: `src/Infrastructure/EtrnOfd/OfdEtrnGateway.cs`
- Modify: `src/Infrastructure/EtrnOfd/OfdTokenClient.cs`
- Modify: `src/Infrastructure/EtrnOfd/OfdApiKeyHash.cs`
- Modify: `src/Core/Domain/Etrn/Xml/ConsigneeTitleWriter.cs`
- Modify: `src/tests/EtrnOfd.Tests/OfdEtrnGatewayTests.cs`
- Modify: `src/tests/EtrnOfd.Tests/OfdTokenClientTests.cs`
- Modify: `src/tests/Application.Tests/EtrnProbeServiceTests.cs`

**Interfaces:**
- Consumes: текущие строки ошибок шлюза и токена
- Produces: те же ситуации, текст начинается с `Платформа ОФД`

- [ ] **Step 1: Поправить ожидания тестов**

В `OfdEtrnGatewayTests.Чужой_корень_списка_не_сдвигает_пару_Saby` и `Отказ_списка_при_проверке_не_читает_титулы`:

```csharp
Assert.Equal("Платформа ОФД вернула не список ЭТрН", result.Value.Error);
Assert.Equal("Платформа ОФД ответила 401", result.Value.Message);
```

В `OfdTokenClientTests` ожидание `Платформа ОФД не вернула токен`. В `EtrnProbeServiceTests` сообщение `Платформа ОФД ответила 401`.

- [ ] **Step 2: Прогон — FAIL**

Run: `dotnet test src/tests/EtrnOfd.Tests/EtrnOfd.Tests.csproj -v q`

Expected: FAIL, фактические строки ещё `ОФД вернула` / `ОФД ответила` / `ОФД не вернула` / `ОФД не выдала`.

- [ ] **Step 3: Заменить тексты**

В `OfdEtrnGateway` и `OfdTokenClient` каждая строка, которая сейчас начинается с `ОФД `, начинается с `Платформа ОФД `. Комментарий `ConsigneeTitleWriter` говорит про документы Платформы ОФД. Комментарий `OfdApiKeyHash` — про документацию Платформы ОФД.

- [ ] **Step 4: Прогон — PASS**

Run: `dotnet test src/tests/EtrnOfd.Tests/EtrnOfd.Tests.csproj -v q` и `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~EtrnProbeServiceTests -v q`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Infrastructure/EtrnOfd src/Core/Domain/Etrn/Xml/ConsigneeTitleWriter.cs src/tests/EtrnOfd.Tests src/tests/Application.Tests/EtrnProbeServiceTests.cs
git commit -m "fix: в текстах драйвера имя Платформа ОФД"
```

---

### Task 2: Страницы списка

**Files:**
- Modify: `src/Infrastructure/EtrnOfd/OfdEtrnGateway.cs`
- Modify: `src/tests/EtrnOfd.Tests/OfdEtrnGatewayTests.cs`

**Interfaces:**
- Consumes: `FetchAsync`, `ProbeAsync`, разбор карточки `TryCards`
- Produces: `ListUrl(api, subscriber, fromId)` — при пустом `fromId` нет параметра `from-id`; всегда есть `number=-30`. `FetchAsync` обходит страницы до конца. `ProbeAsync` делает один такой запрос и страницы не листает. `EtrnPollPosition` не меняется.

- [ ] **Step 1: Падающий тест второй страницы**

В `OfdEtrnGatewayTests` заменить оба `Assert.DoesNotContain("number="` на:

```csharp
Assert.Contains("number=-30", list.Url, StringComparison.Ordinal);
Assert.DoesNotContain("from-id=", list.Url, StringComparison.Ordinal);
```

Добавить тест:

```csharp
[Fact]
public async Task Опрос_листает_страницу_назад_по_минимальному_id()
{
    var world = World.Ready();
    var first = string.Join(',', Enumerable.Range(0, 30)
        .Select(i => $$$"""{"id":{{{1000 + i}}},"status":"CARGO_DELIVERED","number":"с"}"""));
    world.Handler.Respond = call =>
    {
        if (call.Url.Contains("/protocol/openid-connect/token", StringComparison.Ordinal))
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
    Assert.Equal(world.Context.Position, result.Value.Position);
}
```

Короткая страница (текущие фикстуры короче 30) второго запроса не делает. Проверка подключения при списке из 30 карточек тоже не делает второй запрос: в тесте проверки список пустой, это уже так.

- [ ] **Step 2: Прогон — FAIL**

Run: `dotnet test src/tests/EtrnOfd.Tests/EtrnOfd.Tests.csproj --filter FullyQualifiedName~OfdEtrnGatewayTests -v q`

Expected: FAIL, в URL нет `number=-30`, второй страницы нет.

- [ ] **Step 3: Листание**

В `OfdEtrnGateway`:

```csharp
private const int PageSize = 30;
private const int PageCap = 100;

private static string ListUrl(string api, string subscriber, string? fromId)
{
    var url = $"{api}/transedm/public/api/v1/etrns?subscriber-id={Uri.EscapeDataString(subscriber)}&number=-{PageSize}";
    if (!string.IsNullOrEmpty(fromId))
        url += "&from-id=" + Uri.EscapeDataString(fromId);
    return url;
}
```

`ProbeAsync` и первый запрос `FetchAsync` вызывают `ListUrl(..., null)`.

`FetchAsync` после разбора страницы обрабатывает карточки как сейчас. Если карточек меньше `PageSize` — выход. Иначе `fromId` = минимальный `id`, который парсится в `long`. Id без числа — останов без ошибки, уже прочитанное остаётся. Следующий URL — `ListUrl(..., fromId)`. Если минимальный id новой страницы не строго меньше курсора или набор id совпал с предыдущей страницей — выход. На 101-й странице `Completed = false`, ошибка `Платформа ОФД не отдала конец списка`, уже собранные документы и закрытые id остаются в результате, `Position` равен входному.

- [ ] **Step 4: Прогон — PASS**

Run: `dotnet test src/tests/EtrnOfd.Tests/EtrnOfd.Tests.csproj --filter FullyQualifiedName~OfdEtrnGatewayTests -v q`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Infrastructure/EtrnOfd/OfdEtrnGateway.cs src/tests/EtrnOfd.Tests/OfdEtrnGatewayTests.cs
git commit -m "fix: список Платформы ОФД читается страницами по 30"
```

---

### Task 3: Подпись сохранённого титула

**Files:**
- Modify: `src/Infrastructure/EtrnOfd/OfdEtrnGateway.cs`
- Modify: `src/tests/EtrnOfd.Tests/OfdEtrnGatewayTests.cs`

**Interfaces:**
- Consumes: `ExecuteAsync`, `IEtrnSignaturePort.SignAsync`, `ConsigneeTitleWriter.ToUtf8` до Task 4
- Produces: после успешного `PUT` титула шлюз читает список документов, затем `content`, и в `EtrnSignatureRequest.Content` кладёт эти байты. Пауза передаётся в конструктор как `Func<CancellationToken, Task>? pause`. Боевой конструктор вызывает `Task.Delay(TimeSpan.FromMilliseconds(1500), token)`. Тестовый `World.Ready` передаёт `ct => Task.CompletedTask`.

- [ ] **Step 1: Падающий тест приёмки**

Переписать `Приёмка_кладёт_XML_и_затем_байты_подписи`. Обработчик:

- токен — как сейчас;
- `PUT`, URL заканчивается на `/documents/1110341v5.01` — `200` и тело `{"status":"ok"}`;
- `GET`, URL заканчивается на `/etrns/308924/documents` — первый раз `[]`, второй раз `[{"id":77,"knd":"1110341","formVersion":"5.01"}]`;
- `GET`, URL заканчивается на `/documents/77/content` — байты `Encoding.UTF8.GetBytes("<Файл/>")`, не равные телу `PUT`;
- остальное — `404`.

Проверки:

```csharp
Assert.True(result.Value.Completed, result.Value.Error);
var puts = world.Handler.Calls.Where(call => call.Method == "PUT").ToList();
Assert.Equal(2, puts.Count);
Assert.EndsWith("/etrns/308924/documents/1110341v5.01", puts[0].Url, StringComparison.Ordinal);
Assert.Equal(xml, puts[0].Body);
Assert.Equal(stored, world.Signer.Last.Content);
Assert.NotEqual(xml, world.Signer.Last.Content);
Assert.Equal(2, world.Handler.Calls.Count(call => call.Url.EndsWith("/etrns/308924/documents", StringComparison.Ordinal)));
Assert.Equal(signature, puts[1].Body);
Assert.StartsWith("application/octet-stream", puts[1].ContentType, StringComparison.Ordinal);
```

`xml` пока равен `ConsigneeTitleWriter.ToUtf8(...)`. `stored` — байты `<Файл/>`.

- [ ] **Step 2: Прогон — FAIL**

Run: `dotnet test src/tests/EtrnOfd.Tests/EtrnOfd.Tests.csproj --filter FullyQualifiedName~Приёмка_кладёт -v q`

Expected: FAIL, подписываются байты `PUT`, второго чтения списка нет.

- [ ] **Step 3: Читать content перед подписью**

После успешного `PUT` и до `SignAsync`:

```csharp
private async Task<OfdResponse> ConsigneeContentAsync(
    string api, string token, string etrnId, List<EtrnExchange> exchanges, CancellationToken cancellationToken)
{
    var documentsUrl = $"{api}/transedm/public/api/v1/etrns/{Uri.EscapeDataString(etrnId)}/documents";
    for (var attempt = 1; attempt <= 5; attempt++)
    {
        var documents = await CallAsync(HttpMethod.Get, documentsUrl, token, null, null, null,
            "Титулы ЭТрН", etrnId, documentsUrl, exchanges, cancellationToken);
        if (!documents.Ok)
            return documents;
        if (TryConsigneeDocument(documents.Text, out var documentId))
        {
            var contentUrl = $"{api}/transedm/public/api/v1/documents/{Uri.EscapeDataString(documentId)}/content";
            return await CallAsync(HttpMethod.Get, contentUrl, token, null, null, "application/xml",
                "Титул грузополучателя", etrnId, contentUrl, exchanges, cancellationToken);
        }
        if (attempt < 5)
            await _pause(cancellationToken);
    }
    return new OfdResponse(false, "Титул грузополучателя не найден в Платформе ОФД", [], "");
}

private static bool TryConsigneeDocument(string json, out string documentId)
```

`TryConsigneeDocument` устроен как `TryShipperDocument`, но `knd` содержит `1110341`, а не равен `1110339`. Пустой `content` при `Ok` — ошибка `Платформа ОФД вернула пустой титул грузополучателя`. В `SignAsync` уходит `content.Bytes`.

- [ ] **Step 4: Прогон — PASS**

Run: `dotnet test src/tests/EtrnOfd.Tests/EtrnOfd.Tests.csproj --filter FullyQualifiedName~OfdEtrnGatewayTests -v q`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Infrastructure/EtrnOfd/OfdEtrnGateway.cs src/tests/EtrnOfd.Tests/OfdEtrnGatewayTests.cs
git commit -m "fix: подпись приёмки берётся из сохранённого титула Платформы ОФД"
```

---

### Task 4: Титул 3 в разметке расширения

**Files:**
- Modify: `src/Core/Domain/Etrn/Xml/ConsigneeTitleWriter.cs`
- Modify: `src/tests/Domain.Tests/XmlSamples/consignee-partial.xml`
- Modify: `src/tests/Domain.Tests/ConsigneeTitleWriterTests.cs`
- Modify: `src/tests/EtrnOfd.Tests/OfdEtrnGatewayTests.cs` — тело `PUT` сравнивать с `ConsigneeTitleWriter.ToWindows1251`

**Interfaces:**
- Consumes: `EtrnIncomingDocumentEntity.Lines` (`Index`, `Name`), `EtrnDecision`
- Produces: `ConsigneeTitleWriter.ToWindows1251(string) : byte[]`. `Build` возвращает XML без объявления. `ToWindows1251` дописывает `<?xml version="1.0" encoding="windows-1251"?>` и кодирует windows-1251. `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` — тем же вызовом, что в `SenderTitleReader`.

Образец `consignee-partial.xml` (объявление в файле образца для чтения человеком; сравнение в тесте идёт по дереву, как сейчас, либо по строке `Build` без объявления — оставить текущий способ сравнения теста и обновить текст образца):

```xml
<?xml version="1.0" encoding="windows-1251"?>
<Файл ВерсФорм="5.01" ВерсПрог="FmuApiCentral/2.3">
  <Документ КНД="1110341" ПоФактХЖ="Транспортная накладная, информация грузополучателя" ДатИнфГП="10.11.2026" ВрИнфГП="13:05:00">
    <СодИнфГП УИД_ТрН="uid-1" НомДок="ТН-77" ДатаДок="04.11.2026" СодОпПр="Получен частично">
      <ПриемГрузГП>
        <СвПринПоНаим НаимГруз="Сыр" КолМестГр="9" МасБрутЗнач="99.5" />
        <СвПринПоНаим НаимГруз="Масло" КолМестГр="5" МасБрутЗнач="50" />
      </ПриемГрузГП>
      <ОтметГП ОбстОтмет="недовоз" />
      <ОтметГП ОбстОтмет="общий" />
    </СодИнфГП>
  </Документ>
</Файл>
```

`КолМестГр` и `МасБрутЗнач` — имена количества мест и массы брутто в карте XML расширения (`ПлЭПД_КлиентскиеНастройкиСервер`, блок груза). `НаимГруз` оттуда же. Узел строки приёмки в расширении — `СвПринПоНаим`, отметка — `ОтметГП`, обстоятельство — `ОбстОтмет`.

Имена груза тест задаёт на `document.Lines`: индекс 1 — `Сыр`, индекс 2 — `Масло`. Строка решения без пары в `document.Lines` пишется без `НаимГруз`.

- [ ] **Step 1: Поправить тесты писателя**

`Частичная_приёмка_совпадает_с_образцом` кладёт имена в строки документа и сравнивает с новым образцом.

`Полный_приём_не_пишет_факты_строк` дополнительно проверяет: есть `СодОпПр="Получен"`, нет `ПричОтк`, нет `ПриемГрузГП`, нет `ОтметГП`.

`Отказ_пишет_общий_комментарий_без_фактов`: нет `СодОпПр`, есть `ПричОтк` с текстом комментария, нет `ПриемГрузГП` и нет `ОтметГП`.

Новый тест:

```csharp
[Fact]
public void Тело_для_платформы_в_windows_1251()
{
    var xml = ConsigneeTitleWriter.Build(document, decision, signedAt);
    var bytes = ConsigneeTitleWriter.ToWindows1251(xml);
    var text = Encoding.GetEncoding("windows-1251").GetString(bytes);
    Assert.StartsWith("<?xml version=\"1.0\" encoding=\"windows-1251\"?>", text, StringComparison.Ordinal);
    Assert.Contains("СодОпПр", text, StringComparison.Ordinal);
}
```

`document` / `decision` — те же, что в частичной приёмке. В тесте перед `GetEncoding` вызвать `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`.

В `Приёмка_кладёт_XML_и_затем_байты_подписи` тело `PUT` равно `ToWindows1251(Build(...))`.

- [ ] **Step 2: Прогон — FAIL**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~ConsigneeTitleWriterTests -v q`

Expected: FAIL, в документе ещё `ПриемГруза` и нет `СвПринПоНаим`.

- [ ] **Step 3: Писатель**

`Build`:

- `СодОпПр` ставится для `Received` и `Partially` значением `decision.Action`. Для `Rejected` атрибут не ставится, ставится `ПричОтк` = `decision.GeneralComment`.
- Для `Partially` один элемент `ПриемГрузГП`. На каждую строку решения — `СвПринПоНаим`. `НаимГруз` из `document.Lines` с тем же `Index`, если имя не пустое. `КолМестГр` из `FactPlaces`, если значение есть. `МасБрутЗнач` из `FactWeight`, если не пусто. Узел `ПриемГруза` удалить.
- Непустые `Comment` строк, без повторов, в порядке строк — элементы `ОтметГП` с атрибутом `ОбстОтмет`.
- `Received` не пишет `ПриемГрузГП` и `ОтметГП`.

`ToWindows1251` не вставляет перевод строки между объявлением и корнем.

- [ ] **Step 4: Прогон — PASS**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~ConsigneeTitleWriterTests -v q` и `dotnet test src/tests/EtrnOfd.Tests/EtrnOfd.Tests.csproj --filter FullyQualifiedName~OfdEtrnGatewayTests -v q`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Core/Domain/Etrn/Xml/ConsigneeTitleWriter.cs src/tests/Domain.Tests/ConsigneeTitleWriterTests.cs src/tests/Domain.Tests/XmlSamples/consignee-partial.xml src/tests/EtrnOfd.Tests/OfdEtrnGatewayTests.cs
git commit -m "fix: титул приёмки в разметке Платформы ОФД"
```
