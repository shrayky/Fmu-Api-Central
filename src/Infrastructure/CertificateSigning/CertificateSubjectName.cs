using System.Text;
using CSharpFunctionalExtensions;

namespace CertificateSigning;

public readonly record struct CertificatePersonName(string Surname, string Name, string Patronymic);

/// <summary>
/// В субъекте квалифицированного сертификата фамилия — SN, имя и отчество — G.
/// Запятая внутри кавычек не разделяет поля.
/// </summary>
public static class CertificateSubjectName
{
    public const string Missing = "В сертификате нет фамилии и имени подписанта";

    public static Result<CertificatePersonName> Read(string? subject)
    {
        var surname = "";
        var given = "";
        foreach (var part in Split(subject ?? ""))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = part[..eq].Trim();
            var value = Unquote(part[(eq + 1)..].Trim());
            if (value.Length == 0)
                continue;

            if (surname.Length == 0 && IsSurname(key))
                surname = value;
            else if (given.Length == 0 && IsGiven(key))
                given = value;
        }

        var words = given.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (surname.Length == 0 || words.Length == 0)
            return Result.Failure<CertificatePersonName>(Missing);

        return Result.Success(new CertificatePersonName(
            surname,
            words[0],
            words.Length == 1 ? "" : string.Join(' ', words[1..])));
    }

    private static bool IsSurname(string key) =>
        key.Equals("SN", StringComparison.OrdinalIgnoreCase)
        || key.Equals("Surname", StringComparison.OrdinalIgnoreCase)
        || key.Equals("2.5.4.4", StringComparison.OrdinalIgnoreCase)
        || key.Equals("OID.2.5.4.4", StringComparison.OrdinalIgnoreCase);

    private static bool IsGiven(string key) =>
        key.Equals("G", StringComparison.OrdinalIgnoreCase)
        || key.Equals("GN", StringComparison.OrdinalIgnoreCase)
        || key.Equals("GivenName", StringComparison.OrdinalIgnoreCase)
        || key.Equals("2.5.4.42", StringComparison.OrdinalIgnoreCase)
        || key.Equals("OID.2.5.4.42", StringComparison.OrdinalIgnoreCase);

    private static List<string> Split(string subject)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < subject.Length; i++)
        {
            var ch = subject[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < subject.Length && subject[i + 1] == '"')
                {
                    current.Append('"');
                    current.Append('"');
                    i++;
                    continue;
                }

                quoted = !quoted;
                current.Append(ch);
                continue;
            }

            if (ch == ',' && !quoted)
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            parts.Add(current.ToString());
        return parts;
    }

    private static string Unquote(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
            return value;

        return value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
    }
}
