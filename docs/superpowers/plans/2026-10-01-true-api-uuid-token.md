# True API UUID Token Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `simpleSignIn` запрашивает единый токен UUID, в кэш кладётся `uuidToken`, срок берётся из `expireDate`.

**Architecture:** Разбор ответа и тело запроса — чистые функции в Domain (`TrueApiUnitedSignIn`), их проверяют тесты без HTTP и КриптоПро. `TrueApiAuthService` только подписывает `data` и отправляет тело. `OrganizationManagerService` и `TrueApiTokenLoaderWorker` пишут в `IApplicationState` срок из сессии, а не `DateTime.Now + 8 часов`. Потребители (Bearer, обмен с GisMt, `GET /api/ts/token/inn`) не меняются: им по-прежнему нужна строка.

**Tech Stack:** .NET 10, xUnit, System.Text.Json, CSharpFunctionalExtensions.

## Global Constraints

- Spec текущего потока: `docs/true-api-auth.md`
- Контракт ГИС МТ: `POST /api/v3/true-api/auth/simpleSignIn`, `unitedToken: true` → в ответе `uuidToken` и `expireDate` (`yyyy-MM-ddTHH:mm:ss.SSSZ`)
- TDD: нет упавшего теста → нет продакшен-кода
- Комментарий только если неочевидно почему; к методам — по-русски
- Try/catch только на верхней границе (`FinishAuth` уже ловит сеть)
- JWT из поля `token` не сохранять и не подставлять вместо `uuidToken`
- `details` не отправлять
- `inn` по-прежнему только если не пустой
- `LiveUntil` — локальное время: `AppStateService.TrueApiToken` сравнивает его с `DateTime.Now` по тикам, без перевода Kind
- Поле `TrueApiTokenDefaults.LifeHours` удалить. `RefreshIntervalMinutes` и `StartDelayMinutes` не трогать
- Воркер не получает новую проверку срока: `AppStateService.TrueApiToken` уже отдаёт пустой токен, когда `LiveUntil < DateTime.Now`, и воркер запрашивает новый
- На диск токен не пишется

## File structure

**Domain** — контракт единого токена, без HTTP

- Create: `src/Core/Domain/TrueApiIntegration/TrueApiSession.cs` — `Token` + `LiveUntil`
- Create: `src/Core/Domain/TrueApiIntegration/TrueApiUnitedSignIn.cs` — тело `simpleSignIn` и разбор `uuidToken` / `expireDate`
- Modify: `src/Core/Domain/TrueApiIntegration/Interfaces/ITrueApiAuthService.cs` — `Result<TrueApiSession>`
- Modify: `src/Core/Domain/TrueApiIntegration/TrueApiTokenDefaults.cs` — убрать `LifeHours`

**Infrastructure** — проводка уже проверенных функций

- Modify: `src/Infrastructure/TrueApiIntegration/Models/AuthData.cs` — `uuidToken`, `expireDate`
- Modify: `src/Infrastructure/TrueApiIntegration/Services/TrueApiAuthService.cs` — тело с `unitedToken`, разбор через `TrueApiUnitedSignIn`

**Application** — срок сессии в кэш

- Modify: `src/Core/Application/Organization/Services/OrganizationManagerService.cs`
- Modify: `src/Infrastructure/TrueApiIntegration/Workers/TrueApiTokenLoaderWorker.cs`

**Tests**

- Create: `src/tests/Domain.Tests/TrueApiUnitedSignInTests.cs`
- Modify: `src/tests/Application.Tests/OrganizationManagerServiceTests.cs`

**Docs**

- Modify: `docs/true-api-auth.md`

---

### Task 1: Разбор uuidToken и тело simpleSignIn

**Files:**
- Create: `src/Core/Domain/TrueApiIntegration/TrueApiSession.cs`
- Create: `src/Core/Domain/TrueApiIntegration/TrueApiUnitedSignIn.cs`
- Test: `src/tests/Domain.Tests/TrueApiUnitedSignInTests.cs`

**Interfaces:**
- Consumes: ничего
- Produces:
  - `TrueApiSession(string Token, DateTime LiveUntil)`
  - `TrueApiUnitedSignIn.Body(string uuid, string signedData, string? inn)` → `TrueApiSignInBody`
  - `TrueApiUnitedSignIn.Parse(string? uuidToken, string? expireDate)` → `Result<TrueApiSession>`
  - `TrueApiSignInBody` с JSON-именами `uuid`, `data`, `inn`, `unitedToken`. `inn` не пишется, если null

