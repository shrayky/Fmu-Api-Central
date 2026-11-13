using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using CertificateSigning;
using CryptoPro.Security.Cryptography.Pkcs;
using CSharpFunctionalExtensions;

namespace EtrnOfd;

public interface IOfdAttachedSignature
{
    Task<Result<string>> SignAsync(
        string certificateNumber,
        string? password,
        byte[] content,
        CancellationToken cancellationToken);
}

public sealed class OfdAttachedSignature : IOfdAttachedSignature
{
    private readonly IOrganizationCertificateDirectory _certificates;

    public OfdAttachedSignature(IOrganizationCertificateDirectory certificates)
    {
        _certificates = certificates;
    }

    public Task<Result<string>> SignAsync(
        string certificateNumber,
        string? password,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var found = _certificates.Find(certificateNumber);
        if (found.IsFailure)
            return Task.FromResult(Result.Failure<string>(found.Error));

        var certificate = found.Value.Certificate;
        try
        {
            var privateKey = GostPrivateKey.Open(certificate, password);
            if (privateKey == null)
                return Task.FromResult(Result.Failure<string>("Не удалось получить закрытый ключ ГОСТ из сертификата"));

            var cms = new CpSignedCms(new ContentInfo(content), detached: false);
            var signer = new CpCmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate, privateKey)
            {
                IncludeOption = X509IncludeOption.EndCertOnly
            };
            cms.ComputeSignature(signer, silent: true);
            return Task.FromResult(Result.Success(Convert.ToBase64String(cms.Encode())));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure<string>(ex.Message));
        }
        finally
        {
            certificate.Dispose();
        }
    }
}
