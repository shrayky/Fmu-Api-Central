# Group Alert Channel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Канал оповещения хранится на группе инстансов; отправка идёт в канал группы, если он включён, иначе в глобальный.

**Architecture:** `AlertChannel` в Domain (резолв + проекция из `TelegramBotSetting`). На группе — поле `alertChannel`. `IMessageServiceFactory` выбирает telegram/max/ntfy в момент отправки. Шаблоны режутся по группе; стандартные алерты — по инстансу. Пороги и расписание остаются в глобальном `TelegramBotSetting`.

**Tech Stack:** .NET 10, xUnit, CSharpFunctionalExtensions, CouchDB.NET, Webix, Jint (скрипты шаблонов).

## Global Constraints

- Spec: `docs/superpowers/specs/2026-09-08-group-alert-channel-design.md`
- TDD: нет упавшего теста → нет продакшен-кода
- Комментарии только если неочевидно *почему*; к методам — по-русски
- Try/catch только на верхней границе
- Глобальный `TelegramBotSetting` не режем, канал из него не выносим
- Пороги, расписание, шаблоны как сущности не переносятся в группу
- Провайдеры только `telegram`, `max`, `ntfy`
- Текст теста канала: `Халло, мир!%0AСЧАСТЬЕ ДЛЯ ВСЕХ, ДАРОМ, И ПУСТЬ НИКТО НЕ УЙДЁТ ОБИЖЕННЫМ!` (как `/api/BotTest`)

## File structure

**Domain**

- `src/Core/Domain/Bot/AlertChannel.cs` — поля канала, `Resolve`, `FromBotSettings`
- `src/Core/Domain/Bot/IMessageServiceFactory.cs` — `For(BotProvidersEnum)`
- `src/Core/Domain/Entitys/InstanceGroup/InstanceGroupEntity.cs` — поле `AlertChannel`
- `src/Core/Domain/Entitys/InstanceGroup/Dto/InstanceGroupView.cs` — поле `alertChannel`
- `src/Core/Domain/Entitys/InstanceGroup/Interfaces/IInstanceGroupManagerService.cs` — `TestChannel`
- `src/Core/Domain/Entitys/AlertTemplates/Dto/AlertInstanceSnapshot.cs` — `GroupId`

**Application**

- `src/Core/Application/InstanceGroup/Services/InstanceGroupManagerService.cs` — persist, валидация, тест канала
- `src/Core/Application/AlertTemplates/AlertTemplateRunService.cs` — прогон по группе / fallback

**Infrastructure**

- `src/Infrastructure/Messages/Services/MessageServiceFactory.cs`
- `src/Infrastructure/Messages/Extensions/Registration.cs` — все три бота + фабрика
- `src/Infrastructure/Messages/Services/AlertsConstuctor.cs` — отправка в канал инстанса
- `src/Infrastructure/Messages/Workers/MessagesSendWorker.cs` — не выходить рано при `!bot.IsEnabled`
- `src/Infrastructure/Messages/Workers/AlertTemplateSendWorker.cs` — то же
- `src/Infrastructure/TelegramBot/Workers/TelegramMessagingWorker.cs` — фабрика вместо `IMessageService` (чтобы сборка не зависела от одиночного сервиса)

**Presentation**

- `src/Presentation/WebApi/Controllers/InstanceGroupController.cs` — `POST test-channel`
- `src/Presentation/WebApi/Controllers/BotTestController.cs` — фабрика
- `src/Presentation/WebApp/WebApp/wwwroot/js/modules/instanceGroup/instanceGroupElementView.js` — вкладки + Тест
- `src/Presentation/WebApp/WebApp/wwwroot/js/modules/instanceGroup/instanceGroupListView.js` — колонка «Канал»
- `src/Presentation/WebApp/WebApp/wwwroot/js/services/instanceGroupService.js` — `testChannel`

**Tests**

- `src/tests/Domain.Tests/AlertChannelTests.cs`
- `src/tests/Application.Tests/InstanceGroupManagerServiceTests.cs`
- `src/tests/Application.Tests/AlertChannelTestServiceTests.cs` — если тест вынесен; иначе методы в `InstanceGroupManagerServiceTests`
- `src/tests/Application.Tests/AlertTemplateRunServiceTests.cs`
- `src/tests/Application.Tests/AlertsConstuctorTests.cs`
- `src/tests/Application.Tests/Fakes/FakeMessageService.cs`
- `src/tests/Application.Tests/Fakes/FakeMessageServiceFactory.cs`
- `src/tests/Application.Tests/Application.Tests.csproj` — ссылка на `Messages.csproj`

---

### Task 1: Domain `AlertChannel`

**Files:**
- Create: `src/Core/Domain/Bot/AlertChannel.cs`
- Test: `src/tests/Domain.Tests/AlertChannelTests.cs`

