# Документы и остатки ГИС МТ в CouchDB Central — план

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GisMt пишет УПД и марки остатка в CouchDB Central; Central получает те же сущности и репозитории для будущего view.

**Architecture:** Копия модели fmu-api (`GisMtDocumentEntity`, `GisMtMarkEntity`, Save/Exists/SaveRange). Имена баз Central. GisMt подключается по `databaseConnection` из пакета `PUT /api/exchange-state`. Фоновые воркеры читают настройки пакета. Меню «Действия» без смены URL.

**Tech Stack:** .NET 8 (Central), .NET 10 (GisMt), CouchDB.NET 3.6.1, `UniversalDocument<T>`, CSharpFunctionalExtensions.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-08-31-gismt-couchdb-marks-design.md`
- Базы: `fmu-api-central-gismt-documents`, `fmu-api-central-gismt-marks` (не `fmu-api-gis-mt-*`)
- Сущности и методы репозиториев — как в `D:\Csharp\FMU-API\src\Core\FmuApiDomain\GisMt\` и репозиториях fmu-api
- Комментарии к методам на русском
- Try/catch только на верхней границе операции/воркера
- Коммиты git только если пользователь явно попросил
- В solution нет тестовых проектов: проверка — `dotnet build` (0 ошибок)
- View WebApp и API списка в Central не делать
- Callback с телами марок не использовать

## File structure

**Central**

- `src/Core/Domain/Entitys/GisMt/GisMtDocumentEntity.cs` — сущность УПД
- `src/Core/Domain/Entitys/GisMt/GisMtMarkEntity.cs` — сущность марки
- `src/Core/Domain/Entitys/GisMt/GisMtMarkSearchResult.cs` — результат Search
- `src/Core/Domain/Entitys/GisMt/GisMtMarkMapper.cs` — FromCisInfo (для единообразия с fmu-api; Central этим этапом не вызывает ЧЗ)
- `src/Core/Domain/Entitys/GisMt/Interfaces/IGisMtDocumentRepository.cs`
- `src/Core/Domain/Entitys/GisMt/Interfaces/IGisMtMarkRepository.cs`
- `src/Infrastructure/CouchDb/DatabaseScheme/DatabaseNames.cs` — две константы в `All()`
- `src/Infrastructure/CouchDb/DatabaseScheme/DatabaseIndexes.cs` — mango-индексы fmu-api
- `src/Infrastructure/CouchDb/Context.cs` — две коллекции
- `src/Infrastructure/CouchDb/Repositories/GisMtDocumentRepository.cs`
- `src/Infrastructure/CouchDb/Repositories/GisMtMarkRepository.cs`
- `src/Infrastructure/CouchDb/DatabaseRegistrationExtensions.cs` — DI
- `src/Infrastructure/CouchDb/Services/CouchDbHealthService.cs` — GetInfo для новых баз

**Family GisMt** (`D:\Csharp\FMU-API Family\GisMt`)

- Domain: те же entity/interfaces/mapper (+ `IHaveStringId`)
- `src/Infrastructure/CouchDb/` — тонкий проект: DTO UniversalDocument, имена баз, индексы, gateway, два репозитория
- Application: `GisMtCisInfoSaver`, правки Documents/Stock
- `src/View/Api/Workers/GisMtDocumentsSyncWorker.cs`, `GisMtStockLoadWorker.cs`
- `Program.cs`, `ExchangeController.cs`, `Api.csproj`

---

### Task 1: Central Domain — сущности и интерфейсы

**Files:**
- Create: `src/Core/Domain/Entitys/GisMt/GisMtDocumentEntity.cs`
- Create: `src/Core/Domain/Entitys/GisMt/GisMtMarkEntity.cs`
- Create: `src/Core/Domain/Entitys/GisMt/GisMtMarkSearchResult.cs`
- Create: `src/Core/Domain/Entitys/GisMt/GisMtMarkMapper.cs`
- Create: `src/Core/Domain/Entitys/GisMt/Interfaces/IGisMtDocumentRepository.cs`
- Create: `src/Core/Domain/Entitys/GisMt/Interfaces/IGisMtMarkRepository.cs`

**Interfaces:**
- Consumes: `Domain.Entitys.Interfaces.IHaveStringId`
- Produces: `GisMtDocumentEntity`, `GisMtMarkEntity`, `IGisMtDocumentRepository`, `IGisMtMarkRepository`, `GisMtMarkMapper.FromCisInfo`

- [ ] **Step 1: Сущности**

`GisMtDocumentEntity` — поля как `D:\Csharp\FMU-API\src\Core\FmuApiDomain\GisMt\Entities\GisMtDocumentEntity.cs`: `Id`, `Number`, `DocDate`, `Type`, `Status`, `SenderInn`, `ReceiverInn`, `ProductGroup`, `OrganisationInn`, `MarksCount`, `LoadedAt`. Namespace: `Domain.Entitys.GisMt`. Реализует `IHaveStringId`.

`GisMtMarkEntity` — поля как `D:\Csharp\FMU-API\src\Core\FmuApiDomain\GisMt\Entities\GisMtMarkEntity.cs`: плюс `IsTracking`, `IsExpired => ExpireDate.HasValue && ExpireDate.Value < DateTime.UtcNow`. Namespace: `Domain.Entitys.GisMt`.

`GisMtMarkSearchResult`: `Marks`, `Count`, `CurrentPage`, `PageSize`, `TotalPages`, `SearchTerm`.

- [ ] **Step 2: Интерфейсы**

```csharp
public interface IGisMtDocumentRepository
{
    Task<GisMtDocumentEntity?> Get(string id);
    Task<bool> Exists(string id);
    Task<bool> Save(GisMtDocumentEntity entity);
}

