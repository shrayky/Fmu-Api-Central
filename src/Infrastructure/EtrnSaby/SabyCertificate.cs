using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using CryptoPro.Security.Cryptography;
using CryptoPro.Security.Cryptography.Pkcs;
using CryptoPro.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Microsoft.Extensions.Logging;

namespace EtrnSaby;

public sealed class SabyCertificateInfo
{
    public CpX509Certificate2 Certificate { get; init; } = null!;

    public string Thumbprint { get; init; } = "";
}

public interface ISabyCertificateDirectory
{
    Result<SabyCertificateInfo> Find(string? serialNumber);
}

public sealed class SabyCertificateDirectory : ISabyCertificateDirectory
{
    public Result<SabyCertificateInfo> Find(string? serialNumber)
    {
        var serial = (serialNumber ?? "").Replace(" ", "").Trim();
        if (serial.Length == 0)
            return Result.Failure<SabyCertificateInfo>("Не выбран сертификат организации");

        CpX509Store store;
        try
        {
            store = new CpX509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
        }
        catch (Exception ex)
        {
            return Result.Failure<SabyCertificateInfo>($"Хранилище сертификатов недоступно: {ex.Message}");
        }

        try
        {
            foreach (var certificate in store.Certificates)
            {
                if (certificate.NotAfter <= DateTime.Now || certificate.Issuer.Contains("DO_NOT_TRUST"))
                    continue;

                if (!string.Equals(
                        certificate.GetSerialNumberString().Replace(" ", ""),
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                return Result.Success(new SabyCertificateInfo
                {
                    Certificate = certificate,
                    Thumbprint = certificate.GetCertHashString()
                });
            }
        }
        finally
        {
            store.Close();
        }

        return Result.Failure<SabyCertificateInfo>("Сертификат организации не найден в хранилище");
    }
}

public interface ISabyAuthenticator
{
    Task<Result<string>> LoginAsync(EtrnOperatorContext context, CancellationToken cancellationToken);
}

public sealed class SabyAuthenticator : ISabyAuthenticator
{
    private readonly SabyJsonRpc _rpc;
    private readonly ISabyCertificateDirectory _certificates;
    private readonly ILogger<SabyAuthenticator> _logger;

    public SabyAuthenticator(
        SabyJsonRpc rpc,
        ISabyCertificateDirectory certificates,
        ILogger<SabyAuthenticator> logger)
    {
        _rpc = rpc;
        _certificates = certificates;
        _logger = logger;
    }

    public async Task<Result<string>> LoginAsync(EtrnOperatorContext context, CancellationToken cancellationToken)
    {
        var found = _certificates.Find(context.CertificateNumber);
        if (found.IsFailure)
            return Result.Failure<string>(found.Error);

        var certificate = found.Value.Certificate;
        var der = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
        var answer = await _rpc.SendAsync(
            SabyJsonRpc.AuthUrl,
            EtrnMethods.AuthByCertificate,
            new Dictionary<string, object> { ["Сертификат"] = der },
            sessionId: null,
            SabyUserAgent.Value,
            cancellationToken);

        if (!answer.Ok)
            return Result.Failure<string>(answer.Error);

        try
        {
            var envelope = Convert.FromBase64String((answer.ResultText ?? "").Trim());
            var cms = new CpEnvelopedCms();
            cms.Decode(envelope);
            var recipients = new CpX509Certificate2Collection { certificate };
            cms.Decrypt(recipients);
            var text = Encoding.UTF8.GetString(cms.ContentInfo.Content).Trim();
            var session = ReadSessionId(text);
            return session.Length == 0
                ? Result.Failure<string>("Saby вернула пустой идентификатор сессии")
                : Result.Success(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось расшифровать сессию Saby");
            return Result.Failure<string>($"Не удалось расшифровать сессию Saby: {ex.Message}");
        }
    }

    private static string ReadSessionId(string text)
    {
        var value = text.Trim().Trim('"');
        if (!value.StartsWith('{'))
            return value;

        using var document = JsonDocument.Parse(value);
        foreach (var name in new[] { "sessionId", "SessionId", "ИдентификаторСессии", "session" })
        {
            if (document.RootElement.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.String)
                return property.GetString() ?? "";
        }

        return "";
    }
}

public static class SabyUserAgent
{
    public const string Value = "FmuApiCentral/2.3";
}

public sealed class EtrnSignatureService : IEtrnSignaturePort
{
    public const string HttpClientName = "EtrnSignatureFile";

    private readonly ISabyCertificateDirectory _certificates;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EtrnSignatureService> _logger;

    public EtrnSignatureService(
        ISabyCertificateDirectory certificates,
        IHttpClientFactory httpClientFactory,
        ILogger<EtrnSignatureService> logger)
    {
        _certificates = certificates;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Result<string>> SignAsync(EtrnSignatureRequest request, CancellationToken cancellationToken)
    {
        var found = _certificates.Find(request.CertificateNumber);
        if (found.IsFailure)
            return Result.Failure<string>(found.Error);

        byte[] content;
        try
        {
            content = await ContentAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Не удалось получить данные для подписи: {ex.Message}");
        }

        try
        {
            var certificate = found.Value.Certificate;
            var privateKey = PrivateKey(certificate, request.CertificatePassword);
            if (privateKey == null)
                return Result.Failure<string>("Не удалось получить закрытый ключ ГОСТ из сертификата");

            var cms = new CpSignedCms(new ContentInfo(content), detached: true);
            var signer = new CpCmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate, privateKey)
            {
                IncludeOption = X509IncludeOption.EndCertOnly
            };
            cms.ComputeSignature(signer, silent: true);
            return Result.Success(Convert.ToBase64String(cms.Encode()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подписи титула ЭТрН");
            return Result.Failure<string>(ex.Message);
        }
    }

    private async Task<byte[]> ContentAsync(EtrnSignatureRequest request, CancellationToken cancellationToken)
    {
        var hash = (request.Hash ?? "").Trim();
        if (hash.Length > 0)
            return Convert.FromBase64String(hash);

        var url = (request.FileUrl ?? "").Trim();
        if (url.Length == 0)
            throw new InvalidOperationException("Saby не вернула хеш или ссылку для подписи.");

        using var http = _httpClientFactory.CreateClient(HttpClientName);
        return await http.GetByteArrayAsync(url, cancellationToken);
    }

    private static AsymmetricAlgorithm? PrivateKey(CpX509Certificate2 certificate, string? password)
    {
        var privateKey = certificate.GetGost3410_2012_256PrivateKey()
                         ?? certificate.GetGost3410_2012_512PrivateKey() as Gost3410Algorithm
                         ?? certificate.GetGost3410PrivateKey();
        if (privateKey == null || string.IsNullOrEmpty(password))
            return privateKey;

        var secure = new SecureString();
        foreach (var character in password)
            secure.AppendChar(character);
        secure.MakeReadOnly();

        switch (privateKey)
        {
            case Gost3410_2012_256CryptoServiceProvider csp256:
                csp256.SetContainerPassword(secure);
                break;
            case Gost3410_2012_512CryptoServiceProvider csp512:
                csp512.SetContainerPassword(secure);
                break;
            case Gost3410CryptoServiceProvider csp3410:
                csp3410.SetContainerPassword(secure);
                break;
        }

        return privateKey;
    }
}
