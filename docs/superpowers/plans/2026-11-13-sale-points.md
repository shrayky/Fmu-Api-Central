# Торговые точки организации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Справочник торговых точек живёт в карточке организации, а входящие документы Меркурия и ЭТрН получают `salePointUid` — подобранный по адресу при загрузке или выбранный вручную в карточке документа. Журналы показывают точку и отбирают по ней.

**Architecture:** Отдельной коллекции, репозитория и кэша у справочника нет: `OrganizationEntity.SalePoints` — второй вложенный список рядом с `EtrnConnections`. Сопоставление адреса — чистая функция Domain, её вызывают обе загрузки. На документе хранится только uid; имя и «закрыта» подставляет чтение из карточки организации, как `enterpriseName` у площадки Ветис. Отбор идёт отдельным семейством индексов, где организация всегда первая.

**Tech Stack:** .NET 10, xUnit, CouchDB Mango, Webix, node:test.

## Global Constraints

- Справочник — в документе организации (`OrganizationEntity.SalePoints`). Новых CouchDB-баз, репозиториев и кэшей не появляется.
- На входящем документе Меркурия и ЭТрН только `salePointUid`. Имя и признак «закрыта» не копируются: их подставляет `Fill` при выдаче, как `enterpriseName`.
- Сравнение адресов: `Trim()` по краям и `OrdinalIgnoreCase`. Внутренние пробелы и знаки препинания не нормализуются — расхождения закрывает синоним.
- Подбор: только открытые точки; сначала адреса, затем синонимы; ровно одно совпадение; иначе пустая строка.
- При загрузке uid пишется только в пустое поле. Уже заполненный повторный опрос не меняет.
- «Изменить» и «Очистить» пишут `salePointUid` на документ сразу. Пустое поле открывает тот же список кнопкой-значком, без надписи. После «Очистить» следующий опрос снова подбирает по адресу.
- У ЭТрН в статусе «Ждёт приёмки» uid копируется со старого документа там же, где копируется `Operator`.
- Отбор точки — только вместе с организацией. Поля индекса: организация, точка, площадка или подключение, статус, `sortAt`; организация всегда первая.
- `salePointUid` не входит в общий перебор `FilterIndexes()`: иначе появятся индексы без организации.
- «Без точки» — равенство `salePointUid` пустой строке. На клиенте это отдельное значение `none`, потому что `_get` в `etrnService`/`vetisService` выбрасывает пустые параметры, а «фильтр не задан» — это отсутствие параметра.
- Документы, сохранённые до фичи, поля не имеют и в отбор «Без точки» не попадают, пока их не перезапишет следующий опрос. Отдельного прохода по старым документам нет.
- Удаление точки проверяется на кнопке и повторно при сохранении карточки. Uid на документах при этом не очищается.
- Адрес и синоним двух открытых точек не совпадают (то же сравнение). Закрытые точки в проверке не участвуют.
- Живые Ветис, СБИС, Платформа ОФД и CouchDB в тестах не вызываются: репозитории подменяются подделками, запросы проверяются как JSON.
- Любое изменение поведения — сначала падающий тест, потом код (`.cursor/rules/tdd.mdc`). JavaScript проверяется `node --test`.

## Вне плана

- Права пользователя на торговые точки (одну или несколько).
- Подбор ФИАС и адреса из внешнего сервиса: ФИАС вводится вручную.
- Копирование имени точки и признака «закрыта» в документ.
- Точки для исходящих документов, заявок Ветис и запросов ЭТрН.
- Пересборка и миграция уже сохранённых документов.
- Изменения протоколов Saby и Платформы ОФД.

---

### Task 1: Справочник в модели, сохранение и проверки

**Files:**
- Create: `src/Core/Domain/Entitys/Organization/SalePoint.cs` — `SalePoint`, `SalePointAlias`, `SalePointOption`
- Create: `src/Core/Domain/Entitys/Organization/SalePointDirectory.cs`
- Modify: `src/Core/Domain/Entitys/Organization/OrganizationEntity.cs`
- Modify: `src/Core/Domain/Entitys/Organization/Dto/OrganizationView.cs`
- Modify: `src/Core/Application/Organization/Services/OrganizationManagerService.cs`
- Test: `src/tests/Domain.Tests/SalePointTests.cs`
- Test: `src/tests/Application.Tests/OrganizationManagerServiceTests.cs`

