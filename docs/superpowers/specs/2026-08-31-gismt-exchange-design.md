# Обмен fmu-api-central ↔ fmu-api-gismt

Дата: 2026-08-31

## Цель

Фоновый канал управления: Central отдаёт GisMt настройки, CouchDB и токены True API; GisMt отдаёт последний HTTP-статус обмена с Честным знаком **по каждой организации**. Документы, марки и остатки по этому каналу не едут — только CouchDB и репликация на инстансы fmu-api.

SignalR нет: прогресс в UI не нужен, повтор при недоступности GisMt делает воркер.

## Границы

Входит:

- HTTP-контракт GisMt (`PUT /api/exchange-state`, `GET /api/status`)
- клиент и воркер в Central
- поле `apiKey` в `GisMtSettings`
- поля последнего статуса на организации и колонка в списке

Не входит (отдельный этап): воркеры GisMt (документы, остатки, КИ), репликация CouchDB на fmu-api, перевод всего solution на net10.0.

Пока воркеров Честного знака нет, `GET /api/status` возвращает `organizations: []`. Колонка показывает «—». Контракт и запись в CouchDB уже работают.

## Направление

Central — клиент, GisMt — сервер. Адрес: `GisMtSettings.ServiceUrl` (по умолчанию `http://localhost:2577`).

UI и `TrueApiTokenLoaderWorker` HTTP не вызывают: ставят флаг, отправляет `GisMtExchangeWorker`.

## Контракт GisMt

База: `{serviceUrl}`. JSON, camelCase. Заголовок `X-Api-Key`, значение `GisMtSettings.ApiKey`. Если ключ пустой — заголовок не шлём, GisMt не проверяет. Если `serviceUrl` не loopback (`localhost`, `127.0.0.1`, `::1`) и ключ пустой — воркер Central не отправляет пакет, в лог Error (пароль CouchDB иначе уйдёт открытым HTTP).

### `PUT /api/exchange-state`

Тело:

```json
{
  "settings": {
    "enable": true,
    "mtDocumentsPollIntervalMinutes": 10,
    "markRetentionDays": 365,
    "documentsSyncDays": 1,
    "stockLoadEnabled": false,
    "stockLoadTime": "03:00:00"
  },
  "databaseConnection": {
    "enable": true,
    "netAddress": "",
    "userName": "",
    "password": "",
    "bulkBatchSize": 1000,
    "bulkParallelTasks": 4,
    "queryLimit": 1000000,
    "queryTimeout": 300
  },
  "tokens": [
    { "inn": "7700000000", "token": "...", "expired": "2026-09-01T12:00:00" }
  ]
}
```

`serviceUrl` и `apiKey` в пакет не входят.

`tokens` — только организации с включённым True API и живым токеном из `IApplicationState` (`LiveUntil >= now`, непустой `Token`). Организации без токена в пакет не попадают.

GisMt хранит пакет в памяти. На диск токены не пишет. После рестарта ждёт следующий `PUT`.

Пока `settings.enable == false` или пакета не было — воркеры Честного знака не запускаются.

Ответы: `200` + тело как у `GET /api/status`; `401` неверный ключ; `503` процесс ещё не готов принять пакет.

### `GET /api/status`

```json
{
  "organizations": [
    {
      "inn": "7700000000",
      "statusCode": 200,
      "description": "Документы: ок",
      "at": "2026-08-31T10:15:00"
    }
  ]
}
```

На организацию одна запись — **последний** ответ Честного знака (документы, остатки или КИ — что было крайним). `statusCode` — HTTP-код. Если HTTP 200, а в теле ошибка бизнеса, `statusCode` остаётся 200, смысл в `description`. Свои сбои GisMt: код `0`, описание вроде «нет токена», «таймаут».

## Воркер Central

`GisMtExchangeWorker` в `src/Infrastructure/GisMt/`, hosted service в WebApi.

Цикл 30 секунд, только если `gisMtSettings.enable`.

Флаг `GisMtPushPending` в `IApplicationState`:

- сохранение секции `gisMtSettings`
- успешное обновление токена в `TrueApiTokenLoaderWorker`

