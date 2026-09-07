using Authentication;

namespace Application.Tests;

public class JwtKeyFileAccessTests
{
    /// <summary>
    /// Файл ключа не наследует ACL каталога ProgramData.
    /// </summary>
    [Fact]
    public void Restrict_отключает_наследование()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var path = Path.Combine(Path.GetTempPath(), $"jwt-acl-{Guid.NewGuid():N}.key");
        File.WriteAllText(path, "test-key");
        try
        {
            JwtKeyFileAccess.RestrictToCurrentUserAndSystem(path);
            var security = new FileInfo(path).GetAccessControl();
            Assert.True(security.AreAccessRulesProtected);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
