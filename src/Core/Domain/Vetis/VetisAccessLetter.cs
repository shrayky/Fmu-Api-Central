using System.Text.RegularExpressions;

namespace Domain.Vetis;

public sealed record VetisAccessLetter(string Login, string Password, string ApiKey, string IssuerId)
{
    private static readonly string[] Keys = ["Login", "Password", "APIKey", "IssuerID"];

    private static readonly Regex GuidPattern = new(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static VetisAccessLetter Parse(string? text)
    {
        var source = text ?? "";
        return new VetisAccessLetter(
            Read(source, "Login"),
            Read(source, "Password"),
            Read(source, "APIKey"),
            Issuer(Read(source, "IssuerID")));
    }

    private static string Issuer(string raw)
    {
        var match = GuidPattern.Match(raw);
        return match.Success ? match.Value : "";
    }

    private static string Read(string text, string key)
    {
        var match = Regex.Match(
            text,
            $"^{Regex.Escape(key)}:\\s*(.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        if (!match.Success)
            return "";

        var inline = match.Groups[1].Value.Trim();
        if (inline.Length > 0)
            return inline;

        var rest = text[(match.Index + match.Length)..];
        foreach (var line in rest.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || IsKey(trimmed))
                continue;

            return trimmed;
        }

        return "";
    }

    private static bool IsKey(string line)
    {
        foreach (var key in Keys)
        {
            if (line.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