**Interfaces:**
- Consumes: `TelegramBotSetting`, `BotProvidersEnum`
- Produces: `AlertChannel` (`IsEnabled`, `Provider`, `ChatId`, `BotToken`), `AlertChannel.Resolve(AlertChannel? groupChannel, AlertChannel global)`, `AlertChannel.FromBotSettings(TelegramBotSetting bot)`, `AlertChannel.Disabled()`

- [ ] **Step 1: Написать падающий тест**

```csharp
using Domain.Bot;
using Domain.Configuration.Options;

namespace Domain.Tests;

public class AlertChannelTests
{
    /// <summary>
    /// Включённый канал группы перекрывает глобальный.
    /// </summary>
    [Fact]
    public void Resolve_группа_включена_берёт_группу()
    {
        var group = new AlertChannel { IsEnabled = true, ChatId = 11, BotToken = "g" };
        var global = new AlertChannel { IsEnabled = true, ChatId = 99, BotToken = "glb" };

        var resolved = AlertChannel.Resolve(group, global);

        Assert.Equal(11, resolved.ChatId);
        Assert.Equal("g", resolved.BotToken);
    }

    [Fact]
    public void Resolve_группа_выключена_берёт_глобальный()
    {
        var group = new AlertChannel { IsEnabled = false, ChatId = 11 };
        var global = new AlertChannel { IsEnabled = true, ChatId = 99 };

        Assert.Equal(99, AlertChannel.Resolve(group, global).ChatId);
    }

    [Fact]
    public void Resolve_группы_нет_берёт_глобальный()
    {
        var global = new AlertChannel { IsEnabled = true, ChatId = 99 };

        Assert.Equal(99, AlertChannel.Resolve(null, global).ChatId);
    }

    [Fact]
    public void FromBotSettings_копирует_четыре_поля()
    {
        var bot = new TelegramBotSetting
        {
            IsEnabled = true,
            Provider = BotProvidersEnum.max,
            ChatId = 42,
            BotToken = "tok"
        };

        var channel = AlertChannel.FromBotSettings(bot);

        Assert.True(channel.IsEnabled);
        Assert.Equal(BotProvidersEnum.max, channel.Provider);
        Assert.Equal(42, channel.ChatId);
        Assert.Equal("tok", channel.BotToken);
    }
}
```

- [ ] **Step 2: Прогнать тест — должен упасть**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~AlertChannelTests -v q`

Expected: FAIL — тип `AlertChannel` не найден.

- [ ] **Step 3: Минимальная реализация**

`src/Core/Domain/Bot/AlertChannel.cs`:

```csharp
using System.Text.Json.Serialization;
using Domain.Configuration.Options;

namespace Domain.Bot;

public class AlertChannel
{
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("provider")]
    public BotProvidersEnum Provider { get; set; } = BotProvidersEnum.telegram;

    [JsonPropertyName("chatId")]
    public long ChatId { get; set; }

    [JsonPropertyName("botToken")]
    public string BotToken { get; set; } = string.Empty;

    public static AlertChannel Disabled() => new();

    public static AlertChannel FromBotSettings(TelegramBotSetting bot) => new()
    {
        IsEnabled = bot.IsEnabled,
        Provider = bot.Provider,
        ChatId = bot.ChatId,
        BotToken = bot.BotToken
    };

    /// <summary>
    /// Канал группы только если он включён; иначе запасной глобальный.
    /// </summary>
    public static AlertChannel Resolve(AlertChannel? groupChannel, AlertChannel global)
        => groupChannel is { IsEnabled: true } ? groupChannel : global;
}
```

- [ ] **Step 4: Прогнать тест — должен пройти**

Run: `dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~AlertChannelTests -v q`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Core/Domain/Bot/AlertChannel.cs src/tests/Domain.Tests/AlertChannelTests.cs
git commit -m "new: AlertChannel и резолв канала группы"
```

---

### Task 2: Поле на группе + валидация Create/Update

**Files:**
- Modify: `src/Core/Domain/Entitys/InstanceGroup/InstanceGroupEntity.cs`
- Modify: `src/Core/Domain/Entitys/InstanceGroup/Dto/InstanceGroupView.cs`
- Modify: `src/Core/Application/InstanceGroup/Services/InstanceGroupManagerService.cs` (`Apply`, `ToView`, `Create`, `Update`)
- Test: `src/tests/Application.Tests/InstanceGroupManagerServiceTests.cs`

**Interfaces:**
- Consumes: `AlertChannel` из Task 1
- Produces: `InstanceGroupEntity.AlertChannel`, `InstanceGroupView.AlertChannel` (`[JsonPropertyName("alertChannel")]`), отказ Create/Update если `isEnabled && chatId == 0`

- [ ] **Step 1: Написать падающий тест**

Фейки: `IInstanceGroupRepository` с `Dictionary<string, InstanceGroupEntity>`; остальные зависимости конструктора — заглушки с `NotImplementedException` (Create/Update их не зовут).

