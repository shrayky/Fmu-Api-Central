using CSharpFunctionalExtensions;
using Domain.Configuration.Options;
using Domain.Entitys.Organization;
using Domain.Vetis;

namespace Domain.Tests;

public class VetisApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void From_переносит_запрос_и_ответ_обмена()
    {
        var exchange = new VetisExchange
        {
            Operation = "GetVetDocumentChangesList",
            LocalTransactionId = "tx-1",
            ApplicationId = "app-1",
            Status = "COMPLETED",
            Completed = true,
            Parameters = "с 2026-10-01 по 2026-10-29",
            RequestXml = "<request/>",
            ResponseXml = "<response/>"
        };

        var entity = VetisApplicationEntity.From(exchange, "o1", "e1", Now);

        Assert.Equal("tx-1", entity.Id);
        Assert.Equal("GetVetDocumentChangesList", entity.Operation);
        Assert.Equal("app-1", entity.ApplicationId);
        Assert.Equal("COMPLETED", entity.Status);
        Assert.True(entity.Completed);
        Assert.Equal("o1", entity.OrganizationId);
        Assert.Equal("e1", entity.EnterpriseGuid);
        Assert.Equal("с 2026-10-01 по 2026-10-29", entity.Parameters);
        Assert.Equal("<request/>", entity.RequestXml);
        Assert.Equal("<response/>", entity.ResponseXml);
        Assert.Equal(Now.ToUnixTimeSeconds(), entity.CreatedAt);
        Assert.True(entity.HasResponse);
    }

    [Fact]
    public void From_без_статуса_и_идентификатора_создаёт_их()
    {
        var entity = VetisApplicationEntity.From(new VetisExchange { Error = "Нет связи" }, "o1", "", Now);

        Assert.Equal("NOT_SENT", entity.Status);
        Assert.False(entity.Completed);
        Assert.Equal("Нет связи", entity.Error);
        Assert.False(string.IsNullOrWhiteSpace(entity.Id));
    }

    [Fact]
    public void From_без_ответа_не_считает_ответ_полученным()
    {
        Assert.False(VetisApplicationEntity.From(new VetisExchange(), "o1", "", Now).HasResponse);
        Assert.True(VetisApplicationEntity.From(new VetisExchange { ResponseXml = "<r/>" }, "o1", "", Now).HasResponse);
    }
}

public class VetisSettingsScheduleTests
{
    [Fact]
    public void Значения_по_умолчанию()
    {
        var settings = new VetisSettings();

        Assert.Equal(TimeSpan.FromMinutes(5), settings.IncomingInterval());
        Assert.Equal(100, settings.IncomingPageSize());
        Assert.Equal(TimeSpan.FromDays(1), settings.CompletedRetention());
        Assert.Equal(TimeSpan.FromDays(7), settings.FailedRetention());
        Assert.Equal(TimeSpan.FromMinutes(15), settings.AutoQuenchInterval());
        Assert.Equal(20, settings.AutoQuenchBatch());
        Assert.False(settings.RequireVehicleForQuench);
    }

    [Fact]
    public void Неположительные_значения_заменяются_значениями_по_умолчанию()
    {
        var settings = new VetisSettings
        {
            IncomingFetchIntervalMinutes = 0,
            IncomingFetchPageSize = -3,
            ApplicationRetentionDays = 0,
            FailedApplicationRetentionDays = 0
        };

        Assert.Equal(TimeSpan.FromMinutes(5), settings.IncomingInterval());
        Assert.Equal(100, settings.IncomingPageSize());
        Assert.Equal(TimeSpan.FromDays(1), settings.CompletedRetention());
        Assert.Equal(TimeSpan.FromDays(7), settings.FailedRetention());
        Assert.Equal(TimeSpan.FromMinutes(15), settings.AutoQuenchInterval());
        Assert.Equal(20, settings.AutoQuenchBatch());
    }

    [Fact]
    public void Автогашение_не_сохраняется_с_неположительным_периодом_или_пакетом()
    {
        Assert.True(new VetisSettings { AutoQuenchIntervalMinutes = 0, AutoQuenchBatchSize = 20 }.CheckAutoQuenchNumbers().IsFailure);
        Assert.Equal(
            "Сколько входящих документов гасить за запуск: нужно целое число больше нуля.",
            new VetisSettings { AutoQuenchIntervalMinutes = 15, AutoQuenchBatchSize = 0 }.CheckAutoQuenchNumbers().Error);
        Assert.True(new VetisSettings { AutoQuenchIntervalMinutes = 15, AutoQuenchBatchSize = -1 }.CheckAutoQuenchNumbers().IsFailure);
        Assert.True(new VetisSettings { AutoQuenchIntervalMinutes = 15, AutoQuenchBatchSize = 20 }.CheckAutoQuenchNumbers().IsSuccess);
    }

