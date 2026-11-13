using System.Security.Cryptography.X509Certificates;
using CryptoPro.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;

namespace CertificateSigning;

public sealed class OrganizationCertificate
{
    public CpX509Certificate2 Certificate { get; init; } = null!;

    public string Thumbprint { get; init; } = "";
}

public interface IOrganizationCertificateDirectory
{
    Result<OrganizationCertificate> Find(string? serialNumber);
}

internal interface ICertificateStore : IDisposable
{
    IReadOnlyList<CpX509Certificate2> Certificates { get; }
}

internal sealed class CpCertificateStore : ICertificateStore
{
    private readonly CpX509Store _store;

    public CpCertificateStore()
    {
        _store = new CpX509Store(StoreName.My, StoreLocation.CurrentUser);
        _store.Open(OpenFlags.ReadOnly);
    }

    public IReadOnlyList<CpX509Certificate2> Certificates => _store.Certificates.Cast<CpX509Certificate2>().ToList();

    public void Dispose() => _store.Close();
}

public sealed class OrganizationCertificateDirectory : IOrganizationCertificateDirectory
{
    private readonly Func<ICertificateStore> _open;

    public OrganizationCertificateDirectory()
        : this(() => new CpCertificateStore())
    {
    }

    internal OrganizationCertificateDirectory(Func<ICertificateStore> open)
    {
        _open = open;
    }

    public Result<OrganizationCertificate> Find(string? serialNumber)
    {
        var serial = (serialNumber ?? "").Replace(" ", "").Trim();
        if (serial.Length == 0)
            return Result.Failure<OrganizationCertificate>("Не выбран сертификат организации");

        ICertificateStore store;
        try
        {
            store = _open();
        }
        catch (Exception ex)
        {
            return Result.Failure<OrganizationCertificate>($"Хранилище сертификатов недоступно: {ex.Message}");
        }

        IReadOnlyList<CpX509Certificate2> certificates;
        try
        {
            certificates = store.Certificates;
        }
        catch (Exception ex)
        {
            store.Dispose();
            return Result.Failure<OrganizationCertificate>($"Хранилище сертификатов недоступно: {ex.Message}");
        }

        try
        {
            var testIssuerOnly = false;
            foreach (var certificate in certificates)
            {
                if (!string.Equals(
                        certificate.GetSerialNumberString().Replace(" ", ""),
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                if (certificate.Issuer.Contains("DO_NOT_TRUST"))
                {
                    testIssuerOnly = true;
                    continue;
                }

                if (certificate.NotAfter <= DateTime.Now)
                    continue;

                // Закрытие хранилища освобождает сертификат. Копия живёт дальше и хранит контекст закрытого ключа.
                return Result.Success(new OrganizationCertificate
                {
                    Certificate = new CpX509Certificate2(certificate),
                    Thumbprint = certificate.GetCertHashString()
                });
            }

            return testIssuerOnly
                ? Result.Failure<OrganizationCertificate>("Сертификат организации тестовый: издатель DO_NOT_TRUST")
                : Result.Failure<OrganizationCertificate>("Сертификат организации не найден в хранилище");
        }
        finally
        {
            // Освобождаются все сертификаты хранилища, включая найденный: наружу ушла копия.
            foreach (var certificate in certificates)
                certificate.Dispose();
            store.Dispose();
        }
    }
}
