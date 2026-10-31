# Исправления гашения входящих ВСД

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Гашение не затирает документ, который загрузка изменила параллельно, и не принимает чужой факт, чужую ТТН и исходящий TRANSPORT как погашенное поступление.

**Architecture:** Решение об объёме остаётся в `Domain.Vetis.VetisQuench`. Сервис перед каждым `Save` перечитывает документ и переносит на свежую копию только статус, факт и дату этого uuid. Один документ гасит один запрос: `SemaphoreSlim` живёт в singleton `VetisQuenchProgressStore`. Отбор TRANSPORT и выбор ТТН — в разборе XML. Уже лежащий исходящий TRANSPORT снимается отдельным проходом. Старый `sortAt` выравнивается страницами по `data.id`, с `Find` только у строк, где ключ не совпал с датой документа.

**Tech Stack:** C# / .NET 10, xUnit, CouchDB, Webix, node:test.

## Global Constraints

- TDD: сначала тест на одно поведение, прогон (падает по делу), минимальный код, снова прогон. Тест после реализации не считается.
- Моки только если иначе нельзя. Проверять состояние документа и ответ, не число вызовов само по себе.
- Комментарий только если без него неочевидно почему. Текст комментария на русском. XML-summary, который повторяет имя, не добавлять.
- Вложенных try/catch нет. Исключение ловится только на уже существующей границе репозитория и воркера.
- Домен не ссылается на инфраструктуру. `VetisApi.csproj` ссылается только на Domain: воркер резолвит интерфейс из Domain, как `IVetisIncomingLoader`. Ссылку VetisApi → Application не добавлять.
- `AddAutoRegisteredServices` регистрирует класс без интерфейсов конкретным типом, а с интерфейсами — только `interfaces[0]`. У нового сервиса воркера ровно один интерфейс.
- Коммиты не делать, пока пользователь явно не попросит.
- `docs/superpowers/plans/2026-10-01-true-api-uuid-token.md` не трогать и не добавлять в коммит гашения.

## Файлы

- `src/Core/Domain/Vetis/VetisQuench.cs` — `Decide`: пропущенный факт и переданный пустой факт.
- `src/Core/Domain/Vetis/VetisIncomingDocument.cs` — `AlignSortAt`, публичный `DocumentSortAt`, `DropForeignTransport`.
- `src/Core/Domain/Vetis/IVetisBackgroundJobs.cs` — `IVetisIncomingSortAligner`, `IVetisIncomingTransportSweep`.
- `src/Core/Domain/Vetis/VetisIncomingShipment.cs` — чужой TRANSPORT по guid грузополучателя.
- `src/Core/Domain/Vetis/IVetisIncomingRepository.cs` — `HeadsAfter`, `Delete`.
- `src/Core/Application/Vetis/VetisIncomingQuenchService.cs` — перечитывание, блокировка, фраза «уже погашен».
- `src/Core/Application/Vetis/VetisQuenchProgressStore.cs` — `TryEnter` / `Exit`.
- `src/Core/Application/Vetis/VetisIncomingQueryService.cs` — ошибка подсчёта наружу.
- `src/Core/Application/Vetis/VetisIncomingSortAligner.cs` — страница выравнивания, слой Application.
- `src/Core/Application/Vetis/VetisIncomingTransportSweep.cs` — страница снятия чужого TRANSPORT, слой Application.
- `src/Infrastructure/VetisApi/Xml/ProcessIncomingConsignmentXml.cs` — ТТН связи 1, дата со смещением.
- `src/Infrastructure/VetisApi/Xml/VetisResponseReader.cs` — TRANSPORT по guid грузополучателя.
- `src/Infrastructure/VetisApi/VetisGateway.cs` — передаёт guid площадки в разбор.
- `src/Infrastructure/VetisApi/Workers/VetisIncomingWorker.cs` — страницы до бюджета за круг, короткий повтор при сбое.
- `src/Infrastructure/CouchDb/Repositories/VetisIncomingQuery.cs` — keyset непринятых и страница по `data.id`.
- `src/Infrastructure/CouchDb/Repositories/VetisIncomingRepository.cs` — подсчёт и чтение страницы без `skip`.
- `src/Infrastructure/CouchDb/DatabaseScheme/DatabaseIndexes.cs` — индексы `status-id-idx` и `id-idx`.
- `src/Presentation/WebApi/Controllers/VetisController.cs` — 400 при ошибке подсчёта.
- `src/Presentation/WebApp/WebApp/wwwroot/js/utils/vetisFormat.js` — проверка факта и «тот же документ».
- `src/Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisIncomingWindow.js` — ячейка, кнопка, Esc, отчёт.
- `src/Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisWindow.js` — `canClose`.

---

### Task 1: Пустой переданный факт — ошибка, объём в акте с точкой

Пропущенный uuid в запросе по-прежнему означает полный объём. Пустая строка, пробел и нечисло — ошибка этого ВСД: журнал всегда шлёт поле `actualVolume`, и пробел сейчас становится `ACCEPT_ALL`. `IncomingVolume` пишется тем же форматом, что и факт, чтобы акт не смешивал `4.5` и `10,5`.

**Files:**
- Modify: `src/Core/Domain/Vetis/VetisQuench.cs`
- Test: `src/tests/Domain.Tests/VetisQuenchTests.cs`
- Test: `src/tests/Application.Tests/VetisQuenchXmlTests.cs`
- Test: `src/tests/Application.Tests/VetisIncomingQuenchServiceTests.cs`

**Interfaces:**
- Consumes: `VetisQuench.Decide(string? incoming, string? actual)`
- Produces: `actual == null` — полный объём; `""` и пробел — `IsFailure`. `IncomingVolume` — `Format(volume)` с точкой.

- [x] **Step 1: Заменить тест пустого факта и добавить отказ пробела**

В `VetisQuenchTests` тест `Пустой_факт_считается_полным_объёмом` заменить на два:

```csharp
[Fact]
public void Пропущенный_факт_считается_полным_объёмом()
{
    var order = VetisQuench.Decide("10", null).Value;

    Assert.Equal(VetisQuench.AcceptAll, order.Decision);
    Assert.Equal("10", order.IncomingVolume);
}

[Fact]
public void Переданный_пустой_факт_не_гасится()
{
    Assert.True(VetisQuench.Decide("10", "").IsFailure);
    Assert.True(VetisQuench.Decide("10", " ").IsFailure);
}

[Fact]
public void Объём_в_акте_пишется_с_точкой()
{
    var order = VetisQuench.Decide("10,5", "4,5").Value;

    Assert.Equal("10.5", order.IncomingVolume);
    Assert.Equal("4.5", order.ActualVolume);
}
```

- [x] **Step 2: Прогнать и убедиться, что падает по делу**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter "FullyQualifiedName~VetisQuenchTests" --nologo`

Expected: FAIL только у `Переданный_пустой_факт_не_гасится` и `Объём_в_акте_пишется_с_точкой`. `Пропущенный_факт_считается_полным_объёмом` зелёный до правки: `Decide` при пустом `actual` подставляет `incoming`, `IncomingVolume` остаётся `"10"`. Красный объём — из-за `Trim()` вместо `Format`. Не искать поломку в ветке `null`.

- [x] **Step 3: Разделить пропуск и пустую строку**

В `Decide` после успешного разбора `incoming`:

```csharp
if (actual != null && string.IsNullOrWhiteSpace(actual))
    return Result.Failure<VetisQuenchOrder>("Фактическое количество должно быть от 0 до объёма поступления.");