Текст ошибки: `"ID чата не может быть 0"`.

```csharp
[Fact]
public async Task Create_включённый_канал_без_chatId_отказывает()
{
    var sut = CreateSut(new FakeGroupRepository());
    var result = await sut.Create(new InstanceGroupView
    {
        Id = "g1",
        Name = "Группа",
        AlertChannel = new AlertChannel { IsEnabled = true, ChatId = 0, BotToken = "t" }
    });

    Assert.True(result.IsFailure);
    Assert.Equal("ID чата не может быть 0", result.Error);
}

[Fact]
public async Task Create_выключенный_канал_с_нулевым_chatId_пишет()
{
    var repo = new FakeGroupRepository();
    var sut = CreateSut(repo);

    var result = await sut.Create(new InstanceGroupView
    {
        Id = "g1",
        Name = "Группа",
        AlertChannel = new AlertChannel { IsEnabled = false, ChatId = 0 }
    });

    Assert.True(result.IsSuccess);
    Assert.False(repo.Store["g1"].AlertChannel.IsEnabled);
    Assert.Equal(0, repo.Store["g1"].AlertChannel.ChatId);
}

[Fact]
public async Task Update_включённый_канал_без_chatId_отказывает()
{
    var repo = new FakeGroupRepository();
    repo.Store["g1"] = new InstanceGroupEntity { Id = "g1", Name = "Группа" };
    var sut = CreateSut(repo);

    var result = await sut.Update(new InstanceGroupView
    {
        Id = "g1",
        Name = "Группа",
        AlertChannel = new AlertChannel { IsEnabled = true, ChatId = 0 }
    });

    Assert.True(result.IsFailure);
    Assert.False(repo.Store["g1"].AlertChannel.IsEnabled);
}

[Fact]
public async Task Create_пишет_канал()
{
    var repo = new FakeGroupRepository();
    var sut = CreateSut(repo);

    var result = await sut.Create(new InstanceGroupView
    {
        Id = "g1",
        Name = "Группа",
        AlertChannel = new AlertChannel
        {
            IsEnabled = true,
            Provider = BotProvidersEnum.ntfy,
            ChatId = 7,
            BotToken = "tok"
        }
    });

    Assert.True(result.IsSuccess);
    Assert.Equal(7, repo.Store["g1"].AlertChannel.ChatId);
    Assert.Equal(BotProvidersEnum.ntfy, repo.Store["g1"].AlertChannel.Provider);
}
```

`CreateSut` собирает `InstanceGroupManagerService` с `FakeGroupRepository` и пустыми заглушками остальных интерфейсов (как в `InstanceHandshakeServiceTests`).

- [ ] **Step 2: Прогнать тест — должен упасть**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~InstanceGroupManagerServiceTests -v q`

Expected: FAIL — нет `AlertChannel` на View/Entity или валидации.

- [ ] **Step 3: Минимальная реализация**

`InstanceGroupEntity`:

```csharp
public AlertChannel AlertChannel { get; set; } = AlertChannel.Disabled();
```

`InstanceGroupView`:

```csharp
[JsonPropertyName("alertChannel")]
public AlertChannel AlertChannel { get; set; } = new();
```

В `Create` и `Update` **до** `Apply`:

```csharp
if (data.AlertChannel.IsEnabled && data.AlertChannel.ChatId == 0)
    return Result.Failure("ID чата не может быть 0");
```

`Apply`: `entity.AlertChannel = data.AlertChannel ?? AlertChannel.Disabled();`

`ToView`: `AlertChannel = entity.AlertChannel ?? AlertChannel.Disabled()`

- [ ] **Step 4: Прогнать тест — должен пройти**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~InstanceGroupManagerServiceTests -v q`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Core/Domain/Entitys/InstanceGroup/InstanceGroupEntity.cs src/Core/Domain/Entitys/InstanceGroup/Dto/InstanceGroupView.cs src/Core/Application/InstanceGroup/Services/InstanceGroupManagerService.cs src/tests/Application.Tests/InstanceGroupManagerServiceTests.cs
git commit -m "new: канал оповещения на группе инстансов"
```

---

### Task 3: Фабрика отправителя и DI

**Files:**
- Create: `src/Core/Domain/Bot/IMessageServiceFactory.cs`
- Create: `src/Infrastructure/Messages/Services/MessageServiceFactory.cs`
- Modify: `src/Infrastructure/Messages/Extensions/Registration.cs`
- Modify: `src/Presentation/WebApi/Controllers/BotTestController.cs`
- Modify: `src/Infrastructure/TelegramBot/Workers/TelegramMessagingWorker.cs`
- Modify: `src/tests/Application.Tests/Application.Tests.csproj` — `<ProjectReference Include="..\..\Infrastructure\Messages\Messages.csproj" />`
- Create: `src/tests/Application.Tests/Fakes/FakeMessageService.cs`
- Create: `src/tests/Application.Tests/Fakes/FakeMessageServiceFactory.cs`
- Test: `src/tests/Application.Tests/MessageServiceFactoryTests.cs`

**Interfaces:**
- Consumes: `IMessageService`, `BotProvidersEnum`
- Produces: `IMessageServiceFactory.For(BotProvidersEnum provider)` → `Result<IMessageService>`; при неизвестном значении enum — `Result.Failure` с текстом `"Неизвестный провайдер {provider}"`, без исключения

- [ ] **Step 1: Написать падающий тест**

```csharp
public class MessageServiceFactoryTests
{
    [Fact]
    public void For_telegram_возвращает_telegram()
    {
        var telegram = new FakeMessageService("tg");
        var factory = new MessageServiceFactory(telegram, new FakeMessageService("max"), new FakeMessageService("ntfy"));

        var result = factory.For(BotProvidersEnum.telegram);

        Assert.True(result.IsSuccess);
        Assert.Same(telegram, result.Value);
    }

