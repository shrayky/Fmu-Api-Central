using Application.SettingsSchema;
using Domain.Dto.FmuApiExchangeData;
using Domain.Entitys.SettingsSchema;

namespace Application.Tests;

public class GroupSettingsApplyTests
{
    [Fact]
    public void Apply_копирует_HaveExpireDate()
    {
        var source = new FmuApiSetting { Version = 1 };
        var mappings = new List<GisMtProductMapping>
        {
            new()
            {
                AtolCode = 13,
                TrueApiGroupId = 8,
                Name = "Молочная продукция",
                HaveExpireDate = true
            }
        };

        var result = GroupSettingsApply.Apply(source, new HttpRequestTimeouts(), mappings, []);

        Assert.True(result.GisMtProductMappings.Single().HaveExpireDate);
    }
}
