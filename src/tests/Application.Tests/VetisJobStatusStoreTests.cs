using Application.Vetis;
using Domain.Vetis;

namespace Application.Tests;

public class VetisJobStatusStoreTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 2, 10, 17, 0, TimeSpan.FromHours(7));

    private static readonly DateTimeOffset Next = new(2026, 11, 2, 10, 32, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Список_идёт_по_порядку_и_помнит_счётчик_после_остановки()
    {
        var store = new VetisJobStatusStore();
        store.Report(Update("autoQuench", "Автогашение", "погашено документов", 2, VetisJobPhase.Waiting, 4));
        store.Report(Update("incoming", "Получение ВСД", "записано документов", 1, VetisJobPhase.Preparing, null));

        var lines = store.Current();

        Assert.Equal(["incoming", "autoQuench"], lines.Select(item => item.Id));
        Assert.Equal("Готовит журнал, загрузка ВСД ещё не начата.", lines[0].Text);
        Assert.Equal("Ожидает до 02.11.2026 10:32. За прошлый запуск погашено документов 4.", lines[1].Text);

        store.Report(Update("autoQuench", "Автогашение", "погашено документов", 2, VetisJobPhase.Stopped, null));
        store.Report(Update("cleanup", "Очистка заявок", "удалено заявок", 3, VetisJobPhase.Waiting, 0));

        lines = store.Current();

        Assert.Equal(["incoming", "autoQuench", "cleanup"], lines.Select(item => item.Id));
        Assert.Equal("Остановлено: Ветис выключен. За прошлый запуск погашено документов 4.", lines[1].Text);
        Assert.Equal("Ожидает до 02.11.2026 10:32. За прошлый запуск удалено заявок 0.", lines[2].Text);
    }

    private static VetisJobUpdate Update(string id, string title, string countLabel, int order, VetisJobPhase phase, int? count) =>
        new(id, title, countLabel, order, phase, At, Next, count, null);
}
