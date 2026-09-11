using Domain.Entitys.CrptViolations;
using Domain.Entitys.Organization;

namespace Domain.Tests;

public class CrptViolationsLoadRulesTests
{
    /// <summary>
    /// Без включённой интеграции организацию пропускаем.
    /// </summary>
    [Fact]
    public void CanLoad_без_enable_ложно()
    {
        var organization = Org(enable: false, signature: "thumb");

        Assert.False(CrptViolationsLoadRules.CanLoad(organization));
    }

    /// <summary>
    /// Без выбранного сертификата ЭЦП организацию пропускаем.
    /// </summary>
    [Fact]
    public void CanLoad_без_эцп_ложно()
    {
        var organization = Org(enable: true, signature: "");

        Assert.False(CrptViolationsLoadRules.CanLoad(organization));
    }

    /// <summary>
    /// Интеграция включена и ЭЦП выбран — организацию берём.
    /// </summary>
    [Fact]
    public void CanLoad_с_enable_и_эцп_истинно()
    {
        var organization = Org(enable: true, signature: "thumb");

        Assert.True(CrptViolationsLoadRules.CanLoad(organization));
    }

    /// <summary>
    /// Начало месяца — всегда 1-е число той же даты.
    /// </summary>
    [Fact]
    public void MonthStart_первое_число()
    {
        Assert.Equal(new DateOnly(2026, 9, 1), CrptViolationsLoadRules.MonthStart(new DateOnly(2026, 9, 11)));
    }

    /// <summary>
    /// Без сохранённых дней предыдущей базы нет — сегодняшний снимок целиком станет дельтой.
    /// </summary>
    [Fact]
    public void PreviousDate_без_данных_пусто()
    {
        Assert.Null(CrptViolationsLoadRules.PreviousDate([], new DateOnly(2026, 9, 11)));
    }

    /// <summary>
    /// Сегодняшний документ не берём как базу — нужен последний день строго раньше.
    /// </summary>
    [Fact]
    public void PreviousDate_сегодня_не_считается()
    {
        var existing = new DateOnly[] { new(2026, 9, 11) };

        Assert.Null(CrptViolationsLoadRules.PreviousDate(existing, new DateOnly(2026, 9, 11)));
    }

    /// <summary>
    /// База для прироста — максимальная сохранённая дата месяца до сегодня.
    /// </summary>
    [Fact]
    public void PreviousDate_последний_день_до_сегодня()
    {
        var existing = new DateOnly[]
        {
            new(2026, 9, 1),
            new(2026, 9, 3),
            new(2026, 9, 11)
        };

        Assert.Equal(new DateOnly(2026, 9, 3), CrptViolationsLoadRules.PreviousDate(existing, new DateOnly(2026, 9, 11)));
    }

    private static OrganizationEntity Org(bool enable, string signature) => new()
    {
        Inn = "246412218294",
        TrueApiIntegrationSettings = new TrueApiIntegrationSettings
        {
            Enable = enable,
            DigitalSignature = signature
        }
    };
}
