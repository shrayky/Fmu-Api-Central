using CouchDb.DatabaseScheme;

namespace CouchDb.Tests;

public class DatabaseDumpScopeTests
{
    /// <summary>
    /// В zip данных только справочники; статистика и ГИС МТ на новом сервере подгрузятся сами.
    /// </summary>
    [Fact]
    public void ExcludedFromExport_оставляет_только_переносимые_справочники()
    {
        var exported = DatabaseNames.All()
            .Except(DatabaseNames.ExcludedFromExport(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string[] expected =
        [
            DatabaseNames.AlertTemplates,
            DatabaseNames.Instance,
            DatabaseNames.InstanceGroup,
            DatabaseNames.Organization,
            DatabaseNames.SettingsSchema
        ];
        Array.Sort(expected, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expected, exported);
    }
}
