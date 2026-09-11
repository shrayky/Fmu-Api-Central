using Application.Instance.Services;

namespace Application.Tests;

public class CheckerTemplateFilesTests
{
    [Fact]
    public void Folder_называется_instance_template()
    {
        Assert.Equal("instance-template", Path.GetFileName(new CheckerTemplateLocator().Folder));
    }

    [Fact]
    public void IsCheckerExe_принимает_check_и_checker()
    {
        Assert.True(CheckerTemplateFiles.IsCheckerExe("fmu-api-check.exe"));
        Assert.True(CheckerTemplateFiles.IsCheckerExe("fmu-api-checker.exe"));
        Assert.False(CheckerTemplateFiles.IsCheckerExe("fmu-api.exe"));
    }

    [Fact]
    public void FindExe_находит_check_в_каталоге()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "fmu-api-check.exe"), "x"u8.ToArray());

            var found = CheckerTemplateFiles.FindExe(dir.FullName);

            Assert.Equal(Path.Combine(dir.FullName, "fmu-api-check.exe"), found);
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