var factText = actual ?? incoming;
```

`IncomingVolume = Format(volume)` вместо `(incoming ?? "").Trim()`.

- [x] **Step 4: Прогнать доменные тесты**

Run: та же команда. Expected: PASS.

- [x] **Step 5: Сервис передаёт null, если uuid нет в запросе**

Сейчас при отсутствии ключа подставляется `vsd.Volume`, поэтому пустая строка из словаря и пропуск неразличимы. В `VetisIncomingQuenchService`:

```csharp
string? actual = requested.TryGetValue(vsd.Uuid, out var fact) ? fact : null;
var order = VetisQuench.Decide(vsd.Volume, actual);
```

Тест в `VetisIncomingQuenchServiceTests`: два `CONFIRMED`, в facts только `v1` с `ActualVolume = " "`, в очереди шлюза один `Done("v2")`. Ожидание: ошибка по `v1`, `QuenchCalls == 1`, `v2` погашен, `v1` остаётся `CONFIRMED`. Отдельный счётчик вызовов на uuid у шлюза не вести. Существующий `Quench_не_отправляет_факт_вне_объёма` оставить.

- [x] **Step 6: Акт с запятой в исходном объёме**

В `VetisQuenchXmlTests` добавить факт: `Decide("10,5", "4,5")`, в XML есть `Принято 4.5 из 10.5` и нет `10,5`.

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter "FullyQualifiedName~VetisQuenchXmlTests|FullyQualifiedName~VetisIncomingQuenchServiceTests" --nologo`

Expected: PASS.

---

### Task 2: Неверный факт в ячейке откатывается

`clampActualVolume` при `11`, `-1` и пустой строке подставляет объём поступления. В Меркурий уходит полная приёмка без сообщения. Ячейка должна оставить прежнее значение и показать ошибку. Сервер из Task 1 всё равно отвергнет пустую строку, если она дойдёт.

**Files:**
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/utils/vetisFormat.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisIncomingWindow.js`
- Test: `src/tests/WebApp.Tests/vetisFormat.test.mjs`

**Interfaces:**
- Produces: `editedActualVolume(incoming, edited) -> { ok: true, value } | { ok: false }`
- Produces: `canQuenchRows(vsds) -> boolean` — есть статус `CONFIRMED` без учёта регистра
- Produces: `sameIncomingDocument(openedId, requestId) -> boolean`

- [x] **Step 1: Тесты вместо подстановки объёма**

Заменить тест `факт не выходит за объём поступления`:

```javascript
test("верный факт нормализуется, неверный не подменяется объёмом", () => {
    assert.deepEqual(editedActualVolume("10", "4"), { ok: true, value: "4" });
    assert.deepEqual(editedActualVolume("10", "4,5"), { ok: true, value: "4.5" });
    assert.deepEqual(editedActualVolume("10", "0"), { ok: true, value: "0" });
    assert.deepEqual(editedActualVolume("10", "10"), { ok: true, value: "10" });
    assert.equal(editedActualVolume("10", "11").ok, false);
    assert.equal(editedActualVolume("10", "-1").ok, false);
    assert.equal(editedActualVolume("10", "").ok, false);
    assert.equal(editedActualVolume("10", "много").ok, false);
});

test("гасить можно только документ с оформленным ВСД", () => {
    assert.equal(canQuenchRows([{ status: "UTILIZED" }, { status: "confirmed" }]), true);
    assert.equal(canQuenchRows([{ status: "UTILIZED" }]), false);
    assert.equal(canQuenchRows([]), false);
});

test("отчёт гашения применяется только к тому же документу", () => {
    assert.equal(sameIncomingDocument("d1", "d1"), true);
    assert.equal(sameIncomingDocument("d2", "d1"), false);
    assert.equal(sameIncomingDocument(null, "d1"), false);
});
```

Импорт заменить: `editedActualVolume`, `canQuenchRows`, `sameIncomingDocument`. `clampActualVolume` удалить из импорта и из модуля.

- [x] **Step 2: Прогнать**

Run: `node --test src/tests/WebApp.Tests/vetisFormat.test.mjs`

Expected: FAIL, `editedActualVolume is not a function`.

- [x] **Step 3: Функции в vetisFormat.js**

`clampActualVolume` удалить. На его место:

```javascript
export function editedActualVolume(incoming, edited) {
    const volume = parseVolume(incoming);
    const text = String(edited ?? "").trim();
    const fact = parseVolume(text);
    if (!text || volume == null || fact == null || fact < 0 || fact > volume)
        return { ok: false };

    return { ok: true, value: text.replace(",", ".") };
}

export function canQuenchRows(vsds) {
    return (vsds ?? []).some((vsd) => String(vsd?.status ?? "").toUpperCase() === "CONFIRMED");
}

export function sameIncomingDocument(openedId, requestId) {
    return openedId != null && openedId === requestId;
}
```

- [x] **Step 4: Ячейка и кнопка**

В `onAfterEditStop` колонки `actualVolume`:

```javascript
const edited = editedActualVolume(item.volume, state.value);
if (!edited.ok) {
    item.actualVolume = state.old;
    this.updateItem(item.id, item);
    webix.message({ type: "error", text: "Фактическое количество должно быть от 0 до объёма поступления." });
    return;
}
item.actualVolume = edited.value;
this.updateItem(item.id, item);
```

Кнопка «Погасить»: `disabled: !canQuenchRows(incoming.vsds)`. Иначе уже принятый документ даёт только «Нет ВСД для гашения.»

- [x] **Step 5: Прогнать node-тест**

Expected: PASS.

---

### Task 3: Сохранение не затирает ВСД, дописанный во время гашения

`Find` один раз, `Save` пишет этот объект с новым `_rev`. Загрузка за те же минуты добавляет ВСД в CouchDB, следующее сохранение стирает его. Курсор уже сдвинут, строка сама не вернётся.

**Files:**
- Modify: `src/Core/Application/Vetis/VetisIncomingQuenchService.cs`
- Test: `src/tests/Application.Tests/VetisIncomingQuenchServiceTests.cs`

**Interfaces:**
- Consumes: `IVetisIncomingRepository.Find` / `Save`. `MemoryVetisIncoming.Find` отдаёт клон, `OnQuench` у шлюза вызывается до возврата из `QuenchAsync`.

- [x] **Step 1: Тест дописанного ВСД**

```csharp
[Fact]
public async Task Quench_не_стирает_ВСД_дописанный_пока_гашение_шло()
{
    var world = new World();
    world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"), Vsd("v2", "CONFIRMED", "10"));
    world.Gateway.EnqueueQuench(Done("v1"));
    world.Gateway.EnqueueQuench(Done("v2"));
    world.Gateway.OnQuench = () =>
    {
        if (world.Gateway.QuenchCalls != 1)
            return;

        world.Incoming.Store["d1"].Vsds.Add(Vsd("v-new", "CONFIRMED", "1"));
    };

    var report = (await world.Sut.QuenchAsync("d1", [], CancellationToken.None)).Value;

    var saved = world.Incoming.Store["d1"];
    Assert.Equal(3, saved.Vsds.Count);
    Assert.Equal("UTILIZED", saved.Vsds.Single(item => item.Uuid == "v1").Status);
    Assert.Equal("UTILIZED", saved.Vsds.Single(item => item.Uuid == "v2").Status);
    Assert.Equal("CONFIRMED", saved.Vsds.Single(item => item.Uuid == "v-new").Status);
    Assert.Equal(2, report.Quenched);
}
```

`v-new` в этот запрос не гасится: его не было в снимке, с которым пользователь нажал «Погасить».

В том же шаге на `MemoryVetisIncoming` добавить `string? FindError`: если задано, `Find` возвращает `Result.Failure(FindError)`. Второй тест: в `OnQuench` первого вызова выставить `FindError = "CouchDB недоступна"`. Ожидание: `IsFailure`, текст содержит «CouchDB недоступна» и не содержит «уже нет», `v1` остаётся `CONFIRMED`. До исхода `QuenchStored` этот тест не отличить от пропажи uuid — он и фиксирует границу.

- [x] **Step 2: Прогнать**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter "FullyQualifiedName~Quench_не_стирает_ВСД" --nologo`

