using Domain.Vetis;

namespace Domain.Tests;

public class VetisJobStatusTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 2, 10, 17, 0, TimeSpan.FromHours(7));

    private static readonly DateTimeOffset Next = new(2026, 11, 2, 10, 32, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Ожидание_показывает_следующий_запуск_и_счётчик_включая_ноль()
    {
        Assert.Equal(
            "Ожидает до 02.11.2026 10:32. За прошлый запуск погашено документов 4.",
            VetisJobStatusText.Format(VetisJobPhase.Waiting, "погашено документов", 4, At, Next, null));
        Assert.Equal(
            "Ожидает до 02.11.2026 10:32. За прошлый запуск удалено заявок 0.",
            VetisJobStatusText.Format(VetisJobPhase.Waiting, "удалено заявок", 0, At, Next, null));
    }

    [Fact]
    public void Остановленное_задание_без_прохода_не_выдумывает_счётчик()
    {
        Assert.Equal(
            "Остановлено: Ветис выключен.",
            VetisJobStatusText.Format(VetisJobPhase.Stopped, "погашено документов", null, At, Next, null));
        Assert.Equal(
            "Остановлено: Ветис выключен. За прошлый запуск погашено документов 2.",
            VetisJobStatusText.Format(VetisJobPhase.Stopped, "погашено документов", 2, At, Next, null));
    }

    [Fact]
    public void Запуск_выполнение_подготовка_и_ошибка_называют_своё_состояние()
    {
        Assert.Equal(
            "Запускается. Первый проход 02.11.2026 10:32.",
            VetisJobStatusText.Format(VetisJobPhase.Starting, "", null, At, Next, null));
        Assert.Equal(
            "Выполняется с 02.11.2026 10:17.",
            VetisJobStatusText.Format(VetisJobPhase.Running, "", null, At, null, null));
        Assert.Equal(
            "Готовит журнал, загрузка ВСД ещё не начата.",
            VetisJobStatusText.Format(VetisJobPhase.Preparing, "", null, At, null, null));
        Assert.Equal(
            "Ошибка: нет связи. Повтор 02.11.2026 10:32.",
            VetisJobStatusText.Format(VetisJobPhase.Failed, "погашено документов", null, At, Next, " нет связи "));
        Assert.Equal(
            "Ошибка: нет связи. Повтор 02.11.2026 10:32. За этот запуск погашено документов 2.",
            VetisJobStatusText.Format(VetisJobPhase.Failed, "погашено документов", 2, At, Next, " нет связи "));
    }
}
