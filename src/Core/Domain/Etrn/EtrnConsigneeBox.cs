namespace Domain.Etrn;

/// <summary>
/// Второй идентификатор ON_TRNACLGROT — ящик грузополучателя.
/// Префикс 2BE встречается и у других сторон, поэтому сверяется идентификатор целиком.
/// </summary>
public static class EtrnConsigneeBox
{
    public static string? Of(string? fileName)
    {
        var name = (fileName ?? "").Trim();
        if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        var parts = name.Split('_');
        if (parts.Length < 8
            || !string.Equals(parts[0], "ON", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[1], "TRNACLGROT", StringComparison.OrdinalIgnoreCase)
            || parts[5] is not ("0" or "1")
            || parts[6].Length != 8)
            return null;

        var box = parts[3].Trim();
        return box.Length == 0 ? null : box;
    }

    public static bool IsForeign(string? fileName, string? ourBox)
    {
        var ours = (ourBox ?? "").Trim();
        if (ours.Length == 0)
            return false;

        var consignee = Of(fileName);
        return consignee != null && !string.Equals(consignee, ours, StringComparison.OrdinalIgnoreCase);
    }
}