    [Fact]
    public void For_неизвестный_провайдер_отказывает()
    {
        var factory = new MessageServiceFactory(
            new FakeMessageService("tg"),
            new FakeMessageService("max"),
            new FakeMessageService("ntfy"));

        var result = factory.For((BotProvidersEnum)99);

        Assert.True(result.IsFailure);
        Assert.Contains("Неизвестный провайдер", result.Error);
    }
}
```

`FakeMessageService : IMessageService` — список `Sends` из `(BotToken, ChatId, Message)`, плюс `LastToken` / `LastChatId` / `LastMessage` как последние значения. `Send` возвращает `Result.Success()`. Один класс-хелпер в `src/tests/Application.Tests/Fakes/FakeMessageService.cs` — его же использовать в Task 4–6.

Конструктор фабрики: три параметра `IMessageService` (telegram, max, ntfy) — не три одинаковых сервиса из DI.

- [ ] **Step 2: Прогнать тест — должен упасть**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~MessageServiceFactoryTests -v q`

Expected: FAIL — нет `IMessageServiceFactory` / `MessageServiceFactory`.

- [ ] **Step 3: Минимальная реализация**

`IMessageServiceFactory`:

```csharp
public interface IMessageServiceFactory
{
    Result<IMessageService> For(BotProvidersEnum provider);
}
```

`MessageServiceFactory`:

```csharp
public class MessageServiceFactory : IMessageServiceFactory
{
    private readonly Dictionary<BotProvidersEnum, IMessageService> _map;

    public MessageServiceFactory(IMessageService telegram, IMessageService max, IMessageService ntfy)
    {
        _map = new()
        {
            [BotProvidersEnum.telegram] = telegram,
            [BotProvidersEnum.max] = max,
            [BotProvidersEnum.ntfy] = ntfy
        };
    }

    public Result<IMessageService> For(BotProvidersEnum provider)
        => _map.TryGetValue(provider, out var service)
            ? Result.Success(service)
            : Result.Failure<IMessageService>($"Неизвестный провайдер {provider}");
}
```

`Registration.AddBotService` — больше не регистрировать один `IMessageService`. Вместо этого:

```csharp
services.AddSingleton<TelegramBotService>();
services.AddSingleton<MaxBotService>();
services.AddSingleton<NtfyBotService>();
services.AddSingleton<IMessageServiceFactory>(sp => new MessageServiceFactory(
    sp.GetRequiredService<TelegramBotService>(),
    sp.GetRequiredService<MaxBotService>(),
    sp.GetRequiredService<NtfyBotService>()));
services.AddSingleton<IAlertMessageConstructor, AlertsConstuctor>();
```

`BotTestController`: вместо `IMessageService` — `IMessageServiceFactory`. В `Get`:

```csharp
var bot = settings.BotSettings;
if (!bot.IsEnabled)
    return BadRequest("Бот не подключен");

var service = _factory.For(bot.Provider);
if (service.IsFailure)
    return BadRequest(service.Error);

var sendResult = await service.Value.Send(bot.BotToken, bot.ChatId,
    "Халло, мир!%0AСЧАСТЬЕ ДЛЯ ВСЕХ, ДАРОМ, И ПУСТЬ НИКТО НЕ УЙДЁТ ОБИЖЕННЫМ!");
```

`TelegramMessagingWorker`: то же — `For(bot.Provider)`, затем `Send`. Сейчас воркер не подключается из `Program.cs`, но должен собираться.

`AlertsConstuctor` и `AlertTemplateRunService` пока оставляют `IMessageService` — сломается DI. **В этом же шаге** временно зарегистрировать `IMessageService` как telegram-сервис, чтобы решение собиралось до Task 5–6:

```csharp
services.AddSingleton<IMessageService>(sp => sp.GetRequiredService<TelegramBotService>());
```