public interface IGisMtMarkRepository
{
    Task<GisMtMarkEntity?> Get(string id);
    Task<bool> Save(GisMtMarkEntity entity);
    Task<bool> SaveRange(IEnumerable<GisMtMarkEntity> entities);
    Task<Result<GisMtMarkEntity>> ChangeState(string sGtin, bool sold);
    Task<List<GisMtMarkEntity>> GetExpiredForCleanup(DateTime olderThanUtc, int limit);
    Task<bool> Delete(string id);
    Task<Result<GisMtMarkSearchResult>> Search(string searchTerm, int page, int pageSize, string? productGroup = null);
}
```

- [ ] **Step 3: Mapper**

Скопировать `FromCisInfo` / `IsSold` / `ParseExpireDate` из `D:\Csharp\FMU-API\src\Core\FmuApiDomain\GisMt\GisMtMarkMapper.cs`, возвращать `GisMtMarkEntity` (`IsTracking = false`). Тип `CisInfoData` в Central может отсутствовать — тогда mapper в Central не копировать (Central ЧЗ не вызывает). **Если нет `CisInfoData` в Central Domain — файл `GisMtMarkMapper.cs` в Central не создавать.** Mapper обязателен в GisMt (Task 3).

- [ ] **Step 4: Сборка Domain**

Run: `dotnet build "D:\Csharp\FMU-API-Central\src\Core\Domain\Domain.csproj"`  
Expected: 0 ошибок.

---

### Task 2: Central CouchDB — базы, Context, репозитории

**Files:**
- Modify: `src/Infrastructure/CouchDb/DatabaseScheme/DatabaseNames.cs`
- Modify: `src/Infrastructure/CouchDb/DatabaseScheme/DatabaseIndexes.cs`
- Modify: `src/Infrastructure/CouchDb/Context.cs`
- Modify: `src/Infrastructure/CouchDb/DatabaseRegistrationExtensions.cs`
- Modify: `src/Infrastructure/CouchDb/Services/CouchDbHealthService.cs`
- Create: `src/Infrastructure/CouchDb/Repositories/GisMtDocumentRepository.cs`
- Create: `src/Infrastructure/CouchDb/Repositories/GisMtMarkRepository.cs`

**Interfaces:**
- Consumes: entity/interfaces из Task 1, `BaseCouchDbRepository<T>`, `CreateAsync` / `UpdateAsync` / `CreateBulkAsync` / `GetByIdAsync` / `DeleteAsync`
- Produces: рабочие репозитории на базах `fmu-api-central-gismt-documents` и `fmu-api-central-gismt-marks`

- [ ] **Step 1: Имена баз**

В `DatabaseNames`:

```csharp
public const string GisMtDocuments = "fmu-api-central-gismt-documents";
public const string GisMtMarks = "fmu-api-central-gismt-marks";
```

Добавить оба в массив `All()`.

- [ ] **Step 2: Индексы**

В `DatabaseIndexSchema` добавить ключи `DatabaseNames.GisMtDocuments` и `DatabaseNames.GisMtMarks` с тем же набором имён/полей, что в `D:\Csharp\FMU-API\src\Infrastructure\CouchDb\DatabaseScheme\DatabaseIndexes.cs` методы `GisMtDocumentsDbIndexes` и `GisMtMarksDbIndexes`.

- [ ] **Step 3: Context**

Свойства:

```csharp
public required CouchDatabase<UniversalDocument<GisMtDocumentEntity>> GisMtDocuments { get; set; }
public required CouchDatabase<UniversalDocument<GisMtMarkEntity>> GisMtMarks { get; set; }
```

В `OnDatabaseCreating`:

```csharp
databaseBuilder.Document<UniversalDocument<GisMtDocumentEntity>>().ToDatabase(DatabaseNames.GisMtDocuments);
databaseBuilder.Document<UniversalDocument<GisMtMarkEntity>>().ToDatabase(DatabaseNames.GisMtMarks);
```

- [ ] **Step 4: GisMtDocumentRepository**

Наследование `BaseCouchDbRepository<GisMtDocumentEntity>`. Конструктор: `services.GetRequiredService<Context>().GisMtDocuments`.

Логика как `D:\Csharp\FMU-API\src\Infrastructure\CouchDb\Repositories\GisMtDocumentRepository.cs`: `Get` → `GetByIdAsync`; `Exists` → сущность с непустым Id; `Save` — пустой Id → `entity.Number`; нет записи → `CreateAsync`, есть → `UpdateAsync`. Если `!_appState.DbState()` — Get/Exists false/null, Save false.

- [ ] **Step 5: GisMtMarkRepository**

Конструктор: `Context.GisMtMarks`.

- `Get` / `Save` / `SaveRange` / `ChangeState` / `Delete` — как fmu-api `GisMtMarkRepository.cs` (Id = SGtin или Cis; SaveRange → `CreateBulkAsync`).
- `GetExpiredForCleanup` и `Search` — та же mango-логика, что в fmu-api. Запросы через `_database.QueryAsync` (CouchDB.Driver 3.6). Если метода нет — `POST _find` через `_database.NewRequest()`. Селекторы без изменения смысла полей `data.infoLoadedAt`, `data.sold`, `data.sGtin`, `data.cis`, `data.productGroup`.
- При `!_appState.DbState()` — как «БД недоступна» в fmu-api (`null` / `false` / `Result.Failure`).

- [ ] **Step 6: DI и health**

В `AddCouchDb`:

```csharp
services.AddScoped<IGisMtDocumentRepository, GisMtDocumentRepository>();
services.AddScoped<IGisMtMarkRepository, GisMtMarkRepository>();
```

В `CouchDbHealthService.IsDatabaseAccessible` добавить ветки `DatabaseNames.GisMtDocuments` → `_dbContext.GisMtDocuments.GetInfoAsync()` и `GisMtMarks` аналогично.

- [ ] **Step 7: Сборка CouchDb + WebApi**

Run: `dotnet build "D:\Csharp\FMU-API-Central\src\Presentation\WebApi\WebApi.csproj"`  
Если DLL занята процессом — `-o` во временную папку.  
Expected: 0 ошибок.

---

### Task 3: GisMt Domain — те же сущности

**Files:**
- Create: `D:\Csharp\FMU-API Family\GisMt\src\Core\Domain\Entitys\Interfaces\IHaveStringId.cs` (как Central)
- Create: `...\Domain\GisMt\Entities\GisMtDocumentEntity.cs`
- Create: `...\Domain\GisMt\Entities\GisMtMarkEntity.cs`
- Create: `...\Domain\GisMt\Models\GisMtMarkSearchResult.cs`
- Create: `...\Domain\GisMt\Interfaces\IGisMtDocumentRepository.cs`
- Create: `...\Domain\GisMt\Interfaces\IGisMtMarkRepository.cs`
- Create: `...\Domain\GisMt\Interfaces\IGisMtCisInfoSaver.cs`
- Create: `...\Domain\GisMt\Interfaces\IGisMtCouchDbGateway.cs`
- Modify: `...\Domain\GisMt\GisMtMarkMapper.cs` — добавить `FromCisInfoEntity` (или второй `FromCisInfo` → `GisMtMarkEntity`)
- Modify: `...\Domain\GisMt\Interfaces\IGisMtDocumentsSyncService.cs` — `SyncAll`
- Modify: `...\Domain\GisMt\Interfaces\IGisMtStockLoadService.cs` — `LoadAll`

**Interfaces:**
- Consumes: `CisInfoData` уже в GisMt Domain
- Produces: те же типы, что Task 1, плюс gateway/saver и SyncAll/LoadAll

- [ ] **Step 1: IHaveStringId и entity**

Те же поля, namespace `Domain.GisMt.Entities`.

- [ ] **Step 2: Репозитории и saver**

Интерфейсы репозиториев — те же сигнатуры, что в Task 1.

```csharp
public interface IGisMtCisInfoSaver
{
    Task<Result<int>> SaveBatches(
        string organisationInn,
        string token,
        string productGroup,
        IReadOnlyList<string> cises,
        string sourceDocumentId,
        CancellationToken cancellationToken = default);
}

