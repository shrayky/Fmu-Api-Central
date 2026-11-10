using System.Text;
using CryptoPro.Security.Cryptography;

namespace EtrnOfd;

public interface IOfdApiKeyHash
{
    string Hex(string apiKey);
}

/// <summary>
/// Документация ОФД требует ГОСТ Р 34.11-94, не ГОСТ 2012. Ключ — текст, кодировка в документе не названа.
/// </summary>
public sealed class OfdGostApiKeyHash : IOfdApiKeyHash
{
    public string Hex(string apiKey)
    {
        using var algorithm = new Gost3411CryptoServiceProvider();
        var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(apiKey ?? ""));
        return Convert.ToHexString(hash);
    }
}
