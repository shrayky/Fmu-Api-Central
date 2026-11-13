using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using CertificateSigning;
using CryptoPro.Security.Cryptography;
using CryptoPro.Security.Cryptography.Pkcs;
using CryptoPro.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;
using Domain.Etrn;
using Microsoft.Extensions.Logging;

namespace EtrnSaby;

public interface ISabyAuthenticator
{
    Task<Result<string>> LoginAsync(EtrnOperatorContext context, CancellationToken cancellationToken);
}

public sealed class SabyAuthenticator : ISabyAuthenticator
{
    private readonly SabyJsonRpc _rpc;
    private readonly IOrganizationCertificateDirectory _certificates;
    private readonly ILogger<SabyAuthenticator> _logger;
    private readonly Func<CpX509Certificate2, string?, AsymmetricAlgorithm?> _openKey;

    public SabyAuthenticator(
        SabyJsonRpc rpc,
        IOrganizationCertificateDirectory certificates,
        ILogger<SabyAuthenticator> logger)
        : this(rpc, certificates, logger, GostPrivateKey.Open)
    {
    }

    internal SabyAuthenticator(
        SabyJsonRpc rpc,
        IOrganizationCertificateDirectory certificates,
        ILogger<SabyAuthenticator> logger,
        Func<CpX509Certificate2, string?, AsymmetricAlgorithm?> openKey)
    {
        _rpc = rpc;
        _certificates = certificates;
        _logger = logger;
        _openKey = openKey;
    }

    public async Task<Result<string>> LoginAsync(EtrnOperatorContext context, CancellationToken cancellationToken)
    {
        var found = _certificates.Find(context.CertificateNumber);
        if (found.IsFailure)
            return Result.Failure<string>(found.Error);

        var certificate = found.Value.Certificate;
        try
        {
            return await OpenSessionAsync(context, certificate, cancellationToken);
        }
        finally
        {
            certificate.Dispose();
        }
    }

    private async Task<Result<string>> OpenSessionAsync(
        EtrnOperatorContext context,
        CpX509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        var der = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
        var answer = await _rpc.SendAsync(
            SabyJsonRpc.AuthUrl,
            EtrnMethods.AuthByCertificate,
            CertificateParameters(der),
            sessionId: null,
            SabyUserAgent.Value,
            cancellationToken);

        if (!answer.Ok)
            return Result.Failure<string>(answer.Error);

        var privateKey = _openKey(certificate, context.CertificatePassword);
        if (privateKey == null)
            return Result.Failure<string>("Не удалось получить закрытый ключ ГОСТ из сертификата");

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

    /// <summary>
    /// Saby читает «Сертификат» как объект. Строка Base64 даёт 500: The value is not an object.
    /// </summary>
    internal static Dictionary<string, object> CertificateParameters(string der) =>
        new()
        {
            ["Сертификат"] = new Dictionary<string, string>
            {
                ["ДвоичныеДанные"] = der
            }
        };

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

/// <summary>
/// Хеш Saby — готовый ГОСТ Р 34.11-2012 256. Обычный ContentInfo хеширует байты ещё раз.
/// </summary>
internal static class EtrnSignatureContent
{
    public const string Gost2012Hash256 = "1.2.643.7.1.1.2.2";

    private const string Pkcs7Data = "1.2.840.113549.1.7.1";

    public static string ContentTypeOid(bool preparedHash) =>
        preparedHash ? Gost2012Hash256 : Pkcs7Data;

    public static ContentInfo Create(bool preparedHash, byte[] content) =>
        preparedHash
            ? new ContentInfo(new Oid(Gost2012Hash256), content)
            : new ContentInfo(content);
}

public sealed class EtrnSignatureService : IEtrnSignaturePort
{
    public const string HttpClientName = "EtrnSignatureFile";

    private readonly IOrganizationCertificateDirectory _certificates;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EtrnSignatureService> _logger;

    public EtrnSignatureService(
        IOrganizationCertificateDirectory certificates,
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

        var certificate = found.Value.Certificate;
        try
        {
            return await SignFoundAsync(certificate, request, cancellationToken);
        }
        finally
        {
            certificate.Dispose();
        }
    }

    private async Task<Result<string>> SignFoundAsync(
        CpX509Certificate2 certificate,
        EtrnSignatureRequest request,
        CancellationToken cancellationToken)
    {
        (byte[] content, bool preparedHash) material;
        try
        {
            material = await ContentAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Не удалось получить данные для подписи: {ex.Message}");
        }

        try
        {
            var privateKey = GostPrivateKey.Open(certificate, request.CertificatePassword);
            if (privateKey == null)
                return Result.Failure<string>("Не удалось получить закрытый ключ ГОСТ из сертификата");

            var cms = new CpSignedCms(EtrnSignatureContent.Create(material.preparedHash, material.content), detached: true);
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

    private async Task<(byte[] Bytes, bool PreparedHash)> ContentAsync(
        EtrnSignatureRequest request,
        CancellationToken cancellationToken)
    {
        var material = EtrnSignatureMaterial.Resolve(request);
        if (material.IsFailure)
            throw new InvalidOperationException(material.Error);

        if (material.Value.Bytes != null)
            return (material.Value.Bytes, material.Value.PreparedHash);

        using var http = _httpClientFactory.CreateClient(HttpClientName);
        return (await http.GetByteArrayAsync(material.Value.FileUrl, cancellationToken), false);
    }
}