**Interfaces:**
- Produces:
  - `SalePoint { Uid, Name, Address, FiasId, Closed, Aliases }`, `SalePointAlias { Uid, Address }`, `SalePointOption { Uid, Name, Closed }` — JSON-имена `uid`, `name`, `address`, `fiasId`, `closed`, `aliases` (у синонима — `uid`, `address`)
  - `OrganizationEntity.SalePoints`, `OrganizationView.SalePoints` (`List<SalePoint>?`, `null` — «поле не прислали»)
  - `SalePointDirectory.Collision(IEnumerable<SalePoint>)` → `(SalePoint Point, string Address, bool Alias)?`

- [x] **Step 1: Падающий тест проверки справочника**

`SalePointDirectory.Collision` не пропускает две открытые точки с одинаковым адресом или синонимом. Сравнение — `Trim` + без учёта регистра. Закрытая точка в проверке не участвует. Одинаковый адрес внутри одной точки ошибкой не считается.

`OrganizationManagerService` на сохранении с пустым `uid` выдаёт его точке и синониму, обрезает поля по краям, выбрасывает синоним с пустым адресом. Сохранение без `salePoints` в JSON не стирает уже записанный справочник. Точка без названия или без адреса не сохраняется: «Укажите название торговой точки», «Укажите адрес торговой точки».

- [x] **Step 2: Прогон**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~SalePointTests -v q`

Expected: FAIL, типов нет.

- [x] **Step 3: Модель и проверка**

`SalePoint` — `sealed class` с `[JsonPropertyName]` и инициализаторами по умолчанию, как `OrganizationEnterprise`. `SalePointDirectory` — статический `Collision` (проверка) плюс экземплярные `Find`, `Name`, `Closed`, `Options(organizationId, openOnly)` для чтения по id организации; экземпляр собирается из `IEnumerable<OrganizationEntity>`, как `VetisDirectory`.

- [x] **Step 4: Сохранение**

`OrganizationEntity.SalePoints ??= []` в `OnDeserialized`. В `Prepare` список нормализуется: `uid` пустой или длиннее 36 — новый `Guid`; `name`/`address`/`fiasId` обрезаются; `aliases` — без пустых адресов, с выданными uid. Там же — длины: название 150, адрес и синоним 250, ФИАС 36, и проверка `Collision` по нормализованному списку с сообщением «Адрес «{address}» уже указан у открытой точки «{name}»» (для синонима — «Синоним «{address}» уже указан у открытой точки «{name}»»). В `Apply` подготовленный список копируется только при `data.SalePoints != null` (как `EtrnConnections`) — иначе сохранённый справочник остаётся. `ToView` отдаёт копию списка, включая закрытые точки.

- [x] **Step 5: Прогон и коммит**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~SalePoint -v q` и `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~OrganizationManagerServiceTests -v q`

Expected: PASS

Commit: `new: справочник торговых точек на организации`

---

### Task 2: Подбор точки по адресу при загрузке

**Files:**
- Create: `src/Core/Domain/Entitys/Organization/SalePointMatch.cs`
- Modify: `src/Core/Domain/Etrn/EtrnDocument.cs` — `EtrnIncomingDocumentEntity.SalePointUid`
- Modify: `src/Core/Domain/Vetis/VetisIncomingDocument.cs` — `VetisIncomingDocumentEntity.SalePointUid`
- Modify: `src/Core/Application/Etrn/EtrnIncomingLoadService.cs`
- Modify: `src/Core/Application/Vetis/VetisIncomingLoadService.cs`
- Test: `src/tests/Domain.Tests/SalePointMatchTests.cs`
- Test: `src/tests/Application.Tests/EtrnIncomingLoadServiceTests.cs`
- Test: `src/tests/Application.Tests/VetisIncomingLoadServiceTests.cs`

**Interfaces:**
- Produces: `SalePointMatch.Find(IEnumerable<SalePoint>? points, string address)` → uid или `""`
- Consumes: `OrganizationEntity.SalePoints`, `OrganizationEnterprise.Address` (адрес площадки Ветис)

- [x] **Step 1: Падающий тест подбора**

Адрес совпал с адресом одной открытой точки — её uid. Совпал только синоним — uid владельца. Совпадение с адресом важнее совпадения с синонимом: если адрес одной точки и синоним другой совпали, выигрывает адрес. Два совпадения на одном шаге — пустая строка. Закрытая точка не участвует. Пустой адрес — пустая строка.