Удалить эту строку в Task 5 и Task 6, когда оба потребителя перейдут на фабрику.

- [ ] **Step 4: Прогнать тесты и сборку**

Run:

```
dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~MessageServiceFactoryTests -v q
dotnet build FmuApiCentral.sln --no-restore
```

Expected: PASS, build 0 ошибок.

- [ ] **Step 5: Commit**

```bash
git add src/Core/Domain/Bot/IMessageServiceFactory.cs src/Infrastructure/Messages/Services/MessageServiceFactory.cs src/Infrastructure/Messages/Extensions/Registration.cs src/Presentation/WebApi/Controllers/BotTestController.cs src/Infrastructure/TelegramBot/Workers/TelegramMessagingWorker.cs src/tests/Application.Tests/Application.Tests.csproj src/tests/Application.Tests/MessageServiceFactoryTests.cs
git commit -m "new: фабрика отправителя оповещений по provider"
```

---

### Task 4: Тест канала группы (API)

**Files:**
- Modify: `src/Core/Domain/Entitys/InstanceGroup/Interfaces/IInstanceGroupManagerService.cs`
- Modify: `src/Core/Application/InstanceGroup/Services/InstanceGroupManagerService.cs`
- Modify: `src/Presentation/WebApi/Controllers/InstanceGroupController.cs`
- Test: `src/tests/Application.Tests/InstanceGroupManagerServiceTests.cs` (дописать факты)

**Interfaces:**
- Consumes: `IMessageServiceFactory` из Task 3, `AlertChannel` из Task 1
- Produces: `Task<Result> TestChannel(AlertChannel channel)` — без fallback на глобальный канал; при успехе `Send` с token/chatId из тела и текстом `/api/BotTest`

- [ ] **Step 1: Написать падающий тест**

Добавить `IMessageServiceFactory` в конструктор менеджера (после репозиториев). В старых тестах Task 2 передать `new FakeMessageServiceFactory()`.

```csharp
[Fact]
public async Task TestChannel_включённый_шлёт_в_поля_тела()
{
    var sender = new FakeMessageService("tg");
    var factory = new FakeMessageServiceFactory { Services = { [BotProvidersEnum.telegram] = sender } };
    var sut = CreateSut(new FakeGroupRepository(), factory);

    var result = await sut.TestChannel(new AlertChannel
    {
        IsEnabled = true,
        Provider = BotProvidersEnum.telegram,
        ChatId = 15,
        BotToken = "tok"
    });

    Assert.True(result.IsSuccess);
    Assert.Equal("tok", sender.LastToken);
    Assert.Equal(15, sender.LastChatId);
    Assert.Equal("Халло, мир!%0AСЧАСТЬЕ ДЛЯ ВСЕХ, ДАРОМ, И ПУСТЬ НИКТО НЕ УЙДЁТ ОБИЖЕННЫМ!", sender.LastMessage);
}

[Fact]
public async Task TestChannel_выключенный_не_шлёт()
{
    var sender = new FakeMessageService("tg");
    var factory = new FakeMessageServiceFactory { Services = { [BotProvidersEnum.telegram] = sender } };
    var sut = CreateSut(new FakeGroupRepository(), factory);

    var result = await sut.TestChannel(new AlertChannel { IsEnabled = false, ChatId = 15 });

    Assert.True(result.IsFailure);
    Assert.Equal("Бот не подключен", result.Error);
    Assert.Null(sender.LastMessage);
}

[Fact]
public async Task TestChannel_нулевой_chatId_не_шлёт()
{
    var sender = new FakeMessageService("tg");
    var factory = new FakeMessageServiceFactory { Services = { [BotProvidersEnum.telegram] = sender } };
    var sut = CreateSut(new FakeGroupRepository(), factory);

    var result = await sut.TestChannel(new AlertChannel { IsEnabled = true, ChatId = 0, BotToken = "t" });

    Assert.True(result.IsFailure);
    Assert.Equal("ID чата не может быть 0", result.Error);
    Assert.Null(sender.LastMessage);
}
```

`FakeMessageServiceFactory.For` — `TryGetValue` или `"Неизвестный провайдер"`.