    [Fact]
    public void Размер_страницы_не_больше_предела_Ветис()
    {
        Assert.Equal(100, new VetisSettings { IncomingFetchPageSize = 5000 }.IncomingPageSize());
        Assert.Equal(20, new VetisSettings { IncomingFetchPageSize = 20 }.IncomingPageSize());
    }
}

public class VetisCursorTests
{
    [Fact]
    public void Merge_не_обрезает_момент_курсора_площадки_до_даты()
    {
        var stored = new List<OrganizationEnterprise>
        {
            new() { Guid = "e1", Name = "Склад", IncomingFrom = "2026-10-20T08:30:00+07:00", Participates = true }
        };

        var merged = OrganizationVetisScope.Merge(stored, [new VetisEnterprise { Guid = "e1", Name = "Склад" }]);

        Assert.Equal("2026-10-20T08:30:00+07:00", merged.Single().IncomingFrom);
    }

    [Fact]
    public void Apply_переносит_автогашение_и_не_трогает_площадки()
    {
        var current = new OrganizationVetisSettings
        {
            Enterprises = [new OrganizationEnterprise { Guid = "e1", Name = "Склад", Participates = true }]
        };
        var update = new OrganizationVetisSettings
        {
            AutoQuench = true,
            AutoQuenchDelayHours = 4,
            Enterprises = [new OrganizationEnterprise { Guid = "e1", Name = "Склад", Participates = true }]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.True(current.AutoQuench);
        Assert.Equal(4, current.AutoQuenchDelayHours);
        Assert.Equal("e1", current.Enterprises.Single().Guid);
    }

    [Fact]
    public void Задержка_автогашения_по_умолчанию_час_и_ноль_не_принимается()
    {
        Assert.Equal(1, new OrganizationVetisSettings().AutoQuenchDelayHours);
        Assert.Equal(1, new OrganizationVetisSettings { AutoQuenchDelayHours = 0 }.DelayHours());
        Assert.True(new OrganizationVetisSettings { AutoQuenchDelayHours = 0 }.CheckDelay().IsFailure);
        Assert.True(new OrganizationVetisSettings { AutoQuenchDelayHours = 3 }.CheckDelay().IsSuccess);
    }

    [Fact]
    public void Apply_не_обрезает_момент_курсора_площадки_до_даты()
    {
        var current = new OrganizationVetisSettings();
        var update = new OrganizationVetisSettings
        {
            Enterprises = [new OrganizationEnterprise { Guid = "e1", IncomingFrom = "2026-10-20T08:30:00+07:00", Participates = true }]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("2026-10-20T08:30:00+07:00", current.Enterprises.Single().IncomingFrom);
    }

    [Fact]
    public void Курсор_площадки_не_заменяется_общей_датой_организации()
    {
        var current = new OrganizationVetisSettings();
        var update = new OrganizationVetisSettings
        {
            IncomingFrom = "2026-01-01",
            Enterprises = [new OrganizationEnterprise { Guid = "e1", IncomingFrom = "2026-10-20T08:30:00+07:00", Participates = true }]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("2026-10-20T08:30:00+07:00", current.Enterprises.Single().IncomingFrom);
    }

    [Fact]
    public void Apply_оставляет_момент_курсора_если_карточка_прислала_тот_же_день()
    {
        var current = new OrganizationVetisSettings
        {
            Enterprises = [new OrganizationEnterprise { Guid = "e1", IncomingFrom = "2026-10-25T09:30:00+07:00", Participates = true }]
        };
        var update = new OrganizationVetisSettings
        {
            Enterprises = [new OrganizationEnterprise { Guid = "e1", IncomingFrom = "2026-10-25", Participates = true }]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("2026-10-25T09:30:00+07:00", current.Enterprises.Single().IncomingFrom);
    }

    [Fact]
    public void Apply_сбрасывает_курсор_на_день_только_когда_день_сменился()
    {
        var current = new OrganizationVetisSettings
        {
            Enterprises = [new OrganizationEnterprise { Guid = "e1", IncomingFrom = "2026-10-25T09:30:00+07:00", Participates = true }]
        };
        var update = new OrganizationVetisSettings
        {
            Enterprises = [new OrganizationEnterprise { Guid = "e1", IncomingFrom = "2026-10-20", Participates = true }]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("2026-10-20", current.Enterprises.Single().IncomingFrom);
    }

    [Fact]
    public void Apply_не_откатывает_курсор_штампом_с_открытой_карточки()
    {
        var current = new OrganizationVetisSettings
        {
            Enterprises = [new OrganizationEnterprise { Guid = "e1", IncomingFrom = "2026-10-25T09:30:00+07:00", Participates = true }]
        };
        var update = new OrganizationVetisSettings
        {
            Enterprises = [new OrganizationEnterprise { Guid = "e1", IncomingFrom = "2026-10-20T08:00:00+07:00", Participates = true }]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("2026-10-25T09:30:00+07:00", current.Enterprises.Single().IncomingFrom);
    }
}