В тестах загрузки Ветис `Organization(...)` заполняет площадке `Address`: подбирать иначе нечего, а адрес документа берётся именно оттуда.

- [x] **Step 2: Прогон**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~SalePointMatchTests -v q`

Expected: FAIL, типа нет.

- [x] **Step 3: Подбор**

Два прохода: адреса точек, затем их синонимы. Каждый проход собирает совпадения и возвращает uid только при ровно одном; на втором проходе синоним принадлежит точке-владельцу. Сравнение — `Trim` + `OrdinalIgnoreCase`.

- [x] **Step 4: Загрузка Меркурия**

`SaveDocumentsAsync` получает организацию и площадку целиком: `SaveDocumentsAsync(organization, enterprise, vsds, saved)` — вызывающий `ReadEnterpriseAsync` их уже держит, а `organizationId`/`enterpriseGuid` берутся из них же. Перед `Save` у документа с пустым `SalePointUid` пишется результат `SalePointMatch.Find(organization.SalePoints, enterprise.Address)`. Пустой адрес площадки оставляет поле пустым. Существующий документ правку сохраняет: `Merge` поле не трогает, перезапись идёт только пустого — поэтому следующий опрос заодно проставляет поле документам, сохранённым до фичи.

- [x] **Step 5: Загрузка ЭТрН**

В `LoadConnectionAsync`, в блоке существующего документа рядом с `entity.Operator = previous.Operator`, копируется `entity.SalePointUid = previous.SalePointUid`. Подбор по `entity.DeliveryAddress` выполняется только если после копирования поле пустое. Повторный опрос «Ждёт приёмки» ручной выбор не сбрасывает.

- [x] **Step 6: Прогон и коммит**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~SalePoint -v q` и `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter "FullyQualifiedName~EtrnIncomingLoadServiceTests|FullyQualifiedName~VetisIncomingLoadServiceTests" -v q`

Expected: PASS

Commit: `new: подбор торговой точки по адресу при загрузке`

---

### Task 3: Индексы точки и проверка использования

**Files:**
- Modify: `src/Infrastructure/CouchDb/Repositories/EtrnIncomingQuery.cs` — `SalePointIndexes()`, `AnyBySalePoint`
- Modify: `src/Infrastructure/CouchDb/Repositories/VetisIncomingQuery.cs` — то же
- Modify: `src/Infrastructure/CouchDb/DatabaseScheme/DatabaseIndexes.cs`
- Modify: `src/Core/Domain/Etrn/EtrnRepositories.cs` — `IEtrnIncomingRepository.AnyBySalePoint`
- Modify: `src/Core/Domain/Vetis/IVetisIncomingRepository.cs` — `AnyBySalePoint`
- Modify: `src/Infrastructure/CouchDb/Repositories/EtrnRepositories.cs`
- Modify: `src/Infrastructure/CouchDb/Repositories/VetisIncomingRepository.cs`
- Test: `src/tests/CouchDb.Tests/EtrnQueriesTests.cs`
- Test: `src/tests/CouchDb.Tests/VetisQueriesTests.cs`

**Interfaces:**
- Produces:
  - `SalePointIndexes()` → 4 пары `(Name, Fields)` на журнал
  - `AnyBySalePoint(organizationId, salePointUid)` → Mango-запрос с `limit: 1`
  - `IEtrnIncomingRepository.AnyBySalePoint`, `IVetisIncomingRepository.AnyBySalePoint` → `Result<bool>`

Имена индексов заданы контрактом:

```
Меркурий, vetis-incoming:
  organizationId-salePointUid-sort-at-idx
  organizationId-salePointUid-enterpriseGuid-sort-at-idx
  organizationId-salePointUid-status-sort-at-idx
  organizationId-salePointUid-enterpriseGuid-status-sort-at-idx
ЭТрН, etrn-incoming:
  organizationId-salePointUid-sort-at-idx
  organizationId-salePointUid-operator-connectionId-sort-at-idx
  organizationId-salePointUid-status-sort-at-idx
  organizationId-salePointUid-operator-connectionId-status-sort-at-idx
```

- [x] **Step 1: Падающие тесты схемы и запроса**

Схема содержит все восемь имён, и в ней нет индекса `salePointUid-…` без организации. `AnyBySalePoint` строит селектор с `data.organizationId`, `data.salePointUid`, якорем `data.sortAt >= 0`, `fields: ["data.id"]`, `limit: 1` и `use_index` базового индекса точки.