public interface IGisMtCouchDbGateway
{
    bool IsReady { get; }
    int BulkBatchSize { get; }
    int BulkParallelTasks { get; }
    Task Apply(Domain.Configuration.Options.DatabaseConnection connection, CancellationToken cancellationToken);
    ICouchDatabase<UniversalDocument<GisMtMarkEntity>>? Marks { get; }
    ICouchDatabase<UniversalDocument<GisMtDocumentEntity>>? Documents { get; }
}
```

`ICouchDatabase` / `UniversalDocument` живут в Infrastructure: gateway-интерфейс в Domain держать **без** CouchDB.Driver. В Domain только:

```csharp
public interface IGisMtCouchDbGateway
{
    bool IsReady { get; }
    Task Apply(DatabaseConnection connection, CancellationToken cancellationToken);
}
```

Репозитории получают коллекции через конструктор Context/gateway в Infrastructure.

- [ ] **Step 3: SyncAll / LoadAll**

```csharp
// IGisMtDocumentsSyncService
Task<Result> Sync(GisMtOperationJob job, CancellationToken cancellationToken = default);
Task<Result> SyncAll(CancellationToken cancellationToken = default);

// IGisMtStockLoadService
Task<Result> Load(GisMtOperationJob job, CancellationToken cancellationToken = default);
Task<Result> LoadAll(CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Mapper entity**

Добавить `FromCisInfoEntity(...)` с телом fmu-api `FromCisInfo` (поле `IsTracking = false`). Старый `FromCisInfo` → DTO можно оставить.

- [ ] **Step 5: Сборка Domain GisMt**

Run: `dotnet build "D:\Csharp\FMU-API Family\GisMt\src\Core\Domain\Domain.csproj"`  
Expected: 0 ошибок.

---

### Task 4: GisMt Infrastructure CouchDb

**Files:**
- Create: `D:\Csharp\FMU-API Family\GisMt\src\Infrastructure\CouchDb\CouchDb.csproj` (net10.0, CouchDB.NET 3.6.1 + DI, ProjectReference Domain + Shared)
- Create: DTO `UniversalDocument<T>` — копия Central
- Create: `DatabaseNames` с двумя именами spec
- Create: `DatabaseIndexes` — как Task 2
- Create: `GisMtCouchDbContext` с двумя `ICouchDatabase<UniversalDocument<...>>` (как fmu-api `CouchDbContext`, не `CouchContext` startup, чтобы менять endpoint после PUT)
- Create: `GisMtCouchDbGateway` — singleton, пересоздание `CouchClient` при смене fingerprint `enable|netAddress|userName|password`
- Create: тонкий `BaseGisMtCouchDbRepository<T>`: GetById/Create/Update/Delete/CreateBulk из Central `BaseCouchDbRepository`, bulk size из `packet.DatabaseConnection` через gateway
- Create: `GisMtDocumentRepository`, `GisMtMarkRepository` — логика Task 2 / fmu-api, готовность: `gateway.IsReady`
- Create: `CouchDbRegistrationExtension.AddGisMtCouchDb()` — AutoRegister сборки, scoped репозитории, singleton gateway
- Modify: `Api.csproj` — ProjectReference на CouchDb
- Modify: `Program.cs` — `AddGisMtCouchDb()`

**Interfaces:**
- Consumes: Domain entity/interfaces Task 3
- Produces: `IGisMtDocumentRepository`, `IGisMtMarkRepository`, `IGisMtCouchDbGateway.Apply`

- [ ] **Step 1: Проект и имена баз**

Константы строго `fmu-api-central-gismt-documents` и `fmu-api-central-gismt-marks`.

- [ ] **Step 2: Gateway.Apply**

Алгоритм:

1. Если `!connection.Enable` или пустой `NetAddress` — освободить клиент, `IsReady = false`, return.
2. Если fingerprint совпал и клиент жив — return.
3. Создать `CouchClient` на `NetAddress` + basic auth.
4. PUT `/{db}` для обеих баз (404 → создать), как `CouchDbStatusService.EnsureDatabasesExists`.
5. POST `/{db}/_index` по `DatabaseIndexes`.
6. Собрать `GisMtCouchDbContext` через `client.GetDatabase<UniversalDocument<T>>(name)`.
7. `IsReady = true`.

Вызов из `ExchangeController.PutExchangeState` после `state.ApplyPacket`: `await gateway.Apply(packet.DatabaseConnection, cancellationToken)`. Контроллер сделать `async Task<IActionResult>`.

- [ ] **Step 3: Репозитории**

Те же методы, что Task 2. `CreateBulkAsync` берёт `BulkBatchSize` / `BulkParallelTasks` из текущего пакета (`exchangeState.Packet()?.DatabaseConnection`, запасные 1000/4).

- [ ] **Step 4: Сборка Api**

Run: `dotnet build "D:\Csharp\FMU-API Family\GisMt\src\View\Api\Api.csproj"`  
Expected: 0 ошибок (или `-o` если Api.exe держит DLL).

---

### Task 5: GisMt — запись вместо callback марок

**Files:**
- Create: `D:\Csharp\FMU-API Family\GisMt\src\Core\Application\GisMt\GisMtCisInfoSaver.cs`
- Modify: `GisMtDocumentsSyncService.cs`
- Modify: `GisMtStockLoadService.cs`
- Modify: `Application.csproj` — reference CouchDb **не** нужен, если репозитории в Domain interfaces (Application уже ссылается на Domain)

**Interfaces:**
- Consumes: `IGisMtCisInfoSaver`, `IGisMtDocumentRepository`, `IGisMtMarkRepository`, `IGisMtCouchDbGateway`, `IGisMtCentralExchangeState`
- Produces: УПД и марки в CouchDB; `SyncAll` / `LoadAll`

- [ ] **Step 1: GisMtCisInfoSaver**

Как `D:\Csharp\FMU-API\src\Core\FmuApiApplication\GisMt\GisMtCisInfoSaver.cs`, но `organisationInn: string` вместо `PrintGroupData`. `FromCisInfoEntity`, `SaveRange`. `BatchSize = 1000`. Если `!gateway.IsReady` — `Result.Failure("CouchDB выключена")`. AutoRegister Scoped.

- [ ] **Step 2: Документы**

В `ProcessDocument` (сейчас callback с DTO):

1. Если `await documentRepository.Exists(item.Number)` — `return Result.Success(0)` (пропуск, как fmu-api).
2. `SaveBatches(..., sourceDocumentId: item.Number)`.
3. `documentRepository.Save` с `Id = item.Number`, `LoadedAt = DateTime.UtcNow`, `MarksCount` из saver.
4. Callback `GisMtCallbackFactory.Document` **не вызывать**.
5. После цикла групп по job — cleanup: `GetExpiredForCleanup(UtcNow.AddDays(-max(markRetentionDays, 365)))`, удалить с тем же `OrganisationInn`, у которых `Sold || IsExpired` (как fmu-api `CleanupMarks`). `markRetentionDays` из `exchangeState.Packet()?.Settings`.

`SendFinal` callback оставить: пустой URL = no-op.

Если `!gateway.IsReady` в начале `Sync` — `RecordStatus(inn, 0, "CouchDB выключена")` и `Result.Failure`.

- [ ] **Step 3: Остатки**

Заменить callback пачек марок на `SaveBatches(..., "stock")`. Документ УПД не создавать. `SendFinal` оставить.

- [ ] **Step 4: SyncAll / LoadAll**

`SyncAll`: для каждого `LiveTokens()` собрать `GisMtOperationJob` Kind=Documents, `ProductGroups = []`, `DateTo = DateTime.Now`, `DateFrom = DateTo.Date.AddDays(1 - max(DocumentsSyncDays, 1))`, `CallbackUrl = ""`, вызвать `Sync(job)`. Ошибку одной организации логировать, остальных продолжать. Итог Success, если не было общего сбоя gateway.

`LoadAll`: то же для Kind=Stock без дат.

- [ ] **Step 5: Сборка Application + Api**

Run: `dotnet build "D:\Csharp\FMU-API Family\GisMt\src\View\Api\Api.csproj"`  
Expected: 0 ошибок.

---

### Task 6: Воркеры расписания GisMt

**Files:**
- Create: `D:\Csharp\FMU-API Family\GisMt\src\View\Api\Workers\GisMtDocumentsSyncWorker.cs`
- Create: `D:\Csharp\FMU-API Family\GisMt\src\View\Api\Workers\GisMtStockLoadWorker.cs`
- Modify: `D:\Csharp\FMU-API Family\GisMt\src\View\Api\Program.cs`

**Interfaces:**
- Consumes: `IGisMtDocumentsSyncService.SyncAll`, `IGisMtStockLoadService.LoadAll`, `IGisMtCentralExchangeState`
- Produces: фоновая загрузка по пакету

- [ ] **Step 1: Documents worker**

Как `GisMtStatusProbeWorker`: пока `!ExchangeEnabled` — sleep 5 с. Иначе `SyncAll`, затем ждать `PollIntervalMinutes()` секунд по секунде (если &lt; 1 минуты в пакете — 10 минут, уже в `PollIntervalMinutes()`). Один try/catch на цикл.

- [ ] **Step 2: Stock worker**

Как `D:\Csharp\FMU-API\src\Infrastructure\TrueApiIntegration\Workers\GisMtStockLoadWorker.cs`: каждую минуту; `stockLoadEnabled` и `StockLoadTime` из `Packet()?.Settings`; `ShouldRunToday`; `LoadAll`. Если пакета нет — не запускать.

- [ ] **Step 3: Program.cs**

```csharp
builder.Services.AddGisMtCouchDb();
builder.Services.AddHostedService<GisMtDocumentsSyncWorker>();
builder.Services.AddHostedService<GisMtStockLoadWorker>();
```

- [ ] **Step 4: Сборка**

Run: `dotnet build "D:\Csharp\FMU-API Family\GisMt\src\View\Api\Api.csproj"`  
Run: `dotnet build "D:\Csharp\FMU-API-Central\src\Presentation\WebApi\WebApi.csproj"`  
Expected: 0 ошибок в обоих.

---

## Spec coverage

| Spec | Task |
|---|---|
| Имена баз central-gismt-documents/marks | 2, 4 |
| Entity как fmu-api | 1, 3 |
| Репозитории Get/Exists/Save/SaveRange/Search/cleanup | 2, 4 |
| Индексы mango | 2, 4 |
| GisMt пишет из пакета, не appsettings | 4 |
| enable=false → не писать, статус 0 | 5 |
| Смена connection → новый клиент | 4 |
| Exists(number) skip | 5 |
| sourceDocumentId номер / stock | 5 |
| markRetentionDays cleanup | 5 |
| Расписание документов и остатков | 6 |
| Меню Central без смены POST | — не трогать |
| View не делать | — |
| GET /api/status без изменений контракта | — |
| Один try на границе | 5, 6 |

## Type consistency

- `IGisMtDocumentRepository` / `IGisMtMarkRepository` совпадают в Central и GisMt.
- `SyncAll` / `LoadAll` только в GisMt.
- `IGisMtCisInfoSaver.SaveBatches(string organisationInn, ...)`.
- `IGisMtCouchDbGateway.Apply(DatabaseConnection, CancellationToken)`.
