using Domain.Logs;

namespace Domain.Tests;

public class LogFileNameOrderTests
{
    /// <summary>
    /// Combo логов должен показывать дни от новых к старым, а не порядок файловой системы.
    /// </summary>
    [Fact]
    public void Descending_ставит_новые_даты_выше_старых()
    {
        var ordered = LogFileNameOrder.Descending(
            ["20260723", "20260827", "20260701", "20260801"],
            suffix => suffix);

        Assert.Equal(["20260827", "20260801", "20260723", "20260701"], ordered);
    }

    /// <summary>
    /// При превышении размера Serilog добавляет nnn — такой файл новее дневного без суффикса.
    /// </summary>
    [Fact]
    public void Descending_файл_с_номером_ролла_новее_дневного()
    {
        var ordered = LogFileNameOrder.Descending(
            ["20260828", "20260828001"],
            suffix => suffix);

        Assert.Equal(["20260828001", "20260828"], ordered);
    }
}
