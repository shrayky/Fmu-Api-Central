using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using CertificateSigning;
using CryptoPro.Security.Cryptography;
using CryptoPro.Security.Cryptography.Pkcs;
using CryptoPro.Security.Cryptography.X509Certificates;

namespace EtrnSaby.Tests;

public class OrganizationCertificateDirectoryTests
{
    [Fact]
    public void Найденный_сертификат_читается_после_закрытия_хранилища()
    {
        var matching = Certificate("CN=matching");
        var other = Certificate("CN=other");
        var serial = matching.GetSerialNumberString();
        var thumbprint = matching.GetCertHashString();
        var store = new FakeStore();
        store.Certificates.Add(other);
        store.Certificates.Add(matching);
        var directory = new OrganizationCertificateDirectory(() => store);

        var found = directory.Find(serial);

        Assert.True(found.IsSuccess, found.IsFailure ? found.Error : "");
        Assert.True(store.Closed);
        Assert.Equal(thumbprint, found.Value.Thumbprint);
        Assert.Equal(thumbprint, found.Value.Certificate.Thumbprint);
        Assert.NotEmpty(found.Value.Certificate.Export(X509ContentType.Cert));
    }

    [GostContainerFact]
    public void Ключ_копии_читается_после_закрытия_хранилища()
    {
        var (certificate, key) = GostCertificateFactory.Create();
        var serial = certificate.GetSerialNumberString();
        var thumbprint = certificate.GetCertHashString();
        var store = new FakeStore();
        store.Certificates.Add(certificate);
        var directory = new OrganizationCertificateDirectory(() => store);

        var found = directory.Find(serial);

        Assert.True(found.IsSuccess, found.IsFailure ? found.Error : "");
        Assert.True(store.Closed);
        Assert.Equal(thumbprint, found.Value.Thumbprint);
        Assert.True(found.Value.Certificate.HasPrivateKey);
        var privateKey = GostPrivateKey.Open(found.Value.Certificate, null);
        Assert.NotNull(privateKey);
        var cms = new CpSignedCms(new ContentInfo([1, 2, 3]), detached: true);
        var signer = new CpCmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, found.Value.Certificate, privateKey)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        };
        cms.ComputeSignature(signer, silent: true);
        Assert.NotEmpty(cms.Encode());
        key.Dispose();
    }

    [Fact]
    public void Тестовый_издатель_называется_в_ошибке()
    {
        var test = Certificate("CN=DO_NOT_TRUST");
        var store = new FakeStore();
        store.Certificates.Add(test);
        var directory = new OrganizationCertificateDirectory(() => store);

        var found = directory.Find(test.GetSerialNumberString());

        Assert.True(found.IsFailure);
        Assert.Contains("DO_NOT_TRUST", found.Error);
    }

    private static CpX509Certificate2 Certificate(string subject)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var created = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddDays(30));
        return new CpX509Certificate2(created.Export(X509ContentType.Cert));
    }

    private sealed class FakeStore : ICertificateStore
    {
        public List<CpX509Certificate2> Certificates { get; } = [];

        IReadOnlyList<CpX509Certificate2> ICertificateStore.Certificates => Certificates;

        public bool Closed { get; private set; }

        public void Dispose()
        {
            Closed = true;
            foreach (var certificate in Certificates)
                certificate.Dispose();
        }
    }
}

/// <summary>
/// Контейнер ГОСТ выпускается только при рабочем поставщике CryptoPro. Если выпустить его нельзя,
/// тест явно пропускается, а не проходит молча.
/// </summary>
public sealed class GostContainerFactAttribute : FactAttribute
{
    public GostContainerFactAttribute()
    {
        if (!GostCertificateFactory.CanCreate())
            Skip = "В этой среде нельзя выпустить контейнер ГОСТ";
    }
}

internal static class GostCertificateFactory
{
    public static bool CanCreate()
    {
        try
        {
            var (certificate, key) = Create();
            certificate.Dispose();
            key.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static (CpX509Certificate2 Certificate, IDisposable Key) Create()
    {
        var parameters = new CpCspParameters { Flags = CspProviderFlags.NoPrompt };
        var key = new Gost3410_2012_256CryptoServiceProvider(parameters);
        var request = new CpCertificateRequest("CN=gost-copy-test", key);
        var certificate = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddDays(30));
        return (certificate, key);
    }
}
