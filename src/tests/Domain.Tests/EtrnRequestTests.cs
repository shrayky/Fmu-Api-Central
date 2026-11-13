using Domain.Configuration;
using Domain.Etrn;

namespace Domain.Tests;

public class EtrnRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Успешный_запрос_ровно_сутки_остаётся()
    {
        Assert.False(EtrnRequestRetention.IsExpired(completed: true, Now.AddDays(-1), Now));
    }

    [Fact]
    public void Успешный_запрос_на_секунду_старше_удаляется()
    {
        Assert.True(EtrnRequestRetention.IsExpired(completed: true, Now.AddDays(-1).AddSeconds(-1), Now));
    }

    [Fact]
    public void Ошибочный_запрос_ровно_семь_суток_остаётся()
    {
        Assert.False(EtrnRequestRetention.IsExpired(completed: false, Now.AddDays(-7), Now));
    }

    [Fact]
    public void Ошибочный_запрос_на_секунду_старше_удаляется()
    {
        Assert.True(EtrnRequestRetention.IsExpired(completed: false, Now.AddDays(-7).AddSeconds(-1), Now));
    }

    [Fact]
    public void Запись_журнала_не_хранит_сессию_и_сертификат()
    {
        var entity = new EtrnRequestEntity();

        Assert.DoesNotContain("session", typeof(EtrnRequestEntity).GetProperties().Select(item => item.Name), StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("certificate", typeof(EtrnRequestEntity).GetProperties().Select(item => item.Name), StringComparer.OrdinalIgnoreCase);
    }
}

public class EtrnSettingsTests
{
    [Fact]
    public void По_умолчанию_интервал_пять_минут()
    {
        var settings = new EtrnSettings();

        Assert.Equal(5, settings.IncomingFetchIntervalMinutes);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.IncomingInterval());
    }

    [Fact]
    public void Ноль_при_сохранении_отвергается()
    {
        var settings = new EtrnSettings { IncomingFetchIntervalMinutes = 0 };

        var result = settings.Check();

        Assert.True(result.IsFailure);
        Assert.Equal("Как часто загружать ЭТрН: нужно целое число больше нуля", result.Error);
    }

    [Fact]
    public void Уже_записанный_ноль_читается_как_пять_минут()
    {
        var settings = new EtrnSettings { IncomingFetchIntervalMinutes = 0 };

        Assert.Equal(TimeSpan.FromMinutes(5), settings.IncomingInterval());
    }

    [Fact]
    public void Положительный_интервал_принимается()
    {
        Assert.True(new EtrnSettings { IncomingFetchIntervalMinutes = 15 }.Check().IsSuccess);
    }

    [Fact]
    public void Настройки_сервера_содержат_ЭТрН()
    {
        Assert.NotNull(new Parameters().EtrnSettings);
    }

    [Fact]
    public void По_умолчанию_ЭТрН_выключена_со_сроками_сутки_и_неделя()
    {
        var settings = new EtrnSettings();
        var stored = System.Text.Json.JsonSerializer.Deserialize<EtrnSettings>("{}");

        Assert.False(settings.Enable);
        Assert.False(stored!.Enable);
        Assert.Equal(1, settings.RequestRetentionDays);
        Assert.Equal(7, settings.FailedRequestRetentionDays);
        Assert.Equal(TimeSpan.FromDays(1), settings.CompletedRetention());
        Assert.Equal(TimeSpan.FromDays(7), settings.FailedRetention());
    }

    [Fact]
    public void Ноль_в_сроке_выполненных_запросов_отвергается()
    {
        var settings = new EtrnSettings { RequestRetentionDays = 0 };

        var result = settings.Check();

        Assert.True(result.IsFailure);
        Assert.Equal("Хранить выполненные запросы: нужно целое число больше нуля", result.Error);
    }

    [Fact]
    public void Ноль_в_сроке_запросов_с_ошибкой_отвергается()
    {
        var settings = new EtrnSettings { FailedRequestRetentionDays = 0 };

        var result = settings.Check();

        Assert.True(result.IsFailure);
        Assert.Equal("Хранить запросы с ошибкой или без ответа: нужно целое число больше нуля", result.Error);
    }

    [Fact]
    public void Уже_записанный_ноль_срока_читается_как_значение_по_умолчанию()
    {
        var settings = new EtrnSettings { RequestRetentionDays = 0, FailedRequestRetentionDays = 0 };

        Assert.Equal(TimeSpan.FromDays(1), settings.CompletedRetention());
        Assert.Equal(TimeSpan.FromDays(7), settings.FailedRetention());
    }
}
