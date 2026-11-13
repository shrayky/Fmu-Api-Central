using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using CertificateSigning;
using CryptoPro.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtrnSaby.Tests;

public class SabyAuthenticatorTests
{
    [Fact]
    public async Task Вход_задаёт_пароль_контейнера_и_освобождает_сертификат()
    {
        var certificate = KeylessCertificate();
        string? password = null;
        var handler = new SabyHandler
        {
            Respond = (_, _) => SabyHandler.Result("\"YQ==\"")
        };
        var authenticator = new SabyAuthenticator(
            new SabyJsonRpc(new SabyHttpClientFactory(handler), NullLogger<SabyJsonRpc>.Instance),
            new FixedCertificate(certificate),
            NullLogger<SabyAuthenticator>.Instance,
            (_, given) =>
            {
                password = given;
                return null;
            });

        var result = await authenticator.LoginAsync(new EtrnOperatorContext
        {
            CertificateNumber = "serial",
            CertificatePassword = "pwd"
        }, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Не удалось получить закрытый ключ ГОСТ из сертификата", result.Error);
        Assert.Equal("pwd", password);
        var disposed = Assert.Throws<CryptographicException>(() => certificate.Thumbprint);
        Assert.Contains("invalid handle", disposed.Message);
    }

    [Fact]
    public async Task Подпись_освобождает_сертификат()
    {
        var certificate = KeylessCertificate();
        var service = new EtrnSignatureService(
            new FixedCertificate(certificate),
            new SabyHttpClientFactory(new SabyHandler()),
            NullLogger<EtrnSignatureService>.Instance);

        var result = await service.SignAsync(new EtrnSignatureRequest
        {
            CertificateNumber = "serial",
            CertificatePassword = "pwd",
            Content = [1, 2, 3]
        }, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Не удалось получить закрытый ключ ГОСТ из сертификата", result.Error);
        var disposed = Assert.Throws<CryptographicException>(() => certificate.Thumbprint);
        Assert.Contains("invalid handle", disposed.Message);
    }

    private static CpX509Certificate2 KeylessCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=login", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
        return new CpX509Certificate2(created.Export(X509ContentType.Cert));
    }

    private sealed class FixedCertificate(CpX509Certificate2 certificate) : IOrganizationCertificateDirectory
    {
        public Result<OrganizationCertificate> Find(string? serialNumber) =>
            Result.Success(new OrganizationCertificate
            {
                Certificate = certificate,
                Thumbprint = certificate.Thumbprint
            });
    }

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
