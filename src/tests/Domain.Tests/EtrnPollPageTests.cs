using Domain.Etrn;

namespace Domain.Tests;

public class EtrnPollPageTests
{
    [Fact]
    public void Непрочитанная_середина_оставляет_пару_на_этом_событии()
    {
        var page = new[]
        {
            Event("e1", "d1", EtrnPollEvent.AcceptanceStage),
            Event("e2", "d2", EtrnPollEvent.AcceptanceStage),
            Event("e3", "d3", EtrnPollEvent.AcceptanceStage),
            Event("e4", "d4", EtrnPollEvent.AcceptanceStage)
        };

        var next = EtrnPollPage.Advance(new EtrnPollPosition("e1", "d1"), page, processed: 2);

        Assert.Equal(new EtrnPollPosition("e3", "d3"), next);
    }

    [Fact]
    public void Первое_непрочитанное_событие_становится_парой()
    {
        var page = new[]
        {
            Event("e1", "d1", EtrnPollEvent.AcceptanceStage),
            Event("e2", "d2", EtrnPollEvent.AcceptanceStage)
        };

        var next = EtrnPollPage.Advance(EtrnPollPosition.Empty, page, processed: 0);

        Assert.Equal(new EtrnPollPosition("e1", "d1"), next);
    }

    [Fact]
    public void События_мимо_приёмки_двигают_пару()
    {
        var page = new[]
        {
            Event("e1", "d1", EtrnPollEvent.AcceptanceStage),
            Event("e2", "d2", "Подписание"),
            Event("e3", "d3", "")
        };

        var next = EtrnPollPage.Advance(EtrnPollPosition.Empty, page, processed: 3);

        Assert.Equal(new EtrnPollPosition("e3", "d3"), next);
    }

    [Fact]
    public void Пустая_страница_пару_не_двигает()
    {
        var current = new EtrnPollPosition("e1", "d1");

        Assert.Equal(current, EtrnPollPage.Advance(current, [], processed: 0));
    }

    [Fact]
    public void Полностью_прочитанная_страница_встаёт_на_последнее_событие()
    {
        var page = new[]
        {
            Event("e1", "d1", EtrnPollEvent.AcceptanceStage),
            Event("e2", "d2", EtrnPollEvent.AcceptanceStage)
        };

        var next = EtrnPollPage.Advance(EtrnPollPosition.Empty, page, processed: 2);

        Assert.Equal(new EtrnPollPosition("e2", "d2"), next);
    }

    [Fact]
    public void Этап_приёмки_распознаётся_без_учёта_регистра_и_пробелов()
    {
        Assert.True(Event("e1", "d1", "  приемка ГРУЗА ").IsAcceptance);
        Assert.False(Event("e1", "d1", "Приемка").IsAcceptance);
    }

    private static EtrnPollEvent Event(string eventId, string documentId, string stage) =>
        new(eventId, documentId, stage);
}