- [x] **Step 2: Прогон**

Run: `dotnet test src/tests/CouchDb.Tests/CouchDb.Tests.csproj --filter "FullyQualifiedName~EtrnQueriesTests|FullyQualifiedName~VetisQueriesTests" -v q`

Expected: FAIL, методов и имён нет.

- [x] **Step 3: Запросы**

`SalePointIndexes()` перечисляет четыре комбинации, собирая поля тем же `Field(...)`/`IndexName(...)`, что и `List`, — организация и точка всегда первые, `data.sortAt` последний. `AnyBySalePoint` — отдельный метод, в общий `List` он не встраивается: проверке не нужны ни `skip`, ни проекция строки.

- [x] **Step 4: Схема**

`DatabaseIndexes.EtrnIncomingIndexSchema` и `VetisIncomingIndexSchema` дополняются `..Query.SalePointIndexes().Select(...)`. В `FilterFields` новых полей нет — общий перебор остаётся прежним.

- [x] **Step 5: Репозитории**

`AnyBySalePoint` в обоих репозиториях повторяет форму `List`: проверка `_appState.DbState()`, `ExecuteMangoQueryAsync`, `Value.Count > 0`.

- [x] **Step 6: Прогон и коммит**

Run: `dotnet test src/tests/CouchDb.Tests/CouchDb.Tests.csproj -v q`

Expected: PASS

Commit: `new: индексы и проверка использования торговой точки`

---

### Task 4: Колонка и отбор точки в журналах

**Files:**
- Modify: `src/Core/Domain/Etrn/EtrnRepositories.cs` — `EtrnIncomingFilter.SalePointUid/OnlyWithoutSalePoint`, поля `EtrnIncomingRow`/`EtrnIncomingView`, `EtrnFilterOrganization.SalePoints`
- Modify: `src/Core/Domain/Vetis/IVetisIncomingRepository.cs` — то же в `VetisIncomingFilter`
- Modify: `src/Core/Domain/Vetis/VetisViews.cs` — `SalePointUid`, `SalePointName`, `SalePointClosed`, `SalePoints` в карточке и фильтре
- Modify: `src/Infrastructure/CouchDb/Repositories/EtrnIncomingQuery.cs`, `VetisIncomingQuery.cs` — `ListFields`, ветвь `List` по точке
- Modify: `src/Core/Application/Etrn/EtrnIncomingQueryService.cs`
- Modify: `src/Core/Application/Vetis/VetisIncomingQueryService.cs`
- Modify: `src/Presentation/WebApi/Controllers/EtrnController.cs`, `VetisController.cs`
- Modify: `src/tests/Application.Tests/Fakes/EtrnFakes.cs`, `VetisFakes.cs` — новый отбор в `MemoryEtrnIncoming.List` и `MemoryVetisIncoming.List`
- Test: `src/tests/CouchDb.Tests/EtrnQueriesTests.cs`, `VetisQueriesTests.cs`
- Test: `src/tests/Application.Tests/EtrnIncomingQueryServiceTests.cs`, `VetisQueryServicesTests.cs`

**Interfaces:**
- Produces:
  - `EtrnIncomingFilter { …, SalePointUid, OnlyWithoutSalePoint }`, `VetisIncomingFilter { …, SalePointUid, OnlyWithoutSalePoint }`
  - `EtrnIncomingRow.SalePointUid/SalePointName/SalePointClosed`, то же `VetisIncomingRow`
  - `EtrnIncomingView.SalePoints`, `VetisIncomingView.SalePoints` (`List<SalePointOption>`, только открытые)
  - `EtrnFilterOrganization.SalePoints`, `VetisFilterOrganization.SalePoints`
  - параметр `salePoint` у `GET api/etrn/incoming` и `GET api/vetis/incoming`: пустой — все, `none` — без точки, uid — конкретная

- [x] **Step 1: Падающие тесты запроса**

`List` с `OrganizationId = "o1"`, `SalePointUid = "p1"` и статусом использует индекс `organizationId-salePointUid-status-sort-at-idx`, селектор содержит `data.salePointUid = "p1"`, сортировка — организация, точка, статус, `sortAt`, все `desc`. `OnlyWithoutSalePoint` даёт равенство пустой строке и тот же индекс без статуса. Без точки запрос идёт прежним индексом. Отбор точки без организации — ошибка «Выберите организацию для отбора по торговой точке» — проверяется тестом сервиса, не запроса.

