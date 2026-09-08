using System.Text.Json;
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

    [Fact]
    public void Json_Web_provider_строка_telegram_и_max()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var telegramJson = JsonSerializer.Serialize(
            new AlertChannel { Provider = BotProvidersEnum.telegram }, options);
        var maxJson = JsonSerializer.Serialize(
            new AlertChannel { Provider = BotProvidersEnum.max }, options);

        Assert.Equal(JsonValueKind.String, JsonDocument.Parse(telegramJson).RootElement.GetProperty("provider").ValueKind);
        Assert.Equal("telegram", JsonDocument.Parse(telegramJson).RootElement.GetProperty("provider").GetString());
        Assert.Equal("max", JsonDocument.Parse(maxJson).RootElement.GetProperty("provider").GetString());

        var fromTelegram = JsonSerializer.Deserialize<AlertChannel>("""{"provider":"telegram"}""", options);
        var fromMax = JsonSerializer.Deserialize<AlertChannel>("""{"provider":"max"}""", options);

        Assert.Equal(BotProvidersEnum.telegram, fromTelegram!.Provider);
        Assert.Equal(BotProvidersEnum.max, fromMax!.Provider);
    }
}