- [ ] **Step 2: Прогнать тест — должен упасть**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~InstanceGroupManagerServiceTests.TestChannel -v q`

Expected: FAIL — нет `TestChannel`.

- [ ] **Step 3: Минимальная реализация**

Интерфейс + менеджер:

```csharp
public async Task<Result> TestChannel(AlertChannel channel)
{
    if (!channel.IsEnabled)
        return Result.Failure("Бот не подключен");
    if (channel.ChatId == 0)
        return Result.Failure("ID чата не может быть 0");

    var service = _messageServiceFactory.For(channel.Provider);
    if (service.IsFailure)
        return Result.Failure(service.Error);

    return await service.Value.Send(
        channel.BotToken,
        channel.ChatId,
        "Халло, мир!%0AСЧАСТЬЕ ДЛЯ ВСЕХ, ДАРОМ, И ПУСТЬ НИКТО НЕ УЙДЁТ ОБИЖЕННЫМ!");
}
```

Контроллер:

```csharp
[HttpPost("test-channel")]
public async Task<IActionResult> TestChannel([FromBody] AlertChannel channel)
{
    var result = await _manager.TestChannel(channel);
    return result.IsSuccess ? Ok() : BadRequest(result.Error);
}
```

- [ ] **Step 4: Прогнать тест — должен пройти**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~InstanceGroupManagerServiceTests -v q`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Core/Domain/Entitys/InstanceGroup/Interfaces/IInstanceGroupManagerService.cs src/Core/Application/InstanceGroup/Services/InstanceGroupManagerService.cs src/Presentation/WebApi/Controllers/InstanceGroupController.cs src/tests/Application.Tests/InstanceGroupManagerServiceTests.cs
git commit -m "new: тест канала оповещения группы"
```

---

### Task 5: Шаблоны — прогон по группе

**Files:**
- Modify: `src/Core/Domain/Entitys/AlertTemplates/Dto/AlertInstanceSnapshot.cs`
- Modify: `src/Core/Application/AlertTemplates/AlertTemplateRunService.cs`
- Modify: `src/Infrastructure/Messages/Workers/AlertTemplateSendWorker.cs`
- Test: `src/tests/Application.Tests/AlertTemplateRunServiceTests.cs`

**Interfaces:**
- Consumes: `IInstanceGroupRepository.All()`, `AlertChannel.Resolve` / `FromBotSettings`, `IMessageServiceFactory`
- Produces: группа с `isEnabled` — свой контекст (только её инстансы) и `Send` в её канал; остальные инстансы — один прогон в глобальный канал; глобальный выключен — общий прогон не шлёт; `Preview` по-прежнему на всех инстансах

- [ ] **Step 1: Написать падающий тест**

Скрипт шаблона: `return { items: instances.map(function (n) { return n.name; }) };`

Шаблон `Enabled = true`, слот расписания = час/минута `now`.

Инстансы: `a` в группе `g1`, `b` в группе `g2` (канал выключен), `c` без группы.

Группа `g1`: канал включён, `ChatId = 100`, token `g-tok`.  
Глобальный: включён, `ChatId = 200`, token `glb-tok`.

`FakeMessageService` пишет список `(token, chatId, message)`.

Ожидание:

- есть сообщение с именем `a` и `chatId == 100`
- есть сообщение с именем `b` или `c` (можно одним `Send` на item) и `chatId == 200`
- нет `Send` с `chatId == 100` и текстом `b`/`c`

Второй факт: глобальный `IsEnabled = false` — сообщения только в `100` (`a`), `b`/`c` не уходят.

Реальный `AlertDatasetScriptExecutor` (не мок). `IAlertTemplateManager.EnsureDefaults` → `Result.Success()`. Статистика: `GetByDateRange` → пустой список. `ILogger` — `NullLogger<AlertTemplateRunService>.Instance`.

`AlertInstanceSnapshot` должен получить `GroupId` (для нарезки в C#; скрипту не обязателен).

- [ ] **Step 2: Прогнать тест — должен упасть**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~AlertTemplateRunServiceTests -v q`

Expected: FAIL — всё уходит в один глобальный chatId.

- [ ] **Step 3: Минимальная реализация**

`AlertInstanceSnapshot`: `public string GroupId { get; init; } = string.Empty;` + заполнение из `entity.GroupId` в `ToInstanceSnapshot`.

В конструктор `AlertTemplateRunService` добавить `IInstanceGroupRepository` и заменить `IMessageService` на `IMessageServiceFactory`.

`RunDueTemplates`:

1. `EnsureDefaults`, взять due-шаблоны. Если пусто — успех.
2. Загрузить все инстансы и все группы. `global = AlertChannel.FromBotSettings(bot)`.
3. Для каждой группы с `AlertChannel.IsEnabled`: `BuildContext` только из инстансов с `GroupId == group.Id`, выполнить due-шаблоны, `Send` через `For(group.AlertChannel.Provider)` и token/chatId группы. Ошибка `Send` — лог, дальше.
4. `fallback` = инстансы без группы, с неизвестной группой или с выключенным каналом группы. Если `global.IsEnabled` — один `BuildContext(fallback)` и те же due-шаблоны в глобальный канал. Если глобальный выключен — шаг 4 пропустить (не ошибка).

Не выходить в начале метода по `!bot.IsEnabled` — иначе группы с каналом молчат.

`AlertTemplateSendWorker`: убрать `if (bot.IsEnabled)` вокруг `RunDueTemplates`. Вызывать всегда (шаблоны сами решают, куда слать).