- [ ] **Step 1: Write the failing test**

`src/tests/Domain.Tests/TrueApiUnitedSignInTests.cs`

```csharp
using System.Text.Json;
using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class TrueApiUnitedSignInTests
{
    /// <summary>
    /// AppState сравнивает LiveUntil с DateTime.Now по тикам, поэтому срок переводим в локальное время.
    /// </summary>
    [Fact]
    public void Parse_берёт_uuid_и_локальный_срок()
    {
        var result = TrueApiUnitedSignIn.Parse(
            "123e4567-e89b-12d3-a456-426655440000",
            "2026-10-10T00:00:00.123Z");

        Assert.True(result.IsSuccess);
        Assert.Equal("123e4567-e89b-12d3-a456-426655440000", result.Value.Token);
        Assert.Equal(
            DateTimeOffset.Parse("2026-10-10T00:00:00.123Z").LocalDateTime,
            result.Value.LiveUntil);
        Assert.Equal(DateTimeKind.Local, result.Value.LiveUntil.Kind);
    }

    [Fact]
    public void Parse_без_uuidToken_отказ()
    {
        var result = TrueApiUnitedSignIn.Parse(null, "2026-10-10T00:00:00.123Z");

        Assert.True(result.IsFailure);
        Assert.Equal("True API не вернул токен UUID", result.Error);
    }

    [Fact]
    public void Parse_jwt_вместо_uuid_отказ()
    {
        var result = TrueApiUnitedSignIn.Parse(
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e.mk",
            "2026-10-10T00:00:00.123Z");

        Assert.True(result.IsFailure);
        Assert.Equal("True API вернул uuidToken не в формате UUID", result.Error);
    }

    [Fact]
    public void Parse_без_срока_отказ()
    {
        var result = TrueApiUnitedSignIn.Parse("123e4567-e89b-12d3-a456-426655440000", " ");

        Assert.True(result.IsFailure);
        Assert.Equal("True API не вернул срок действия токена", result.Error);
    }

    [Fact]
    public void Parse_битый_срок_отказ()
    {
        var result = TrueApiUnitedSignIn.Parse("123e4567-e89b-12d3-a456-426655440000", "завтра");

        Assert.True(result.IsFailure);
        Assert.Equal("True API вернул некорректный срок действия токена", result.Error);
    }

    [Fact]
    public void Body_запрашивает_единый_токен_и_не_шлёт_пустой_inn()
    {
        var withInn = TrueApiUnitedSignIn.Body("uuid-1", "c2ln", "7700000000");
        var withoutInn = TrueApiUnitedSignIn.Body("uuid-1", "c2ln", "");

        var json = JsonSerializer.Serialize(withInn);
        Assert.Contains("\"unitedToken\":true", json);
        Assert.Contains("\"uuid\":\"uuid-1\"", json);
        Assert.Contains("\"data\":\"c2ln\"", json);
        Assert.Contains("\"inn\":\"7700000000\"", json);
        Assert.DoesNotContain("details", json);

        var emptyInn = JsonSerializer.Serialize(withoutInn);
        Assert.DoesNotContain("inn", emptyInn);
        Assert.Contains("\"unitedToken\":true", emptyInn);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~TrueApiUnitedSignInTests -v q`

Expected: FAIL, тип `TrueApiUnitedSignIn` не найден.

- [ ] **Step 3: Write minimal implementation**

`src/Core/Domain/TrueApiIntegration/TrueApiSession.cs`

```csharp
namespace Domain.TrueApiIntegration;

public sealed record TrueApiSession(string Token, DateTime LiveUntil);
```

`src/Core/Domain/TrueApiIntegration/TrueApiUnitedSignIn.cs`