- [x] **Step 2: Прогон**

Run: `dotnet test src/tests/CouchDb.Tests/CouchDb.Tests.csproj --filter "FullyQualifiedName~EtrnQueriesTests|FullyQualifiedName~VetisQueriesTests" -v q`

Expected: FAIL.

- [x] **Step 3: Запросы**

`ListFields` получает `data.salePointUid`. В `List` появляется ветвь: если задан uid или `OnlyWithoutSalePoint`, селектор и сортировка собираются в порядке индекса, `use_index` — `SalePointIndexes`-имя; иначе работает прежний код. Подстрочный поиск номера и поставщика остаётся вне индекса.

- [x] **Step 4: Чтение**

`ListAsync` получает параметр `salePoint` (в конец списка аргументов) и переводит `none` в `OnlyWithoutSalePoint`; при точке без организации возвращает ошибку. `Fill` пишет `SalePointUid` и подставляет имя/«закрыта» из `SalePointDirectory`, собранного по `_organizations.All()`. `GetAsync` дополнительно кладёт в карточку `SalePoints` — открытые точки организации документа.

- [x] **Step 5: API**

Контроллеры передают `salePoint` из query string. Ошибка отбора без организации уходит 400, как остальные ошибки списка.

- [x] **Step 6: Подделки**

`MemoryEtrnIncoming.List` и `MemoryVetisIncoming.List` получают тот же отбор: пустой `SalePointUid` — без условия, заданный uid — равенство, `OnlyWithoutSalePoint` — только пустой uid. Иначе тесты сервисов чтения с подделкой репозитория не увидят отбор.

- [x] **Step 7: Прогон и коммит**

Run: `dotnet test src/tests/CouchDb.Tests/CouchDb.Tests.csproj -v q` и `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter "FullyQualifiedName~EtrnIncomingQueryServiceTests|FullyQualifiedName~VetisQueryServicesTests" -v q`

Expected: PASS. Старые вызовы `ListAsync` в тестах обновляются явным `salePoint: ""`.

Commit: `new: колонка и отбор торговой точки в журналах`

---

### Task 5: Ручное сопоставление и запрет удаления используемой точки

**Files:**
- Create: `src/Core/Application/SalePoints/SalePointService.cs`
- Create: `src/Core/Domain/Entitys/Organization/Dto/SalePointUsageView.cs`
- Create: `src/Core/Domain/Entitys/Organization/Dto/SalePointCommand.cs`
- Modify: `src/Core/Application/Organization/Services/OrganizationManagerService.cs`
- Modify: `src/Presentation/WebApi/Controllers/OrganizationController.cs`, `EtrnController.cs`, `VetisController.cs`
- Test: `src/tests/Application.Tests/SalePointServiceTests.cs`
- Test: `src/tests/Application.Tests/OrganizationManagerServiceTests.cs`

**Interfaces:**
- Produces:
  - `SalePointService.UsedAsync(organizationId, salePointUid)` → `Result<bool>`
  - `SalePointService.AssignEtrnAsync(documentId, salePointUid)` → `Result<EtrnIncomingView>`
  - `SalePointService.AssignVetisAsync(documentId, salePointUid)` → `Result<VetisIncomingView>`
  - `GET api/organization/{id}/sale-points/{uid}/usage` → `{ used }`
  - `POST api/etrn/incoming/{id}/sale-point`, `POST api/vetis/incoming/{id}/sale-point` с телом `{ salePointUid }`

- [x] **Step 1: Падающие тесты**

`AssignEtrnAsync` пишет uid на документ и возвращает карточку с именем точки. Неизвестный uid — «Точка не найдена в справочнике организации». Закрытая — «Точка закрыта: выберите открытую». Пустая строка очищает поле. `AssignVetisAsync` ведёт себя так же.

`UsedAsync` истинно, если uid есть хоть в одном из двух журналов. `OrganizationManagerService.Update`, у которого из списка исчезла используемая точка, возвращает «Точку «{name}» используют входящие документы: удалить нельзя, закройте её» и не пишет карточку. Неиспользуемая точка удаляется. При `Create` проверки нет.