Удалить временный `AddSingleton<IMessageService>` только после Task 6, если `AlertsConstuctor` ещё зависит от него.

- [ ] **Step 4: Прогнать тест — должен пройти**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~AlertTemplateRunServiceTests -v q`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Core/Domain/Entitys/AlertTemplates/Dto/AlertInstanceSnapshot.cs src/Core/Application/AlertTemplates/AlertTemplateRunService.cs src/Infrastructure/Messages/Workers/AlertTemplateSendWorker.cs src/tests/Application.Tests/AlertTemplateRunServiceTests.cs
git commit -m "new: шаблоны оповещений по каналу группы"
```

---

### Task 6: Стандартные алерты по каналу инстанса

**Files:**
- Modify: `src/Infrastructure/Messages/Services/AlertsConstuctor.cs`
- Modify: `src/Infrastructure/Messages/Workers/MessagesSendWorker.cs`
- Modify: `src/Infrastructure/Messages/Extensions/Registration.cs` — убрать `IMessageService`, если ещё остался
- Test: `src/tests/Application.Tests/AlertsConstuctorTests.cs`

**Interfaces:**
- Consumes: `IInstanceManagerService.All()` (`InstanceMonitoringInformation.Group.Id`), `IInstanceGroupRepository.All()`, `IMessageServiceFactory`, `AlertChannel.Resolve`
- Produces: каждое сообщение инстанса уходит в резолвнутый канал; `!IsEnabled` у резолвнутого — не шлём; ошибка одного `Send` не останавливает остальные

- [ ] **Step 1: Написать падающий тест**

Два узла offline (`LastUpdated` старше `OfflineNodeAlertInterval` часов), группы `g1`/`g2` с разными `ChatId` (10 и 20), оба канала включены. Глобальный тоже включён, `ChatId = 99`. Пороги ЛМ/ТС выключены/пусты, чтобы ушли только offline-сообщения.

`AlertsConstuctor` сейчас резолвит `IInstanceManagerService` из `IServiceScopeFactory`. Для теста сменить конструктор на прямые зависимости: `ILogger<AlertsConstuctor>`, `IInstanceManagerService`, `IInstanceGroupRepository`, `IMessageServiceFactory`. Скоуп больше не нужен.

Ожидание: два `Send`, chatId 10 и 20, нет chatId 99. В тексте есть имена узлов.

Третий факт в том же файле: узел без группы + глобальный включён → `Send` с глобальным chatId.

- [ ] **Step 2: Прогнать тест — должен упасть**

Run: `dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter FullyQualifiedName~AlertsConstuctorTests -v q`

Expected: FAIL — всё уходит в один канал / нет нужного конструктора.

- [ ] **Step 3: Минимальная реализация**

Загрузить группы в словарь `Id → AlertChannel`. `global = FromBotSettings(bot)`.

Каждый `Check*` должен вернуть не `List<string>`, а `List<(string GroupId, string Message)>` (`node.Group?.Id ?? ""`).

Цикл отправки:

```csharp
foreach (var (groupId, message) in messages)
{
    groups.TryGetValue(groupId, out var groupChannel);
    var channel = AlertChannel.Resolve(groupChannel, global);
    if (!channel.IsEnabled)
        continue;

    var service = _factory.For(channel.Provider);
    if (service.IsFailure)
    {
        _logger.LogError("{Error}", service.Error);
        continue;
    }

    var sendResult = await service.Value.Send(channel.BotToken, channel.ChatId, message);
    if (sendResult.IsFailure)
        _logger.LogError("Сообщения в бот: не удалось отправить сообщение {message} боту: {err}!",
            message, sendResult.Error);
}
```

`MessagesSendWorker`: убрать ранний выход `if (!bot.IsEnabled) delay 10 min`. Всегда считать delay по `Scheduler` (пустой scheduler — как сейчас, 10 минут). Иначе при выключенном глобальном канале группы не получат стандартные алерты.

Удалить `services.AddSingleton<IMessageService>(...)` из `Registration`.

- [ ] **Step 4: Прогнать тесты Task 5–6 и сборку**

Run:

```
dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter "FullyQualifiedName~AlertTemplateRunServiceTests|FullyQualifiedName~AlertsConstuctorTests|FullyQualifiedName~InstanceGroupManagerServiceTests" -v q
dotnet build FmuApiCentral.sln
```

Expected: PASS, 0 ошибок.

- [ ] **Step 5: Commit**

```bash
git add src/Infrastructure/Messages/Services/AlertsConstuctor.cs src/Infrastructure/Messages/Workers/MessagesSendWorker.cs src/Infrastructure/Messages/Extensions/Registration.cs src/tests/Application.Tests/AlertsConstuctorTests.cs
git commit -m "new: стандартные оповещения в канал группы инстанса"
```

---

### Task 7: UI группы — вкладки, Тест, колонка

