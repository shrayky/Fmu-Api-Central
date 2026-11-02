using Application.InstanceGroup.Services;

namespace Application.Tests;

public class CheckerSettingsFileTests
{
    [Fact]
    public void Объект_подходит()
    {
        Assert.True(CheckerSettingsFile.IsJsonObject("{\"a\":1}"u8.ToArray()));
    }

    [Fact]
    public void Пустой_массив_и_битый_json_не_подходят()
    {
        Assert.False(CheckerSettingsFile.IsJsonObject([]));
        Assert.False(CheckerSettingsFile.IsJsonObject("[]"u8.ToArray()));
        Assert.False(CheckerSettingsFile.IsJsonObject("{"u8.ToArray()));
    }
}
