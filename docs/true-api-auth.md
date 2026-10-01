# Аутентификация True API

Дата: 2026-10-01

Источник контракта: единая аутентификация ГИС МТ, методы `/auth/key` и `/auth/simpleSignIn`.

## Что получает проект

Токен в формате **UUID** — поле `uuidToken` из ответа `simpleSignIn`. JWT из поля `token` проект не сохраняет и не подставляет вместо него.

`TrueApiAuthService` отправляет `unitedToken: true`, поэтому сервер выдаёт UUID. Срок берётся из `expireDate` ответа и кладётся в `TrueApiToken.LiveUntil` в локальном времени: кэш сравнивает его с `DateTime.Now` по тикам.

Ограничение срока задаёт сам Честный знак: для UUID он не больше 10 часов и дополнительно ограничен сроком УКЭП и МЧД. Фиксированного запаса в проекте больше нет, `TrueApiTokenDefaults.LifeHours` удалён.

## Сроки форматов на стороне ГИС МТ

- Токен UUID без предварительного `GET /auth/key` выдавался до сентября 2026. Этот путь в проекте не используется.
- JWT через пару «uuid — data» принимается до конца 2026 года; проект его больше не запрашивает.
- UUID через ту же пару запрашивается явно: `unitedToken: true`.

## Поток

Базовый URL: `https://markirovka.crpt.ru`. Префикс методов: `/api/v3/true-api`.

Кто вызывает `ITrueApiAuthService.GenerateToken`:

- `TrueApiTokenLoaderWorker` — для организаций с `TrueApiIntegrationSettings.Enable` и пустым токеном в `IApplicationState`. Повтор раз в 10 минут (`RefreshIntervalMinutes`). Уже полученный токен воркер не обновляет, пока строка токена не пустая.
- `OrganizationManagerService.Token` — `GET /api/ts/token/inn?inn=`. Сначала кэш, при пустом токене тот же `GenerateToken`.

Оба места пишут результат в память процесса (`AppStateService`), на диск токен не сохраняется.

### 1. Пара «uuid — data»

`GET /api/v3/true-api/auth/key`

Ответ (`DataWithUuid`):

```json
{
  "uuid": "a63ff582-b723-4da7-958b-453da27a6c62",
  "data": "GNUFBAZBMPIUUMLXNMIOGSHTGFXZMT"
}
```

`uuid` — идентификатор этой попытки входа, не токен сессии. `data` — строка, которую подписывает УКЭП.

### 2. Подпись

`data` подписывается присоединённой подписью (`CpSignedCms`, `detached: false`) сертификатом из хранилища `CurrentUser/My`.

Сертификат выбирается так:

- срок `NotAfter` ещё не вышел;
- издатель не содержит `DO_NOT_TRUST`;
- если в настройках организации задан `DigitalSignature` — совпадение серийного номера;
- иначе сертификат с ИНН организации: для ИП субъект содержит `ОГРНИП` и ИНН, для ЮЛ — `ИНН ЮЛ={inn}`.

Пароль контейнера (`TrueApiIntegrationSettings.Password`) передаётся в ключ ГОСТ, если он не пустой. Подпись уходит в `simpleSignIn` как base64.

### 3. Ключ сессии

`POST /api/v3/true-api/auth/simpleSignIn`

Тело собирает `TrueApiUnitedSignIn.Body` (`TrueApiSignInBody`, camelCase, `inn` пишется только если не пустой):

```json
{
  "uuid": "a63ff582-b723-4da7-958b-453da27a6c62",
  "data": "<присоединённая подпись base64>",
  "inn": "7700000000",
  "unitedToken": true
}
```

`details` не отправляется.

Успешный ответ, который разбирает `AuthData`:

```json
{
  "token": "string",
  "uuidToken": "123e4567-e89b-12d3-a456-426655440000",
  "expireDate": "2026-10-10T00:00:00.123Z"
}
```

Рабочий токен — `uuidToken`, он и попадает в кэш. Поле `token` (JWT) не кэшируется.

Разбор успеха — `TrueApiUnitedSignIn.Parse`: пустой или не-UUID `uuidToken` и пустой либо нечитаемый `expireDate` дают отказ, даже при HTTP 200. Ошибки читаются из `error_message` (также `code`, `description`), текст Честного знака приоритетнее сообщения разбора. Новый запрос всегда выдаёт новый токен, старый не продлевается; отзыв МЧД аннулирует UUID на стороне ГИС МТ.

## Где токен используется дальше

Строка из `uuidToken` кладётся в `TrueApiToken.Token`. Срок — `LiveUntil`.

Дальше она уходит без разбора формата:

- `Authorization: Bearer` в `CrptStatisticsClient` (нарушения и штрафы);
- пакет обмена с fmu-api-gismt: `{ "inn", "token", "expired" }`;
- ручная операция ГИС МТ (`GisMtExchangeService.ManualOperation`);
- ответ `GET /api/ts/token/inn`: `{ "token", "expired" }`.

Потребители не отличают UUID от JWT — им нужна только строка.