- [x] **Step 2: Прогон**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter "FullyQualifiedName~SalePointServiceTests|FullyQualifiedName~OrganizationManagerServiceTests" -v q`

Expected: FAIL.

- [x] **Step 3: Сервис**

`SalePointService` собирается из `IOrganizationRepository`, `IEtrnIncomingRepository`, `IVetisIncomingRepository`, `EtrnIncomingQueryService`, `VetisIncomingQueryService`. Назначение: найти документ, взять организацию, проверить точку через `SalePointDirectory.Find` (непустой uid обязан существовать и быть открытым), записать, вернуть `GetAsync(id)` — имя и «закрыта» придут из справочника. Использование: два `AnyBySalePoint` с `||`.

- [x] **Step 4: Удаление на сохранении**

В `Update` до `Apply` снимается копия прежнего списка; после `Apply` для каждой исчезнувшей по uid точки вызывается `UsedAsync`; при `true` — отказ с именем точки, репозиторий не трогается. Ошибка чтения журнала — тоже отказ, карточка не пишется.

- [x] **Step 5: Эндпоинты**

`OrganizationController` получает `SalePointService` и отдаёт `{ used }`. `EtrnController`/`VetisController` — POST назначения, тело `SalePointCommand { SalePointUid }`, ответ — карточка документа.

- [x] **Step 6: Прогон и коммит**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter "FullyQualifiedName~SalePoint|FullyQualifiedName~OrganizationManagerServiceTests" -v q`

Expected: PASS. В `CreateSut` тестов организации добавляется подделка `SalePointService`/репозиториев журналов.

Commit: `new: ручное сопоставление торговой точки на документе`

---

### Task 6: Вкладка «Торговые точки» в карточке организации

