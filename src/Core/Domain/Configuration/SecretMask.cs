namespace Domain.Configuration;

public static class SecretMask
{
    public const string Placeholder = "***";

    public static string ForClient(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty : Placeholder;

    public static bool IsPlaceholder(string? value)
        => value == Placeholder;

    public static string Restore(string? incoming, string current)
        => IsPlaceholder(incoming) ? current : incoming ?? string.Empty;
}