Expected: FAIL, `v-new` отсутствует, `Vsds.Count == 2`.

- [x] **Step 3: Перед Save читать документ заново**

После успешного ответа Меркурия или ветки «уже погашен» не вызывать `document.Merge` на стартовом объекте. Итог записи — свой тип, не текст `Result.Failure`: ошибка `Find` и пропавший uuid текстом не различаются.

```csharp
private enum QuenchStore
{
    Saved,
    Missing,
    Failed
}

private readonly record struct QuenchStored(QuenchStore Outcome, string Error);

private async Task<QuenchStored> StoreQuench(
    string documentId,
    string uuid,
    Action<VetisIncomingVsd> apply)
{
    var fresh = await _incoming.Find(documentId);
    if (fresh.IsFailure)
        return new QuenchStored(QuenchStore.Failed, fresh.Error);
    if (fresh.Value == null)
        return new QuenchStored(QuenchStore.Missing, "");

    var target = fresh.Value.Vsds.FirstOrDefault(item =>
        string.Equals(item.Uuid, uuid, StringComparison.OrdinalIgnoreCase));
    if (target == null)
        return new QuenchStored(QuenchStore.Missing, "");

    apply(target);
    fresh.Value.Merge([target]);
    var saved = await _incoming.Save(fresh.Value);
    return saved.IsFailure
        ? new QuenchStored(QuenchStore.Failed, saved.Error)
        : new QuenchStored(QuenchStore.Saved, "");
}
```

`apply` выставляет статус, и при обычном успехе — `ActualVolume` и `ReceivedAt`. Вызывающий:

- `Saved` — `report.Quenched++`, следующий uuid.
- `Missing` — в отчёт «ВСД погашен в Меркурии, но в документе его уже нет.», следующий uuid. Сюда попадает и `Find == null`, и uuid, который загрузка уже перенесла.
- `Failed` — прервать отчёт: «ВСД погашены, но документ не сохранён: » + `Error`. Сбой базы здесь не называется пропажей ВСД.

Цикл по-прежнему идёт по `document.Vsds.ToList()` стартового снимка: оттуда берутся uuid, объём, `RawXml` и номер. В Меркурий уходит объём, который видел пользователь.

- [x] **Step 4: Прогнать все тесты гашения сервиса**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter "FullyQualifiedName~VetisIncomingQuenchServiceTests" --nologo`

Expected: PASS, включая прежние тесты поштучного сохранения и обрыва.

---

### Task 4: Второй запрос на тот же документ не идёт в Меркурий

Прогресс один на id. `Clear` первого запроса стирает стадию второго, два `processIncomingConsignment` пишут факт друг поверх друга.

**Files:**
- Modify: `src/Core/Application/Vetis/VetisQuenchProgressStore.cs`
- Modify: `src/Core/Application/Vetis/VetisIncomingQuenchService.cs`
- Modify: `src/tests/Application.Tests/Fakes/VetisFakes.cs`
- Test: `src/tests/Application.Tests/VetisIncomingQuenchServiceTests.cs`

**Interfaces:**
- Produces: `Task<bool> TryEnter(string documentId, CancellationToken cancellationToken)`, `void Exit(string documentId)` на `VetisQuenchProgressStore`.

- [x] **Step 1: Удержание шлюза и тест второго запроса**

В `ScriptedVetisGateway` добавить `TaskCompletionSource Entered` и `TaskCompletionSource Release`. В `QuenchAsync` после `QuenchCalls++` вызвать `Entered.TrySetResult()` и `await Release.Task`, если `Hold` равен true. По умолчанию `Hold` выключен, старые тесты не ждут.

```csharp
[Fact]
public async Task Quench_повтор_того_же_документа_не_отправляется()
{
    var world = new World();
    world.Incoming.Store["d1"] = Document(Vsd("v1", "CONFIRMED", "10"));
    world.Gateway.Hold = true;
    world.Gateway.EnqueueQuench(Done("v1"));

    var first = world.Sut.QuenchAsync("d1", [], CancellationToken.None);
    await world.Gateway.Entered.Task;
    var second = await world.Sut.QuenchAsync("d1", [], CancellationToken.None);
    world.Gateway.Release.TrySetResult();
    var report = (await first).Value;

    Assert.True(second.IsFailure);
    Assert.Equal("Гашение уже выполняется.", second.Error);
    Assert.Equal(1, world.Gateway.QuenchCalls);
    Assert.Equal(1, report.Quenched);
    Assert.Null(world.Progress.Find("d1"));
}
```

- [x] **Step 2: Прогнать**

Expected: FAIL, второй запрос тоже вызывает шлюз либо тест не компилируется без `Hold`.

- [x] **Step 3: Замок в singleton**

В хранилище рядом со словарём прогресса:

```csharp
private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

public Task<bool> TryEnter(string documentId, CancellationToken cancellationToken) =>
    _gates.GetOrAdd(documentId, _ => new SemaphoreSlim(1, 1)).WaitAsync(0, cancellationToken);

public void Exit(string documentId)
{
    if (_gates.TryGetValue(documentId, out var gate))
        gate.Release();
}
```

`Wait(0)` в async-методе не использовать: это блокирующее ожидание. Семафор не удалять. `TryRemove` гоняется с `WaitAsync` и может отпустить чужой вход. Словарь живёт до остановки процесса и растёт на каждый id, который гасили с запуска. Это не чистится: отдельный сборщик снова попадает в ту же гонку. Комментарий на классе говорит оба ограничения.

В `QuenchAsync` сразу после входа:

```csharp
if (!await _progress.TryEnter(documentId, cancellationToken))
    return Result.Failure<VetisQuenchReport>("Гашение уже выполняется.");

