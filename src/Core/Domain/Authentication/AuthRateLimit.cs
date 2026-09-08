using System.Net;

namespace Domain.Authentication;

public static class AuthRateLimit
{
    public const string Login = "auth-login";
    public const string Refresh = "auth-refresh";
    public const string ChangePassword = "auth-change-password";

    public const int LoginPerMinute = 10;
    public const int RefreshPerMinute = 20;
    public const int ChangePasswordPerMinute = 5;

    public static string PartitionKey(IPAddress? ip)
        => ip?.ToString() ?? "unknown";
}