**Files:**
- Create: `src/Presentation/WebApp/WebApp/wwwroot/js/utils/salePointFormat.js`
- Create: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/organization/organizationSalePoints.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/organization/organizationElementView.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/organization/organizationEtrnAccess.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/services/organizationService.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/organization/organizationElementView.test.js`
- Test: `src/tests/WebApp.Tests/organizationSalePoints.test.mjs`

**Interfaces:**
- Consumes: `salePoints` карточки, `GET api/organization/{id}/sale-points/{uid}/usage`
- Produces:
  - `salePointsBody(view)` — тело вкладки
  - `showSalePoints(view, points)`, `salePointsFromTable(view)` — строки ↔ полезная нагрузка
  - `salePointsToPayload(rows)`, `salePointsToRows(points)`, `salePointLabel(point)` — чистые функции для теста
  - `organizationService.salePointUsage(id, uid)`

- [x] **Step 1: Падающий тест преобразования**

`points → rows → payload` сохраняет uid, обрезает поля, выбрасывает синоним с пустым адресом, не теряет «закрыта». Новые строки уходят с `uid: ""` — uid выдаёт сервер.

- [x] **Step 2: Прогон**

Run: `node --test src/tests/WebApp.Tests/organizationSalePoints.test.mjs`

Expected: FAIL, модуля нет.

- [x] **Step 3: Вкладка**

Вкладка «Торговые точки» — первая в `organizationTabCells`: сигнатура становится `(salePoints, marking, vetis, etrn, vetisOn, etrnOn)`, вызов в `_tabs` и тесты `organizationElementView.test.js` обновляются. Вкладка видна всегда, от настроек сервера не зависит. Ячейка строится из `this._loadedSalePoints` (он заполняется в `showDialog` до `webix.ui`), а `showSalePoints` наполняет таблицу после — как `showEnterprises` у Ветис.

Таблица: «Название», «Адрес», справа значок удаления; закрытая точка — с пометкой «закрыта»; uid не показывается. Кнопка «Добавить» и клик по строке открывают одно окно поверх карточки: имя, адрес, ФИАС, таблица синонимов (адрес + удаление, кнопка «Добавить синоним»), флажок «Закрыта». «ОК» меняет строку таблицы; в базу точка попадает кнопкой «Сохранить» карточки. На время окна хоткеи карточки выключаются, как у окна письма Ветис.

- [x] **Step 4: Удаление**

Строка без uid удаляется сразу. С сохранённой точкой сначала `salePointUsage`: занята — `webix.confirm` «Точку используют документы. Закрыть точку?», при согласии строка остаётся с «закрыта»; свободна — удаляется. Ошибка запроса — сообщение, строка не удаляется.

- [x] **Step 5: Сохранение карточки**

`_collect` добавляет `salePoints: salePointsFromTable(this)`; при отсутствии вкладки отдаётся `_loadedSalePoints`, чтобы сохранение не стёрло справочник. Ошибку сервера карточка показывает текстом, как сейчас.

- [x] **Step 6: Прогон и коммит**

Run: `node --test src/tests/WebApp.Tests/organizationSalePoints.test.mjs src/Presentation/WebApp/WebApp/wwwroot/js/modules/organization/organizationElementView.test.js`

Expected: PASS

Commit: `new: вкладка торговых точек в карточке организации`

---

### Task 7: Журналы и карточки документов

**Files:**
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/utils/salePointFormat.js` — `salePointFilterOptions`, `salePointPickerOptions`
- Create: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/organization/salePointPicker.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisFilterBar.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/etrn/etrnFilterBar.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisIncomingTab.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/etrn/etrnIncomingTab.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisIncomingWindow.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/etrn/etrnIncomingWindow.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/services/vetisService.js`, `etrnService.js`
- Test: `src/tests/WebApp.Tests/salePoints.test.mjs`

**Interfaces:**
- Consumes: `salePointUid`/`salePointName`/`salePointClosed` строки, `salePoints` карточки, `POST …/sale-point`, `salePointLabel` из Task 6
- Produces:
  - `salePointFilterOptions(salePoints)` → «Все точки», `none` «Без точки», открытые, затем закрытые с пометкой
  - `salePointPickerOptions(salePoints)` → «Очистить» + открытые
  - `salePointBar({ prefix, document, assign, onChanged })` — строка карточки
  - `vetisService.assignSalePoint(id, uid)`, `etrnService.assignSalePoint(id, uid)`

- [x] **Step 1: Падающий тест**

`Все_точки_без_точки_и_закрытые_стоят_в_этом_порядке`: `salePointFilterOptions` отдаёт «Все точки», «Без точки», открытые по порядку справочника, затем закрытые с «закрыта». `salePointLabel` пустого uid — пустая строка, закрытой — с пометкой. В списке выбора только открытые и «Очистить».

- [x] **Step 2: Прогон**

Run: `node --test src/tests/WebApp.Tests/salePoints.test.mjs`

Expected: FAIL.

- [x] **Step 3: Отбор**

В `VetisFilterBar`/`EtrnFilterBar` — `richselect` точки после организации, выключен при «Все организации», перестраивается в `_organizationChanged` и `apply`. Значение уходит как `salePoint`: `none` для «Без точки», uid для точки, пусто — параметра нет.

- [x] **Step 4: Колонка**

В `vetisIncomingTab`/`etrnIncomingTab` — колонка «Торговая точка» через `salePointLabel`; пустой uid — пустая ячейка.

- [x] **Step 5: Карточка документа**

Строка «Торговая точка» — отдельная, сразу под шапкой (`INFO_ID`), потому что шапка скрывает пустые строки. Пусто — кнопка-значок без надписи, занято — кнопка «Изменить»; обе открывают окно со списком и «Очистить». Выбор сразу уходит POST, строка обновляет подпись, журнал перезагружается через `onChanged`. Ошибка — `webix.message`, подпись не меняется.

- [x] **Step 6: Прогон и коммит**

Run: `node --test src/tests/WebApp.Tests/salePoints.test.mjs src/tests/WebApp.Tests/vetisFilterBar.test.mjs`

Expected: PASS

Commit: `new: выбор торговой точки в журналах и карточках`

---

### Task 8: Сверка экрана

Автотестов браузера в репозитории нет. После Task 1–7 в браузере:

- вкладка «Торговые точки» первая; добавление, правка, «закрыта» и удаление работают; повторное открытие карточки показывает сохранённое; удаление используемой точки предлагает закрыть её;
- сохранение с одинаковым адресом двух открытых точек показывает текст ошибки сервера;
- журналы входящих Меркурия и ЭТрН показывают точку и «закрыта»; отбор «Без точки» и конкретная точка сужают список; при «Все организации» отбор выключен;
- в карточке входящего Меркурия и входящей ЭТрН строка «Торговая точка» стоит под шапкой; значок на пустом поле, «Изменить» и «Очистить» пишут значение сразу и видны после перезагрузки журнала;
- документ после очистки снова получает точку по адресу на следующем опросе.

Это не коммит. Если экран врёт, правка остаётся в задаче, которая это поле рисовала.
