using System.Text.Json;

namespace EtrnSaby.Tests;

public class SabyAuthenticatorTests
{
    [Fact]
    public void Вход_передаёт_сертификат_объектом()
    {
        var json = JsonSerializer.Serialize(SabyAuthenticator.CertificateParameters("QUJD"));

        using var document = JsonDocument.Parse(json);
        var certificate = document.RootElement.GetProperty("Сертификат");
        Assert.Equal(JsonValueKind.Object, certificate.ValueKind);
        Assert.Equal("QUJD", certificate.GetProperty("ДвоичныеДанные").GetString());
    }
}