Каждый круг при `enable` всегда `PUT`, затем `GET`. Так GisMt после своего рестарта за 30 с снова получает токены и CouchDB, без ожидания нового сохранения настроек.

Флаг только сбрасывает ожидание до следующего круга (после Save и после обновления токена не ждать остаток 30 с).

Алгоритм круга:

1. Если не `enable` — выход.
2. Если не loopback и пустой `apiKey` — Error, флаг не снимать, `PUT`/`GET` не делать.
3. `PUT /api/exchange-state`:
   - 2xx — снять флаг
   - сеть / 5xx / 503 — Warning, флаг не снимать
   - 401 — Error, флаг не снимать, `GET` не делать
4. `GET /api/status`. По каждому `inn` найти организацию, сравнить код и описание с сохранёнными. `Update` в CouchDB только при отличии. `at` обновлять вместе с ними.
5. ИНН из ответа, которого нет в Central — пропуск. Организация без записи в ответе — старый статус не трогать.

Ошибки Central → GisMt (сеть, 401, 503) в список организаций **не** пишутся.

Таймаут `HttpClient` `"GisMt"`: 30 секунд, как у TrueApiIntegration.

## Данные организации

`OrganizationEntity` (CouchDB):

- `GisMtLastStatusCode` (`int?`)
- `GisMtLastStatusDescription` (`string`)
- `GisMtLastStatusAt` (`DateTime?`)

Те же поля в `OrganizationView` — только чтение. `Apply` из карточки организации их не меняет.

Список (`organizationListView.js`), порядок колонок:

1. Наименование
2. ГИС МТ
3. ИНН
4. Токен

Пустой статус — «—». Иначе `{code} {description}`. `2xx` — обычный цвет текста. Иначе (включая код `0`) — `#E74C3C`. Клик по колонке ничего не копирует. F5 перечитывает сохранённое, живого пуша нет.

## Слои

| Что | Слой | Куда |
|---|---|---|
| DTO пакета и статуса | Domain | `src/Core/Domain/GisMtExchange/` |
| поля статуса на организации | Domain | `OrganizationEntity`, `OrganizationView` |
| `apiKey` | Domain | `GisMtSettings` |
| флаг пуша, сбор пакета, запись статуса по ИНН | Application | `src/Core/Application/GisMtExchange/` |
| `IGisMtClient`, `HttpClient` `"GisMt"`, воркер | Infrastructure | `src/Infrastructure/GisMt/` (новый проект) |
| регистрация | Presentation | `WebApi/Program.cs` |
| контроллеры контракта | Presentation | `src/Presentation/GisMt` |
| поле ключа на форме ГИС МТ | WebApp | `gisMtSettingsView.js` |
| колонка списка | WebApp | `organizationListView.js` |

`IGisMtClient` в Domain рядом с DTO (как `ITrueApiAuthService`). Реализация — Infrastructure.

GisMt: **net10.0** (как заготовка). Остальной solution до релиза остаётся на net8.0; HTTP от TFM не зависит. В `.sln` проект GisMt добавить. Слушает `http://localhost:2577`. Локальный конфиг GisMt: порт (URL) и `apiKey`. Остальное только из `PUT`.

## UI настроек ГИС МТ

К существующим полям — «Ключ API» (`apiKey`), password-поле. Пустое значение допустимо для localhost.

Сохранение по-прежнему только `saveConfigurationSection("gisMtSettings")` и флаг пуша, без HTTP из запроса.

## Ошибки (сводка)

| Ситуация | Лог | Флаг пуша | Статус в списке организаций |
|---|---|---|---|
| GisMt недоступен | Warning | не снимать | не менять |
| 503 | Warning | не снимать | не менять |
| 401 | Error | не снимать | не менять |
| не loopback, пустой ключ | Error | не снимать | не менять |
| 2xx на PUT | — | снять | затем из GET |
| GET: код/описание по ИНН изменились | — | — | Update CouchDB |
| GET: без изменений | — | — | не писать |

Исключения только на верхней границе воркера и HTTP-клиента, без вложенных try/catch.
