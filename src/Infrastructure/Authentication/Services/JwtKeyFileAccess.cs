using System.Security.AccessControl;
using System.Security.Principal;

namespace Authentication;

public static class JwtKeyFileAccess
{
    /// <summary>
    /// jwt.key лежит в ProgramData — без ACL его читает любой локальный пользователь.
    /// </summary>
    public static void RestrictToCurrentUserAndSystem(string path)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(path))
            return;

        var file = new FileInfo(path);
        var security = file.GetAccessControl();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(
            WindowsIdentity.GetCurrent().User ?? new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        file.SetAccessControl(security);
    }
}
