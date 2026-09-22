using System.Text.Json;
using Domain.Entitys.SettingsSchema;

namespace Domain.Tests;

public class GisMtProductMappingTests
{
    [Fact]
    public void Serialize_пишет_haveExpireDate()
    {
        var json = JsonSerializer.Serialize(new GisMtProductMapping { HaveExpireDate = true });

        Assert.Contains("\"haveExpireDate\":true", json);
    }

    [Fact]
    public void CopyDefaults_копирует_HaveExpireDate()
    {
        var original = AtolToTrueApiGroupMap.Defaults[13];
        var previous = original.HaveExpireDate;
        original.HaveExpireDate = true;

        try
        {
            var copy = AtolToTrueApiGroupMap.CopyDefaults()
                .Single(item => item.AtolCode == 13);

            Assert.True(copy.HaveExpireDate);
        }
        finally
        {
            original.HaveExpireDate = previous;
        }
    }
}
