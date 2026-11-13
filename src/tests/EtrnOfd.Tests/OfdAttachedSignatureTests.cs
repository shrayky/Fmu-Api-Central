using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CertificateSigning;
using CryptoPro.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;

namespace EtrnOfd.Tests;

public class OfdAttachedSignatureTests
{
    [Fact]
    public async Task Подпись_освобождает_сертификат()
    {
        var certificate = KeylessCertificate();
        var signer = new OfdAttachedSignature(new FixedCertificate(certificate));

        var result = await signer.SignAsync("serial", "pwd", [1, 2, 3], CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Не удалось получить закрытый ключ ГОСТ из сертификата", result.Error);
        var disposed = Assert.Throws<CryptographicException>(() => certificate.Thumbprint);
        Assert.Contains("invalid handle", disposed.Message);
    }

    private static CpX509Certificate2 KeylessCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=ofd-sign", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
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
}