```csharp
using System.Globalization;
using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration;

public static class TrueApiUnitedSignIn
{
    public static TrueApiSignInBody Body(string uuid, string signedData, string? inn)
    {
        var normalizedInn = (inn ?? string.Empty).Trim();
        return new TrueApiSignInBody
        {
            Uuid = uuid,
            Data = signedData,
            Inn = normalizedInn.Length == 0 ? null : normalizedInn,
            UnitedToken = true
        };
    }

    /// <summary>
    /// LiveUntil в локальном времени: кэш сравнивает его с DateTime.Now по тикам.
    /// </summary>
    public static Result<TrueApiSession> Parse(string? uuidToken, string? expireDate)
    {
        var token = (uuidToken ?? string.Empty).Trim();
        if (token.Length == 0)
            return Result.Failure<TrueApiSession>("True API не вернул токен UUID");

        if (!Guid.TryParse(token, out _))
            return Result.Failure<TrueApiSession>("True API вернул uuidToken не в формате UUID");

        var rawExpire = (expireDate ?? string.Empty).Trim();
        if (rawExpire.Length == 0)
            return Result.Failure<TrueApiSession>("True API не вернул срок действия токена");

        if (!DateTimeOffset.TryParse(
                rawExpire,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var expires))
            return Result.Failure<TrueApiSession>("True API вернул некорректный срок действия токена");

        return Result.Success(new TrueApiSession(token, expires.LocalDateTime));
    }
}

public sealed class TrueApiSignInBody
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; init; } = string.Empty;

    [JsonPropertyName("inn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Inn { get; init; }

    [JsonPropertyName("unitedToken")]
    public bool UnitedToken { get; init; }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~TrueApiUnitedSignInTests -v q`

Expected: PASS, 6 tests.

- [ ] **Step 5: Commit**

```powershell
git add src/Core/Domain/TrueApiIntegration/TrueApiSession.cs src/Core/Domain/TrueApiIntegration/TrueApiUnitedSignIn.cs src/tests/Domain.Tests/TrueApiUnitedSignInTests.cs
git commit -m "Разбор uuidToken и тела simpleSignIn для единого токена True API"
```

---

### Task 2: Запрос UUID и срок в кэше

**Files:**
- Modify: `src/Core/Domain/TrueApiIntegration/Interfaces/ITrueApiAuthService.cs`
- Modify: `src/Infrastructure/TrueApiIntegration/Models/AuthData.cs`
- Modify: `src/Infrastructure/TrueApiIntegration/Services/TrueApiAuthService.cs`
- Modify: `src/Core/Application/Organization/Services/OrganizationManagerService.cs`
- Modify: `src/Infrastructure/TrueApiIntegration/Workers/TrueApiTokenLoaderWorker.cs`
- Modify: `src/Core/Domain/TrueApiIntegration/TrueApiTokenDefaults.cs`
- Modify: `src/tests/Application.Tests/OrganizationManagerServiceTests.cs`
- Modify: `docs/true-api-auth.md`

**Interfaces:**
- Consumes: `TrueApiUnitedSignIn.Body`, `TrueApiUnitedSignIn.Parse`, `TrueApiSession` из Task 1
- Produces: `ITrueApiAuthService.GenerateToken` → `Task<Result<TrueApiSession>>`. В `TrueApiToken` попадает `session.Token` (`uuidToken`) и `session.LiveUntil`

- [ ] **Step 1: Write the failing test**

В `OrganizationManagerServiceTests` добавить тест. Общий `FakeTrueApiAuthService` пока возвращает `Result<string>` — тест не соберётся, это красный прогон на смену контракта.