try
{
    // текущее тело, включая ранние отказы
}
finally
{
    _progress.Clear(documentId);
    _progress.Exit(documentId);
}
```

Ранние `return` остаются внутри `try`, чтобы `Exit` выполнился. Второго try внутри нет.

- [x] **Step 4: Прогнать тесты сервиса**

Expected: PASS. Прогресс после обоих запросов пуст, первый дописывает `UTILIZED`.

---

### Task 5: «Уже погашен» — полная фраза, факт не переписывается

`Contains("не погашен")` принимает любую ошибку с этими словами. Ветка всё равно пишет `ActualVolume` текущей попытки: повтор с частичным фактом подменяет объём уже принятой партии.

Та же официальная фраза Меркурия приходит и на другой статус, не только на повторное гашение. Отличить аннулирование по тексту нельзя. Поэтому чужой текст с обрывком «не погашен» остаётся ошибкой строки и статусом `CONFIRMED`. Совпавшая полная фраза по-прежнему ставит `UTILIZED`, но `actualVolume` и `receivedAt` не меняет.

**Files:**
- Modify: `src/Core/Application/Vetis/VetisIncomingQuenchService.cs`
- Test: `src/tests/Application.Tests/VetisIncomingQuenchServiceTests.cs`

- [x] **Step 1: Два теста**

Расширить `Quench_уже_погашенный_в_Меркурии_пишет_статус_без_повтора_ошибки`: в facts у `v1` факт `"4"`. После гашения `ActualVolume` у `v1` пустой, `ReceivedAt` пустой, статус `UTILIZED`.

Второй вызов того же теста (или отдельный факт): текст с ёлочками и лишними пробелами — `Ветеринарно-сопроводительный документ должен быть в состоянии «оформлен»  (т.е.  не погашен).` — тоже `UTILIZED` без факта.

Новый тест: ошибка `"партия не погашена из-за акта"`. Статус `v1` остаётся `CONFIRMED`, uuid есть в `report.Errors`, `Quenched == 1` только за `v2`.

- [x] **Step 2: Прогнать**

Expected: FAIL. Сейчас факт становится `"4"`, а обрывок «не погашен» считается успехом.

- [x] **Step 3: Сравнение и запись**

```csharp
private const string AlreadyUtilized =
    "должен быть в состоянии \"оформлен\" (т.е. не погашен)";

private static bool AlreadyQuenched(string? error) =>
    NormalizeMercury(error).Contains(AlreadyUtilized, StringComparison.OrdinalIgnoreCase);

