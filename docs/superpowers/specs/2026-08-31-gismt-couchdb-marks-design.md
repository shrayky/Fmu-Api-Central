# Документы и остатки ГИС МТ в CouchDB Central

Дата: 2026-08-31

Дополнение к `2026-08-31-gismt-exchange-design.md`. Канал Central → GisMt (пакет настроек, CouchDB, токены) и статус `GET /api/status` уже есть.

## Цель

Central отдаёт настройки обмена. GisMt по ним ходит в Честный знак, **сама пишет** документы и марки в CouchDB Central. View в WebApp — отдельный этап.

## Границы

Входит:

- одинаковые сущности и интерфейсы репозиториев в Central и Family GisMt (копия с fmu-api)
- CouchDB-репозитории и две базы в обоих проектах
- запись из GisMt после загрузки УПД и остатков
- фоновые циклы документов и остатков по полям пакета
- ручное меню «Действия» в Central без изменений контракта POST

Не входит:

- экраны списка документов/марок в WebApp
- HTTP callback с телами марок в Central
- запись в базы инстанса `fmu-api-gis-mt-documents` / `fmu-api-gis-mt-marks`
- общий NuGet: копируем код в оба репозитория

## Базы

Имена в `DatabaseNames` обоих проектов:

| Константа | Имя |
|---|---|
| `GisMtDocumentsDbName` | `fmu-api-central-gismt-documents` |
| `GisMtMarksDbName` | `fmu-api-central-gismt-marks` |

Это CouchDB **Central** (адрес из `databaseConnection` пакета). Базы инстанса fmu-api не трогаем.

Индексы mango — как в fmu-api `DatabaseIndexes` (поля `data.number`, `data.loadedAt`, `data.cis`, `data.sGtin`, `data.productGroup`, `data.infoLoadedAt`, cleanup).

Обёртка документа CouchDB — как сейчас в Central: `UniversalDocument<T>` с полем `data`.

## Сущности

Копия `FmuApiDomain.GisMt.Entities` из `D:\Csharp\FMU-API`.

`GisMtDocumentEntity` (`IHaveStringId`):

- `Id` = `Number` (номер УПД в ЧЗ)
- `Number`, `DocDate`, `Type`, `Status`, `SenderInn`, `ReceiverInn`, `ProductGroup`, `OrganisationInn`, `MarksCount`, `LoadedAt`

`GisMtMarkEntity` (`IHaveStringId`):

- `Id` = `SGtin`
- `SGtin`, `Cis`, `Gtin`, `OwnerInn`, `OwnerName`, `ProducerInn`, `Status`, `Sold`, `ExpireDate`, `ProductGroup`, `ProductGroupId`, `IsTracking`, `SourceDocumentId`, `OrganisationInn`, `InfoLoadedAt`
- вычисляемое `IsExpired` — как в fmu-api, в CouchDB не писать, если там так же не сериализуется; иначе то же поведение, что у fmu-api

Маппинг из `cises/info` — копия `GisMtMarkMapper.FromCisInfo` (включая `IsTracking = false`).

## Репозитории

Интерфейсы — копия fmu-api:

`IGisMtDocumentRepository`: `Get`, `Exists`, `Save` (нет записи → Create, есть → Update; пустой Id → `Number`).

`IGisMtMarkRepository`: `Get`, `Save`, `SaveRange` (`CreateBulkAsync`), `ChangeState`, `GetExpiredForCleanup`, `Delete`, `Search`.

`GisMtMarkSearchResult` — те же поля, что в fmu-api.

Central: репозитории в существующем `src/Infrastructure/CouchDb`, две коллекции в `Context`, имена в `DatabaseNames.All()`, создание баз в `EnsureDatabasesExists`.

GisMt: тот же паттерн (`UniversalDocument`, `BaseCouchDbRepository` или эквивалент). Подключение **только** из `databaseConnection` пакета в памяти, не из локального appsettings GisMt.

Если `databaseConnection.enable == false` или пустой `netAddress` — не писать, в статус организации код `0`, описание вроде «CouchDB выключена».

Смена адреса/логина/пароля в следующем `PUT` — пересоздать клиент CouchDB.

## Запись (как fmu-api)

Документы (`GisMtDocumentsSyncService`):

1. Для входящего УПД, если `Exists(number)` — пропуск.
2. `doc/info` → КИ → пачки `cises/info` (по 1000) → `SaveRange`, `sourceDocumentId` = номер УПД.
3. `Save` `GisMtDocumentEntity`, `LoadedAt` = UtcNow.

Остатки (`GisMtStockLoadService`):

- `cises/search` статус `INTRODUCED` → `SaveRange`, `sourceDocumentId` = `"stock"`.
- Документ в `fmu-api-central-gismt-documents` не создаём.

После синхронизации документов — очистка марок по `markRetentionDays` (`GetExpiredForCleanup` + `Delete`), как в fmu-api.

GisMt сейчас грузит марки в DTO и шлёт callback. Callback с массивом марок не используем. Пустой `callbackUrl` по-прежнему no-op. Счётчики и HTTP-код ЧЗ — только через уже существующий `GET /api/status`.

## Расписание (GisMt)

Пока `settings.enable == false` или пакета нет — воркеры ЧЗ (кроме ожидания пакета) не ходят в ЧЗ.

Документы: интервал `mtDocumentsPollIntervalMinutes` (если &lt; 1 → 10). Период: `documentsSyncDays` календарных дней включая сегодня. По каждому ИНН с живым токеном в пакете.

Остатки: если `stockLoadEnabled` — раз в сутки в `stockLoadTime` (локальное время процесса GisMt), все ИНН с живым токеном.

Товарные группы: текущий `GisMtStatusProbeWorker` без изменения смысла.

Меню Central «Действия» ставит те же операции в очередь GisMt (`POST /api/gismt/...`). Токен обязателен (уже сделано). Даты документов для ручного запуска — как сейчас, из `documentsSyncDays`.

## Central (этот этап)

Сущности + репозитории + базы в схеме. API списка и WebApp view — следующий этап. Фоновый обмен по-прежнему только `PUT` / `GET status`.

## Слои

| Что | Central | Family GisMt |
|---|---|---|
| Entity, mapper, интерфейсы репо | Domain `Entitys/GisMt/` | Domain `GisMt/Entities/` (те же типы) |
| Репозитории, имена баз, индексы | Infrastructure/CouchDb | Infrastructure/CouchDb (новый в GisMt) |
| Сохранение после ЧЗ | — | Application (`CisInfoSaver`, правки Documents/Stock) |
| Клиент CouchDB из пакета | свой конфиг | Application/Infrastructure по `IGisMtCentralExchangeState` |
| View | не в этом этапе | — |

## Ошибки

Один try/catch на верхней границе операции/воркера. Сбой записи CouchDB — `Result.Failure`, статус организации через `GisMtHonestSignStatus.Apply` / `RecordStatus`, без вложенных try.

## Проверка

- Имена баз в обоих проектах совпадают с таблицей выше.
- Поля сущностей совпадают с fmu-api.
- Повтор той же марки/документа обновляет тот же `_id`.
- Уже загруженный номер УПД не качается снова (`Exists`).
- При выключенном `databaseConnection` записи нет.