```csharp
/// <summary>
/// Срок кэша берётся из сессии Честного знака, а не из фиксированных 8 часов.
/// </summary>
[Fact]
public async Task Token_сохраняет_uuid_и_срок_сессии()
{
    var repository = new FakeOrganizationRepository();
    repository.Store["o1"] = new OrganizationEntity
    {
        Id = "o1",
        Name = "Орг",
        Inn = "7700000000",
        TrueApiIntegrationSettings = new TrueApiIntegrationSettings
        {
            Enable = true,
            Password = "pwd",
            DigitalSignature = "serial"
        }
    };
    var liveUntil = new DateTime(2026, 10, 10, 7, 0, 0, DateTimeKind.Local);
    var state = new RecordingApplicationState();
    var sut = new OrganizationManagerService(
        repository,
        new SessionTrueApiAuthService(new TrueApiSession("123e4567-e89b-12d3-a456-426655440000", liveUntil)),
        state,
        new FakeGroupRepository());

    var result = await sut.Token("7700000000");

    Assert.True(result.IsSuccess);
    Assert.Equal("123e4567-e89b-12d3-a456-426655440000", result.Value.Token);
    Assert.Equal(liveUntil, result.Value.Expired);
    Assert.Equal("123e4567-e89b-12d3-a456-426655440000", state.SavedToken);
    Assert.Equal(liveUntil, state.SavedUntil);
}

private sealed class SessionTrueApiAuthService : ITrueApiAuthService
{
    private readonly TrueApiSession _session;

    public SessionTrueApiAuthService(TrueApiSession session) => _session = session;

    public Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber)
        => Task.FromResult(Result.Success(_session));
}

private sealed class RecordingApplicationState : IApplicationState
{
    public string SavedToken { get; private set; } = string.Empty;
    public DateTime SavedUntil { get; private set; }

    public void DbStateUpdate(bool isOnline) { }
    public bool DbState() => true;
    public void UpdateNeedRestart(bool need) { }
    public bool NeedRestart() => false;

    public void UpdateTrueApiToken(string inn, string token, DateTime lifeUntil)
    {
        SavedToken = token;
        SavedUntil = lifeUntil;
    }

    public TrueApiToken TrueApiToken(string inn) => new();
    public IReadOnlyList<TrueApiToken> TrueApiTokens() => [];
    public void MarkGisMtPushPending() { }
    public bool GisMtPushPending() => false;
    public void ClearGisMtPushPending() { }
}
```

И поправить существующий фейк в том же файле, иначе проект тестов не соберётся после смены интерфейса (это шаг 3, вместе с интерфейсом):

```csharp
public Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber)
    => Task.FromResult(Result.Failure<TrueApiSession>("не используется в тесте"));
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~OrganizationManagerServiceTests.Token_сохраняет_uuid_и_срок_сессии -v q`

Expected: FAIL сборкой. `GenerateToken` ещё `Task<Result<string>>`, `SessionTrueApiAuthService` не реализует интерфейс. Либо, если интерфейс уже сменён вручную, FAIL по утверждению: `Expired` равен `DateTime.Now + 8 часов`, а не `2026-10-10 07:00`.

- [ ] **Step 3: Write minimal implementation**

`ITrueApiAuthService.cs` — возвращаемый тип и комментарий:

```csharp
using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration.Interfaces;

public interface ITrueApiAuthService
{
    /// <summary>
    /// Получает единый токен UUID: auth/key, подпись КриптоПро, simpleSignIn с unitedToken.
    /// </summary>
    Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber);
}
```

`AuthData.cs` — добавить поля, `Token` оставить: при ошибке Честный знак его не присылает, разбор успеха идёт через `uuidToken`.

```csharp
using System.Text.Json.Serialization;

namespace TrueApiIntegration.Models;

public record AuthData
{
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("uuidToken")]
    public string UuidToken { get; set; } = string.Empty;

    [JsonPropertyName("expireDate")]
    public string ExpireDate { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
```

`TrueApiAuthService`: добавить `using Domain.TrueApiIntegration;`. `GenerateToken` и `FinishAuth` возвращают `Result<TrueApiSession>`. Все `Result.Failure<string>` в этих двух методах заменить на `Result.Failure<TrueApiSession>`.

```csharp
public async Task<Result<TrueApiSession>> GenerateToken(string inn, string password, string signatureNumber)
{
    var data = await DataForEncrypt();
    if (data.IsFailure)
        return Result.Failure<TrueApiSession>(data.Error);

    var encrypted = Encrypt(data.Value.Data, signatureNumber, inn, password);
    if (encrypted.IsFailure)
        return Result.Failure<TrueApiSession>(encrypted.Error);

    return await FinishAuth(encrypted.Value, data.Value.Uuid, inn);
}
```

В `FinishAuth` вместо `new DataWithUuid` отправлять тело из Domain. Успех — `Parse`, поле `token` не читать.