private static string NormalizeMercury(string? error)
{
    var text = (error ?? "")
        .Replace('«', '"')
        .Replace('»', '"')
        .Replace('“', '"')
        .Replace('”', '"');
    return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
```

Прямые кавычки, ёлочки и повторные пробелы сводятся к одной фразе. Обрывок «не погашен» без «оформлен» по-прежнему ошибка строки. В ветке `else` (не `Completed`, но фраза совпала) ставить только `Status = "UTILIZED"`. `ActualVolume` и `ReceivedAt` не трогать. Комментарий метода оставить: он объясняет, почему эта фраза не показывается пользователю.

- [x] **Step 4: Прогнать тесты сервиса**

Expected: PASS.

---

### Task 6: В заявку уходит ТТН связи 1 и дата доставки со смещением

`ReferencedIncomingDocument.Read` берёт тип 1–5 со связью `relationshipType = 1`. `Waybill` берёт первый тип 1–5 с датой. Если раньше стоит связь 2, в Меркурий уходит не та накладная, по которой собран документ.

`deliveryDate` сейчас `DateTime.Now` без пояса. Остальные заявки идут через `VetisDateTime.Format`. У границы суток Меркурий читает другую дату.

**Files:**
- Modify: `src/Infrastructure/VetisApi/Xml/ProcessIncomingConsignmentXml.cs`
- Test: `src/tests/Application.Tests/VetisQuenchXmlTests.cs`

- [x] **Step 1: Тест двух накладных и пояса**

В копию `SampleVsd` вставить перед текущим `referencedDocument` документ связи 2: тип `2`, номер `ТН-7`, дата `2026-10-01`. Существующий блок связи не имеет `relationshipType` — дописать ему `<vd:relationshipType>1</vd:relationshipType>`, номер оставить `ТТН-9`.

Ожидание для `ACCEPT_ALL`: XML содержит `ТТН-9` и не содержит `ТН-7`.

Отдельный факт: элемент `deliveryDate` подходит под

```text
\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}
```

Дата акта `2026-10-30` из аргумента `DateOnly` не меняется.

- [x] **Step 2: Прогнать**

Expected: FAIL, в XML есть `ТН-7`, у `deliveryDate` нет пояса.

- [x] **Step 3: Выбор как в домене**

`Waybill` собирает элементы `referencedDocument` с типом 1–5. Берёт первый со связью `1`, иначе первый. Нет ни одного — `Result.Success<XElement?>(null)`. У выбранного пустая `issueDate` — прежняя ошибка «связанный транспортный документ без даты». Не переходить к следующей накладной: иначе снова уедет не та связь.

`deliveryDate`: `VetisDateTime.Format(DateTimeOffset.Now)`.

- [x] **Step 4: Прогнать `VetisQuenchXmlTests`**

Expected: PASS. Прежние тесты `ТТН-9` остаются зелёными.

---

### Task 7: TRANSPORT только если грузополучатель — площадка загрузки

Поставка на площадку приходит как `TRANSPORT`. Список изменений запрашивается по предприятию без роли грузополучателя, поэтому исходящая перевозка той же площадки попадает в журнал и на «Погасить». `INCOMING` оставляем как есть: в этом поле он почти не встречается, и текущие тесты держат его без guid.

**Files:**
- Modify: `src/Infrastructure/VetisApi/Xml/VetisResponseReader.cs`
- Modify: `src/Infrastructure/VetisApi/VetisGateway.cs`
- Test: `src/tests/Application.Tests/VetisGatewayTests.cs`

**Interfaces:**
- Produces: `ReadIncoming(string xml, string enterpriseGuid)`

- [x] **Step 1: Поменять все три утверждения фикстуры**

В фикстуру к `v-4` добавить `certifiedConsignment/consignee/enterprise/guid` = `ffffffff-ffff-ffff-ffff-ffffffffffff`. Добавить `v-7`: `vetDType` = `TRANSPORT`, guid грузополучателя = `eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee`.

`ReadIncoming_читает_входящие_ВСД_с_накладной_и_пропускает_исходящие` вызывает `ReadIncoming(IncomingResponse, "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")`:

- `Assert.Equal(["v-1", "v-6", "v-7"], ...)` — `v-4` чужая площадка.
- `ListedCount` становится `7`: в списке появился `v-7`, счётчик считает все `vetDocument`, не отфильтрованные.

`GetIncoming_шлёт_окно_и_площадку_и_отдаёт_ВСД` площадку не меняет: `EnterpriseGuid = "enterprise-1"`. Для этого вызова `v-7` тоже чужой:

- `Assert.Equal(7, result.PageCount)`.
- `Assert.Equal(["v-1", "v-6"], result.Documents.Select(...))`.

`INCOMING` (`v-1`, `v-6`) остаётся без сверки guid. Комментарий теста чтения: TRANSPORT остаётся, только если guid предприятия грузополучателя совпадает с площадкой запроса.

- [x] **Step 2: Прогнать**

Expected: FAIL. Сигнатура `ReadIncoming` ещё без guid, либо списки uuid и `PageCount` не совпали с тремя утверждениями выше. Оставлять в `GetIncoming` ожидание `["v-1", "v-6", "v-7"]` нельзя: туда уходит `enterprise-1`.

- [x] **Step 3: Фильтр**

`GetIncomingAsync` вызывает `ReadIncoming(exchange.ResponseXml, request.EnterpriseGuid)`.

Для `INCOMING` сравнение guid не делать. Для `TRANSPORT` читать guid так же, как имя в `PartyName`: потомок `consignee`, потомок `enterprise`, прямой элемент `guid`. Совпадение `OrdinalIgnoreCase`. Пустой guid площадки или грузополучателя — документ не брать.

- [x] **Step 4: Прогнать `VetisGatewayTests`**

Expected: PASS.

---

### Task 8: Сбой подсчёта непринятых не выглядит как ноль

`NotAcceptedKeys` при ошибке превращается в пустой словарь, контроллер отвечает 200. Фильтр затирает прежние числа, строки «Не принят» остаются. `vetisIncomingTab._loadCounts` и `vetisView` уже не вызывают `apply` при отказе сети (`catch` и проверка `if (organizations)`). Менять JS не нужно: достаточно ответа 400.

**Files:**
- Modify: `src/Core/Application/Vetis/VetisIncomingQueryService.cs`
- Modify: `src/Presentation/WebApi/Controllers/VetisController.cs`
- Modify: `src/tests/Application.Tests/Fakes/VetisFakes.cs`
- Test: `src/tests/Application.Tests/VetisQueryServicesTests.cs`

**Interfaces:**
- Produces: `Task<Result<List<VetisFilterOrganization>>> NotAcceptedCountsAsync()`

- [x] **Step 1: Тест ошибки базы**

На `MemoryVetisIncoming` свойство `string? NotAcceptedError`. Если оно задано, `NotAcceptedKeys` возвращает `Result.Failure`.

```csharp
[Fact]
public async Task NotAcceptedCounts_отдаёт_ошибку_базы_а_не_нули()
{
    var world = PendingDocuments();
    world.Incoming.NotAcceptedError = "CouchDB недоступна";

    var result = await world.Sut.NotAcceptedCountsAsync();

    Assert.True(result.IsFailure);
    Assert.Equal("CouchDB недоступна", result.Error);
}
```

Существующий тест успеха перевести на `.Value`.

- [x] **Step 2: Прогнать**

Expected: FAIL, метод возвращает список, не `Result`.

- [x] **Step 3: Проброс**

```csharp
public async Task<Result<List<VetisFilterOrganization>>> NotAcceptedCountsAsync()
{
    var keys = await _incoming.NotAcceptedKeys();
    if (keys.IsFailure)
        return Result.Failure<List<VetisFilterOrganization>>(keys.Error);

    var counts = keys.Value
        .GroupBy(item => (item.OrganizationId, Guid: item.EnterpriseGuid), KeyComparer.Instance)
        .ToDictionary(group => group.Key, group => group.Count(), KeyComparer.Instance);
    return Result.Success(await DirectoryAsync(counts));
}
```

Контроллер, как у `Incoming`:

```csharp
public async Task<IActionResult> NotAcceptedCounts()
{
    var result = await _incoming.NotAcceptedCountsAsync();
    return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
}
```

- [x] **Step 4: Прогнать `VetisQueryServicesTests`**

Expected: PASS.

---

### Task 9: Подсчёт непринятых листается по ключу, без skip

Цикл `skip` по 500 на каждом обновлении фильтра дорожает с каждой страницей. Потолок 500 × 500 ещё и упирается в 400, а `vetisIncomingTab._loadCounts` тогда молча оставляет старые числа. Map/reduce в проекте нет, отдельный view только ради счётчика не заводим.

Страница ищется по равенству `data.status` и диапазону `data.id`. `data.id` совпадает с `_id` документа. Индекс `status-id-idx`: `data.status`, `data.id`. Размер страницы 2000. `skip` в запросе нет. Предел 500 страниц остаётся только чтобы цикл не крутился, если CouchDB всегда отдаёт полную страницу; каждая страница при этом чтение от ключа, а не отступ от начала.

**Files:**
- Modify: `src/Infrastructure/CouchDb/Repositories/VetisIncomingQuery.cs`
- Modify: `src/Infrastructure/CouchDb/Repositories/VetisIncomingRepository.cs`
- Modify: `src/Infrastructure/CouchDb/DatabaseScheme/DatabaseIndexes.cs`
- Test: `src/tests/CouchDb.Tests/VetisQueriesTests.cs`

**Interfaces:**
- Produces: `VetisIncomingQuery.NotAcceptedAfter(string afterId, int limit)`
- Потребляет существующий `NotAcceptedKeys`. Старый `NotAccepted(skip, limit)` удалить, тест словаря перевести на `NotAcceptedAfter`.

- [x] **Step 1: Тест словаря**

Заменить проверки `NotAccepted(0, 500)`:

```csharp
var pending = VetisIncomingQuery.NotAcceptedAfter("doc-9", 2000);
Assert.False(pending.ContainsKey("skip"));
var selector = Assert.IsType<Dictionary<string, object>>(pending["selector"]);
Assert.Equal("notAccepted", selector["data.status"]);
var id = Assert.IsType<Dictionary<string, object>>(selector["data.id"]);
Assert.Equal("doc-9", id["$gt"]);
Assert.Equal(new[] { "vetis-incoming", "status-id-idx" }, Assert.IsType<string[]>(pending["use_index"]));
Assert.Equal(2000, pending["limit"]);
var fields = Assert.IsType<string[]>(pending["fields"]);
Assert.Contains("data.id", fields);
Assert.DoesNotContain("data.vsds", fields);

var schema = DatabaseIndexes.DatabaseIndexSchema()[DatabaseNames.VetisIncoming];
Assert.Equal(
    ["data.status", "data.id"],
    schema.Single(index => index.Name == "status-id-idx").Index.Fields);
```

- [x] **Step 2: Прогнать**

Run: `dotnet test src/tests/CouchDb.Tests/CouchDb.Tests.csproj --filter "FullyQualifiedName~VetisQueriesTests" --nologo`

Expected: FAIL, метода `NotAcceptedAfter` нет.

- [x] **Step 3: Запрос и репозиторий**

```csharp
public static Dictionary<string, object> NotAcceptedAfter(string afterId, int limit) =>
    new()
    {
        ["selector"] = new Dictionary<string, object>
        {
            ["data.status"] = VetisIncomingStatus.NotAccepted,
            ["data.id"] = new Dictionary<string, object> { ["$gt"] = afterId ?? "" }
        },
        ["sort"] = new[]
        {
            new Dictionary<string, string> { ["data.status"] = "asc" },
            new Dictionary<string, string> { ["data.id"] = "asc" }
        },
        ["fields"] = new[] { "data.id", "data.organizationId", "data.enterpriseGuid" },
        ["use_index"] = new[] { DesignDoc, "status-id-idx" },
        ["limit"] = limit
    };