**Files:**
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/instanceGroup/instanceGroupElementView.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/modules/instanceGroup/instanceGroupListView.js`
- Modify: `src/Presentation/WebApp/WebApp/wwwroot/js/services/instanceGroupService.js`

**Interfaces:**
- Consumes: `POST /api/instanceGroup/test-channel`, `InstanceGroupView.alertChannel`
- Produces: вкладки «Основная» / «Канал оповещения»; одно сохранение; колонка «Канал»

Автотестов Webix в solution нет — проверка ручная + сборка.

- [ ] **Step 1: Сервис `testChannel`**

В `instanceGroupService.js`:

```javascript
async testChannel(channel) {
    const data = await this.authService.makeAuthenticatedRequest(`${this.apiEndpoint}/test-channel`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(channel)
    });

    if (!data.result) {
        throw new Error(data.error || data.value);
    }

    return true;
}
```

- [ ] **Step 2: Диалог с вкладками**

`instanceGroupElementView.js`:

- окно `width: 640`
- `tabview` с ячейками «Основная» и «Канал оповещения»
- основная: текущие имя / автообновление / схема (`Text`, `CheckBox`, `richselect` как сейчас)
- канал: `CheckBox` «Использовать» (`instanceGroupChannelEnabled`); блок полей (`richselect` telegram/max/ntfy, `Number` chatId, `Text` токен) с `disabled: !isEnabled`; `onChange` чекбокса включает/выключает блок (как `_setAlertFieldsEnabled` в `alertSettingsView.js`)
- кнопка «Тест» на вкладке канала: читает поля формы канала; если `!isEnabled` — `webix.message` «Бот не подключен»; если `chatId == 0` — «ID чата не может быть 0»; иначе `instanceGroupService.testChannel({ isEnabled, provider, chatId, botToken })`; успех — `webix.message` type success; ошибка — type error. Глобальный канал не вызывать (`/api/BotTest` не трогать)
- кнопки Сохранить/Отмена под вкладками
- `_send`: валидация имени как сейчас; если канал включён и `chatId == 0` — ошибка, не POST; в payload:

```javascript
alertChannel: {
    isEnabled: !!$$(this.NAMES.channelEnabled).getValue(),
    provider: $$(this.NAMES.provider).getValue() || "telegram",
    chatId: parseInt($$(this.NAMES.chatId).getValue()) || 0,
    botToken: $$(this.NAMES.botToken).getValue() || ""
}
```

- `showDialog(editedData)` подставляет `editedData.alertChannel` (по умолчанию выключен)
- id контролов с префиксом `instanceGroup`, чтобы не пересечься с глобальной формой оповещений

- [ ] **Step 3: Колонка в списке**

В `_dataTable` после схемы:

```javascript
{
    id: "alertChannel",
    header: "Канал",
    width: 120,
    template: (obj) => obj.alertChannel?.isEnabled ? (obj.alertChannel.provider || "") : ""
}
```

После create/edit в колбэке список уже получает `alertChannel` из формы — колонка обновится.

- [ ] **Step 4: Проверка**

`dotnet build FmuApiCentral.sln` — 0 ошибок.

`dotnet test src/tests/Domain.Tests/Domain.Tests.csproj --filter FullyQualifiedName~AlertChannelTests`

`dotnet test src/tests/Application.Tests/Application.Tests.csproj --filter "FullyQualifiedName~InstanceGroup|FullyQualifiedName~AlertTemplate|FullyQualifiedName~AlertsConstuctor|FullyQualifiedName~MessageServiceFactory"`

В UI: создать группу без канала — колонка пустая; включить канал без chatId — не сохраняет; с chatId — в списке `telegram`; Тест при выключенном канале — ошибка, не глобальный чат.

- [ ] **Step 5: Commit**

```bash
git add src/Presentation/WebApp/WebApp/wwwroot/js/modules/instanceGroup/instanceGroupElementView.js src/Presentation/WebApp/WebApp/wwwroot/js/modules/instanceGroup/instanceGroupListView.js src/Presentation/WebApp/WebApp/wwwroot/js/services/instanceGroupService.js
git commit -m "new: вкладки канала и тест в карточке группы"
```

---

## Self-review

| Spec | Task |
|---|---|
| `AlertChannel` + Resolve + FromBotSettings | 1 |
| Поле на группе, CouchDB по умолчанию выключен | 2 |
| Валидация `isEnabled` + `chatId == 0` API | 2 |
| Фабрика, три бота в DI | 3 |
| Тест канала из формы, без fallback | 4, 7 |
| Шаблоны по группе + общий fallback | 5 |
| Стандартные алерты по инстансу | 6 |
| Воркеры не глушат группы при выключенном глобальном | 5, 6 |
| Вкладки, колонка типа канала | 7 |
| Глобальный канал / пороги / шаблоны не переносятся | вне задач — не трогаем `alertSettingsView` кроме косвенно BotTest |