```csharp
private async Task<Result<TrueApiSession>> FinishAuth(string encodedData, string requestId, string inn)
{
    using var httpClient = _httpClientFactory.CreateClient("TrueApiIntegration");
    httpClient.BaseAddress = new Uri(Url);

    var data = TrueApiUnitedSignIn.Body(requestId, encodedData, inn);

    try
    {
        var answer = await httpClient.PostAsJsonAsync(SignInPath, data);
        if (answer == null)
            throw new Exception($"Пустой ответ от {SignInPath}");

        if (!answer.IsSuccessStatusCode)
            throw new Exception($"{SignInPath} вернул код ошибки {answer.StatusCode}");

        var rawJson = await answer.Content.ReadAsStringAsync();
        _logger.LogDebug("Ответ simpleSignIn : {Json}", rawJson);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
        var answerData = await JsonHelpers.DeserializeAsync<AuthData>(stream);
        if (answerData == null)
            throw new Exception($"Ошибка преобразования ответа в {SignInPath}");

        var parsed = TrueApiUnitedSignIn.Parse(answerData.UuidToken, answerData.ExpireDate);
        if (parsed.IsFailure)
            return Result.Failure<TrueApiSession>(
                string.IsNullOrEmpty(answerData.ErrorMessage) ? parsed.Error : answerData.ErrorMessage);

        return parsed;
    }
    catch (Exception ex)
    {
        _logger.LogError("Ошибка получения данных авторизации в true api {err}", ex);
        return Result.Failure<TrueApiSession>($"Ошибка получения данных авторизации в true api {ex.Message}");
    }
}
```

`OrganizationManagerService.Token` — убрать `AddHours`:

```csharp
if (generated.IsFailure)
    return Result.Failure<TrueApiTokenView>(generated.Error);

var session = generated.Value;
_applicationState.UpdateTrueApiToken(normalizedInn, session.Token, session.LiveUntil);

return Result.Success(ToTokenView(new TrueApiToken
{
    Inn = normalizedInn,
    Token = session.Token,
    LiveUntil = session.LiveUntil
}));
```

`TrueApiTokenLoaderWorker.RefreshTokens` — то же для сессии, `MarkGisMtPushPending` оставить:

```csharp
var token = await _authService.GenerateToken(inn, trueApiSettings.Password, trueApiSettings.DigitalSignature);
if (token.IsFailure)
{
    _logger.LogWarning("Не удалось получить токен true api для {inn}: {error}", inn, token.Error);
    continue;
}

var session = token.Value;
_applicationState.UpdateTrueApiToken(inn, session.Token, session.LiveUntil);
_applicationState.MarkGisMtPushPending();

_logger.LogInformation("Для {inn} получен новый токен, который действует до {tokenLifeUntil}", inn, session.LiveUntil);
```

`TrueApiTokenDefaults.cs` — удалить `LifeHours`:

```csharp
namespace Domain.TrueApiIntegration;

public static class TrueApiTokenDefaults
{
    public const int RefreshIntervalMinutes = 10;

    public const int StartDelayMinutes = 2;
}
```

Проверить, что `LifeHours` больше нигде не читается.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~OrganizationManagerServiceTests -v q`

Expected: PASS, включая `Token_сохраняет_uuid_и_срок_сессии`.

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~TrueApiUnitedSignInTests -v q`

Expected: PASS.

- [ ] **Step 5: Обновить docs/true-api-auth.md**

Раздел «Что получает проект»: токен — UUID из `uuidToken`, тело содержит `unitedToken: true`, срок — `expireDate` в локальном `LiveUntil`. Убрать абзац про 8 часов и `LifeHours`.

Раздел «3. Ключ сессии»: в примере тела есть `"unitedToken": true`. Пример успеха — объект с `uuidToken` и `expireDate`. Поле `token` (JWT) не кэшируется.

Раздел «Запрос UUID, которого в коде нет» удалить: этот запрос теперь и есть код.

Сроки ГИС МТ не переписывать: JWT на стороне Честного знака ещё до конца 2026, проект его больше не запрашивает.

- [ ] **Step 6: Commit**

```powershell
git add src/Core/Domain/TrueApiIntegration/Interfaces/ITrueApiAuthService.cs src/Core/Domain/TrueApiIntegration/TrueApiTokenDefaults.cs src/Infrastructure/TrueApiIntegration/Models/AuthData.cs src/Infrastructure/TrueApiIntegration/Services/TrueApiAuthService.cs src/Core/Application/Organization/Services/OrganizationManagerService.cs src/Infrastructure/TrueApiIntegration/Workers/TrueApiTokenLoaderWorker.cs src/tests/Application.Tests/OrganizationManagerServiceTests.cs docs/true-api-auth.md
git commit -m "Запрашивать единый токен UUID True API и хранить срок из expireDate"
```