```

В `VetisIncomingIndexSchema` добавить `new("status-id-idx", new(["data.status", "data.id"]), VetisIncomingQuery.DesignDoc)`.

`NotAcceptedKeys`: `afterId = ""`, `pageSize = 2000`, не больше 500 страниц. Следующий `afterId` — `Id` последней строки. Короткая страница — успех. После предела при полной странице — `Result.Failure("Страниц больше 500.")`. `skip` не передавать.

- [x] **Step 4: Прогнать `VetisQueriesTests`**

Expected: PASS.

---

### Task 10: Старый sortAt выравнивается страницами до бюджета за круг

`SortAt` теперь полночь UTC даты документа. Уже лежащие строки хранят unix времени получения. Журнал смешивает два смысла ключа, пока строку не перезапишет загрузка или гашение.

Один проход `Find`+`Save` по всему журналу до первой загрузки не делаем. Список уже содержит `date` и `sortAt`, поэтому полный документ читается только если ключ не совпал с `DocumentSortAt`. Страница идёт по `data.id`, не по `sortAt`: после выравнивания у многих документов одна полночь, и `skip` по этому полю дублирует и пропускает строки. По `_id` курсор тоже не годится: в базе лежат `_design/*`, они попадают под `_id $gt ""`, а `UniversalDocument.Data` объявлен `required` — строка без `data` роняет разбор, и воркер уходит в вечный повтор с остановленной загрузкой. У design-документов нет `data.id`, поэтому курсор по нему их отсекает.

`VetisApi` не ссылается на Application. Воркер берёт `IVetisIncomingSortAligner` из Domain. Класс реализует только этот интерфейс: регистратор пишет в DI первый интерфейс типа.

Курсор нельзя держать в scoped-сервисе: воркер на каждый круг создаёт новый scope. `afterId` и флаг окончания живут в полях воркера.

**Files:**
- Modify: `src/Core/Domain/Vetis/VetisIncomingDocument.cs`
- Modify: `src/Core/Domain/Vetis/IVetisBackgroundJobs.cs`
- Modify: `src/Core/Domain/Vetis/IVetisIncomingRepository.cs`
- Modify: `src/Infrastructure/CouchDb/Repositories/VetisIncomingQuery.cs`
- Modify: `src/Infrastructure/CouchDb/Repositories/VetisIncomingRepository.cs`
- Modify: `src/Infrastructure/CouchDb/DatabaseScheme/DatabaseIndexes.cs`
- Modify: `src/tests/Application.Tests/Fakes/VetisFakes.cs`
- Create: `src/Core/Application/Vetis/VetisIncomingSortAligner.cs`
- Modify: `src/Infrastructure/VetisApi/Workers/VetisIncomingWorker.cs`
- Test: `src/tests/Domain.Tests/VetisIncomingTests.cs`
- Test: `src/tests/Application.Tests/VetisIncomingSortAlignerTests.cs`
- Test: `src/tests/CouchDb.Tests/VetisQueriesTests.cs`

**Interfaces:**
- Produces: `public static long DocumentSortAt(string date)`, `bool AlignSortAt()`.
- Produces: `readonly record struct VetisIncomingHead(string Id, string Date, long SortAt)` в Domain.
- Produces: `readonly record struct VetisScanStep(bool Finished, string AfterId)` в Domain.
- Produces: `IVetisIncomingRepository.HeadsAfter(string afterId, int limit)`.
- Produces: `IVetisIncomingSortAligner.AlignBatchAsync(string afterId, CancellationToken) -> Task<Result<VetisScanStep>>`.

- [x] **Step 1: Тест домена**

Документ с `Date = "2026-10-01"`, `SortAt = 12345`, одним ВСД. `AlignSortAt()` возвращает true, `SortAt` равен `VetisIncomingDocumentEntity.DocumentSortAt("2026-10-01")` (в тесте уже есть `DocumentDay`). Статус и состав ВСД те же. Второй вызов возвращает false. `DocumentSortAt("")` равен 0.

- [x] **Step 2: Прогнать доменный тест**

Expected: FAIL, метода нет.

- [x] **Step 3: Открыть формулу ключа**

`DocumentSortAt` сделать `public static`. Тело не менять.

```csharp
public bool AlignSortAt()
{
    var aligned = DocumentSortAt(Date);
    if (SortAt == aligned)
        return false;

    SortAt = aligned;
    return true;
}
```

`Refresh` по-прежнему пишет ключ сам. `AlignSortAt` не вызывает `Refresh`.

- [x] **Step 4: Тест одной страницы**

На `MemoryVetisIncoming` счётчики `FindCalls` и `SaveCalls`. `HeadsAfter` отдаёт документы с `Id` больше `afterId`, по возрастанию `Id`, не больше `limit`, поля `Id`, `Date`, `SortAt`, без ВСД.

Два документа. `a`: дата `2026-10-01`, `SortAt` уже равен `DocumentSortAt`, ВСД `v1`. `b`: та же дата, `SortAt = 50`, ВСД `v2`.

```csharp
var step = (await aligner.AlignBatchAsync("", CancellationToken.None)).Value;
```

Ожидание: `step.Finished`, `step.AfterId == "b"`, `FindCalls == 1`, `SaveCalls == 1`, у `b` ключ равен полночи, `v2` на месте, у `a` ВСД не пропал.

- [x] **Step 5: Прогнать**

Expected: FAIL, типа `IVetisIncomingSortAligner` нет.

- [x] **Step 6: Страница и сервис**

`VetisIncomingQuery.HeadsAfter(afterId, limit)`: селектор `data.id` `$gt` afterId, сортировка `data.id` asc, поля `data.id`, `data.date`, `data.sortAt`, `limit`, `use_index` — `["vetis-incoming", "id-idx"]`. Ключа `skip` нет. `data.sortAt` в селекторе нет: индекс журнала по дате здесь не используется, ключом страницы служит `data.id`.

В `VetisIncomingIndexSchema` добавить `new("id-idx", new(["data.id"]), VetisIncomingQuery.DesignDoc)`.

Репозиторий собирает `VetisIncomingHead` из `Id`, `Date`, `SortAt`.

```csharp
[AutoRegisterService(ServiceLifetime.Scoped)]
public sealed class VetisIncomingSortAligner : IVetisIncomingSortAligner
{
    public const int PageSize = 200;

    public async Task<Result<VetisScanStep>> AlignBatchAsync(string afterId, CancellationToken cancellationToken)
    {
        var page = await _incoming.HeadsAfter(afterId ?? "", PageSize);
        if (page.IsFailure)
            return Result.Failure<VetisScanStep>(page.Error);

        foreach (var head in page.Value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (head.SortAt == VetisIncomingDocumentEntity.DocumentSortAt(head.Date))
                continue;

            var found = await _incoming.Find(head.Id);
            if (found.IsFailure)
                return Result.Failure<VetisScanStep>(found.Error);
            if (found.Value == null || !found.Value.AlignSortAt())
                continue;

            var saved = await _incoming.Save(found.Value);
            if (saved.IsFailure)
                return Result.Failure<VetisScanStep>(saved.Error);
        }

        var last = page.Value.Count == 0 ? afterId ?? "" : page.Value[^1].Id;
        return Result.Success(new VetisScanStep(page.Value.Count < PageSize, last));
    }
}
```

Сбой на середине страницы не двигает курсор воркера: метод возвращает `Failure`, уже записанные строки на следующем круге дают `AlignSortAt() == false`.

Тест словаря в `VetisQueriesTests`: нет `skip`, есть `$gt` по `data.id`, `use_index` на `id-idx` и сам `id-idx` в схеме индексов.

- [x] **Step 7: Воркер — страницы до бюджета и короткий повтор**

Поля `_sortAfter` и `_sortDone`, `private static readonly TimeSpan ScanBudget = TimeSpan.FromSeconds(10);`. В начале круга `delay = RetryDelay` (уже есть, одна минута). Строку `delay = settings.IncomingInterval();` из начала круга удалить: пока выравнивание не кончилось, пауза остаётся короткой. Одна страница на круг при минутной паузе держит журнал без обновления часами: 200 строк на 20 000 документов — это сто минут.

```csharp
if (!_sortDone)
{
    var budget = Stopwatch.StartNew();
    while (!_sortDone && budget.Elapsed < ScanBudget)
    {
        var aligned = await scope.ServiceProvider
            .GetRequiredService<IVetisIncomingSortAligner>()
            .AlignBatchAsync(_sortAfter, stoppingToken);
        if (aligned.IsFailure)
            throw new InvalidOperationException(aligned.Error);

        _sortAfter = aligned.Value.AfterId;
        _sortDone = aligned.Value.Finished;
    }
}

if (_sortDone)
{
    if (settings.Enable)
        await scope.ServiceProvider.GetRequiredService<IVetisIncomingLoader>().LoadAsync(stoppingToken);

    // Только после успешной загрузки: сбой оставляет короткий повтор, а не интервал журнала.
    delay = settings.IncomingInterval();
}
```

Исключение ловит существующий `catch`. Пока выравнивание не закончилось или круг упал, `LoadAsync` не вызывается и пауза остаётся `RetryDelay`, не интервал загрузки. Истёкший бюджет не ошибка: следующая страница пойдёт на следующем круге. Второго try нет. Конкретный `VetisIncomingSortAligner` из воркера не резолвить.

Интервал ставится после успешного `LoadAsync`, а не до него: иначе исключение из загрузки оставляет `delay` равным интервалу журнала (по умолчанию 5 минут, в настройках бывает больше), и сбой загрузки ждёт его вместо минуты. При выключенной загрузке интервал ставится сразу — вызова, который может упасть, нет. Task 12 заменит условие на `_sortDone && _transportDone`.

- [x] **Step 8: Прогнать тесты выравнивания, домена и запросов**

Expected: PASS.

---

### Task 11: Esc и чужой отчёт не садятся в следующее окно

Esc висит на `UIManager` без привязки к окну. `dialog.disable()` его не снимает. Окно одно (`vetisIncomingWindow`). После закрытия и открытия другой накладной поздний ответ вызывает `applyQuenchReport` и `enable` уже у нового окна. Ошибка HTTP не обновляет журнал, хотя часть ВСД уже записана.

**Files:**
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/utils/vetisFormat.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisWindow.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisIncomingWindow.js`
- Test: `src/tests/WebApp.Tests/vetisFormat.test.mjs`

**Interfaces:**
- Consumes: `sameIncomingDocument`, `canQuenchRows` из Task 2.
- Produces: `incomingWindowClosable(openedId, busyId) -> boolean`.

- [x] **Step 1: Тест владения окном**

В `vetisFormat.test.mjs`:

```javascript
test("Esc закрыт только у документа, который сейчас гасится", () => {
    assert.equal(incomingWindowClosable("d1", null), true);
    assert.equal(incomingWindowClosable("d1", "d1"), false);
    assert.equal(incomingWindowClosable("d2", "d1"), true);
});
```

Run: `node --test src/tests/WebApp.Tests/vetisFormat.test.mjs`

Expected: FAIL, функции нет.

```javascript
export function incomingWindowClosable(openedId, busyId) {
    return busyId == null || busyId !== openedId;
}
```

Повторный прогон: PASS.

- [x] **Step 2: canClose привязан к id запроса, не к модульному флагу**

`openVetisWindow` принимает `canClose`. В `closeOnEsc` и в `click` крестика: если `canClose` задан и вернул false, окно не закрывать (`return false` у Esc).

В модуле окна `openedId` и `busyId`. При открытии `openedId = incoming.id`. `canClose: () => incomingWindowClosable(openedId, busyId)`.

Общий `quenching = true` не использовать: он держится до `finally` и закрывает Esc уже у следующего документа.

- [x] **Step 3: Отчёт, стадия и enable сверяют id**

`quench` запоминает `const requestId = documentId`, ставит `busyId = requestId` до `await`. В `finally` снимает `busyId`, только если он всё ещё равен `requestId` (новый запрос уже мог занять флаг). `hideQuenchStage` и `enable` вызывать только если `sameIncomingDocument(openedId, requestId)` и окно ещё есть.

`applyQuenchReport(report, requestId)` в начале выходит, если документ уже другой.

`watchQuench` в `pull` после проверки `progress.running` тоже выходит, если `!sameIncomingDocument(openedId, documentId)`. Иначе поздний опрос пишет стадию в новое окно.

В `catch` после сообщения вызвать `onQuenched`, если он есть: список подтянет ВСД, которые успели сохраниться. При успехе оставить один вызов `onQuenched` после отчёта, без второго в `finally`. Кнопку «Погасить» эта задача не трогает: её выключает Task 2.

- [x] **Step 4: Прогнать node-тесты Ветис**

Run: `node --test src/tests/WebApp.Tests/vetisFormat.test.mjs src/tests/WebApp.Tests/vetisFilterBar.test.mjs`

Expected: PASS.

Ручная проверка окна, когда поднят WebApp: неверный факт возвращает прежнее число и показывает сообщение; у принятого документа кнопка «Погасить» недоступна; Esc во время гашения окно не закрывает; закрытие после ответа обновляет журнал. Автотеста на Webix в репозитории нет.

---

### Task 12: Уже лежащий исходящий TRANSPORT снять со журнала

Фильтр `ReadIncoming` не трогает документы, которые уже лежат в CouchDB. Именно они видны в журнале и дают «Погасить». Проход отдельный от Task 10: список не содержит `rawXml`, поэтому `Find` нужен каждой строке страницы, а не только расхождению `sortAt`.

Чужой TRANSPORT — `vetDType` равен `TRANSPORT`, а guid предприятия в `consignee` не совпадает с `EnterpriseGuid` документа. `INCOMING` и TRANSPORT на свою площадку остаются. Пустой guid грузополучателя тоже снимается: иначе исходящая перевозка без площадки останется в журнале. Битый XML не снимается. Правило то же, что у фильтра Task 7: два места обязаны совпадать, иначе один проход хранит то, что другой снимает.

Удаление необратимо, поэтому каждая страница оставляет запись в журнале воркера: сколько ВСД снято и сколько документов удалено. След погашения, снятого вместе с документом, остаётся в «Заявках» — там лежит обмен с Меркурием.

**Files:**
- Modify: `src/Core/Domain/Vetis/VetisIncomingDocument.cs`
- Modify: `src/Core/Domain/Vetis/IVetisBackgroundJobs.cs`
- Modify: `src/Core/Domain/Vetis/IVetisIncomingRepository.cs`
- Create: `src/Core/Domain/Vetis/VetisIncomingShipment.cs`
- Create: `src/Core/Application/Vetis/VetisIncomingTransportSweep.cs`
- Modify: `src/Infrastructure/VetisApi/Workers/VetisIncomingWorker.cs`
- Modify: `src/Infrastructure/CouchDb/Repositories/VetisIncomingRepository.cs`
- Modify: `src/tests/Application.Tests/Fakes/VetisFakes.cs`
- Test: `src/tests/Domain.Tests/VetisIncomingTests.cs`
- Test: `src/tests/Application.Tests/VetisIncomingTransportSweepTests.cs`

**Interfaces:**
- Produces: `bool DropForeignTransport()` на документе.
- Produces: `IVetisIncomingRepository.Delete(string id)`.
- Produces: `IVetisIncomingTransportSweep.SweepBatchAsync(string afterId, CancellationToken) -> Task<Result<VetisScanStep>>`. Единственный интерфейс класса. Страница — `HeadsAfter`, размер 200.
- Consumes: `VetisScanStep`, `HeadsAfter` из Task 10.

- [x] **Step 1: Тест домена**

Документ площадки `e1`. Три ВСД:

- `INCOMING` без guid — остаётся.
- `TRANSPORT`, consignee `e1` — остаётся.
- `TRANSPORT`, consignee `e2` — снимается.
- Отдельный документ только с `TRANSPORT` на `e2`: после `DropForeignTransport` список ВСД пуст.

У каждого ВСД `RawXml` с `vetDType` и `certifiedConsignment/consignee/enterprise/guid`: без них правило вернёт false, и тест позеленеет вхолостую.

`DropForeignTransport` возвращает true, только если кто-то снят. Статус документа пересчитывается через уже существующий `Drop`.

- [x] **Step 2: Прогнать**

Expected: FAIL, метода нет.

- [x] **Step 3: Разбор rawXml в домене**

Рядом с `ReferencedIncomingDocument`: разобрать `rawXml`, взять `vetDocument`, прочитать `vetDType` и guid прямого `enterprise` внутри `consignee`. Исключение разбора XML — false. Метод документа:

```csharp
public bool DropForeignTransport()
{
    var foreign = Vsds
        .Where(vsd => VetisIncomingShipment.IsForeignTransport(vsd.RawXml, EnterpriseGuid))
        .Select(vsd => vsd.Uuid)
        .ToList();
    return Drop(foreign);
}
```

- [x] **Step 4: Тест страницы**

`MemoryVetisIncoming.Delete` удаляет id из `Store`. Документ `a` со смешанными ВСД из шага 1 и документ `b` только с чужим TRANSPORT.

```csharp
var step = (await sweep.SweepBatchAsync("", CancellationToken.None)).Value;
```

Ожидание: `a` сохранён без чужого uuid, `b` удалён из `Store`, `step.Finished`. `Find` вызывается на обе строки: сверка `sortAt` чужой XML не видит.

- [x] **Step 5: Сервис**

`SweepBatchAsync` читает `HeadsAfter(afterId, 200)`. На каждую голову `Find`. `Failure` — наружу, курсор воркера не двигается. `null` — пропуск. `DropForeignTransport() == false` — пропуск. Пустой `Vsds` — `Delete`, иначе `Save`. `Finished` — страница короче 200. `AfterId` — id последней головы. За страницу считается снятых ВСД и удалённых документов, `ILogger<VetisIncomingTransportSweep>` пишет их одним `LogInformation` вместе с `AfterId`; сбой `Save`/`Delete` возвращается наружу до записи в журнал, чтобы не объявлять снятым несохранённое.

`Delete` репозитория — нынешний приватный метод сделать публичной реализацией интерфейса. Второй try вокруг страницы не добавлять: исключение CouchDB уже ловится в `Find` / `Save` / `Delete`.

- [x] **Step 6: Воркер**

Поля `_transportAfter` и `_transportDone`. Прямо перед условием загрузки, тем же `delay = RetryDelay`:

```csharp
if (!_transportDone)
{
    var budget = Stopwatch.StartNew();
    while (!_transportDone && budget.Elapsed < ScanBudget)
    {
        var swept = await scope.ServiceProvider
            .GetRequiredService<IVetisIncomingTransportSweep>()
            .SweepBatchAsync(_transportAfter, stoppingToken);
        if (swept.IsFailure)
            throw new InvalidOperationException(swept.Error);

        _transportAfter = swept.Value.AfterId;
        _transportDone = swept.Value.Finished;
    }
}

if (_sortDone && _transportDone)
{
    if (settings.Enable)
        await scope.ServiceProvider.GetRequiredService<IVetisIncomingLoader>().LoadAsync(stoppingToken);

    // Только после успешной загрузки: сбой оставляет короткий повтор, а не интервал журнала.
    delay = settings.IncomingInterval();
}
```

Пока хотя бы один проход не закончен, `LoadAsync` не вызывается. Иначе загрузка дописывает ВСД между `Find` и `Save` снятия, и сохранение снятия записывает старый список поверх — а курсор загрузки к этому моменту уже сдвинут, и строка сама не вернётся. Бюджет `ScanBudget` на круг остаётся: одна страница в минуту держала бы журнал без обновления часами. Снятие работает и при выключенной загрузке. Пауза при незавершённых проходах — `RetryDelay`.

- [x] **Step 7: Прогнать тесты снятия и домена**

Expected: PASS.

---

## Покрытие замечаний

| Замечание | Задача |
| --- | --- |
| 1. Снимок документа затирает загрузку | 3 |
| 2. Два гашения и один прогресс | 4 |
| 3. Подстрока «не погашен» и чужой факт | 5 |
| 4. Пустой факт становится полной приёмкой | 1, 2 |
| 5. Не та ТТН | 6 |
| 6. Все TRANSPORT в журнале, включая уже лежащие | 7, 12 |
| 7. Сбой подсчёта как ноль | 8 |
| 8. Старый sortAt | 10 |
| 9. Esc, чужой отчёт и чужая стадия | 11 |
| 10. deliveryDate без пояса | 6 |
| 11. Подсчёт через skip | 9 |
| 12. Запятая в тексте акта | 1 |
| 13. Кнопка без оформленных ВСД | 2 |
| 14. Ошибка HTTP не обновляет журнал | 11 |

## Проверка целиком

```powershell
dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --nologo
dotnet test src/tests/Application.Tests/Application.Tests.csproj --nologo
dotnet test src/tests/CouchDb.Tests/CouchDb.Tests.csproj --nologo
node --test src/tests/WebApp.Tests/vetisFormat.test.mjs src/tests/WebApp.Tests/vetisFilterBar.test.mjs
```

Ожидание: все четыре команды завершаются без падений.

## Ход выполнения

План закрыт. Domain.Tests — 164, Application.Tests — 240, CouchDb.Tests — 19, node-тесты WebApp — 24; `dotnet build FmuApiCentral.sln` — 0 ошибок. Коммиты не делались.

Отклонения от TDD-порядка:

- Task 10, шаг 4: тест страницы написан после появления `IVetisIncomingSortAligner` и `VetisIncomingSortAligner`, поэтому у него не было красного прогона. Красным был доменный тест `AlignSortAt` (шаги 1–3).
- Task 1, шаг 2: `Пропущенный_факт_считается_полным_объёмом` был зелёным до правки — как и записано в ожидании шага.
- Остальные шаги шли тест-первыми, ожидаемые красные прогоны совпали.

Особенности окружения: сборка и тесты шли с `--artifacts-path` во временной папке — Windows не даёт писать в `src/**` из-под песочницы, а тестовый хост в песочнице не стартует (запрет на дескриптор родительского процесса). Код и тесты от этого не менялись.
