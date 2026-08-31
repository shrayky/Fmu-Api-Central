using CryptoPro.Security.Cryptography;
using CryptoPro.Security.Cryptography.Pkcs;
using CryptoPro.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.TrueApiIntegration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Json;
using System.Net.Http.Json;
using System.Security;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TrueApiIntegration.Models;

namespace TrueApiIntegration.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class TrueApiAuthService : ITrueApiAuthService
{
    private readonly ILogger<TrueApiAuthService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string Url = "https://markirovka.crpt.ru";
    private const string AuthPath = "/api/v3/true-api/auth/key";
    private const string SignInPath = "/api/v3/true-api/auth/simpleSignIn";

    public TrueApiAuthService(ILogger<TrueApiAuthService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Result<string>> GenerateToken(string inn, string password, string signatureNumber)
    {
        var data = await DataForEncrypt();
        if (data.IsFailure)
            return Result.Failure<string>(data.Error);

        var encrypted = Encrypt(data.Value.Data, signatureNumber, inn, password);
        if (encrypted.IsFailure)
            return Result.Failure<string>(encrypted.Error);

        return await FinishAuth(encrypted.Value, data.Value.Uuid, inn);
    }

    private async Task<Result<DataWithUuid>> DataForEncrypt()
    {
        using var httpClient = _httpClientFactory.CreateClient("TrueApiIntegration");
        httpClient.BaseAddress = new Uri(Url);

        try
        {
            var answer = await httpClient.GetFromJsonAsync<DataWithUuid>(AuthPath);
            if (answer == null)
                throw new Exception($"Пустой ответ от {AuthPath}");

            return Result.Success(answer);
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка получения данных авторизации в true api {err}", ex);
            return Result.Failure<DataWithUuid>($"Ошибка получения данных авторизации в true api {ex.Message}");
        }
    }

    private Result<string> Encrypt(string data, string signatureNumber, string inn, string password)
    {
        try
        {
            var store = new CpX509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            CpX509Certificate2? certificate = null;

            foreach (var storeCertificate in store.Certificates)
            {
                if (storeCertificate.NotAfter <= DateTime.Now)
                    continue;

                if (storeCertificate.Issuer.Contains("DO_NOT_TRUST"))
                    continue;

                if (storeCertificate.GetSerialNumberString() == signatureNumber)
                {
                    certificate = storeCertificate;
                    break;
                }

                if (storeCertificate.Subject.Contains("ОГРНИП"))
                {
                    if (!storeCertificate.Subject.Contains(inn))
                        continue;
                }
                else
                {
                    if (!storeCertificate.Subject.Contains($"ИНН ЮЛ={inn}"))
                        continue;
                }

                if (signatureNumber == string.Empty)
                {
                    certificate = storeCertificate;
                    break;
                }
            }

            store.Close();

            if (certificate == null)
            {
                var msg = $"Для ИНН {inn} не найден действующий сертификат";
                _logger.LogError(msg);
                return Result.Failure<string>(msg);
            }

            _logger.LogInformation("Выбран сертификат для авторизации в true api {info}", certificate.Subject);

            var privateKey = certificate.GetGost3410_2012_256PrivateKey()
                            ?? certificate.GetGost3410_2012_512PrivateKey() as Gost3410Algorithm
                            ?? certificate.GetGost3410PrivateKey();

            if (privateKey == null)
                return Result.Failure<string>("Не удалось получить закрытый ключ ГОСТ из сертификата");

            if (!string.IsNullOrEmpty(password))
            {
                var securePassword = new SecureString();
                foreach (char c in password)
                    securePassword.AppendChar(c);
                securePassword.MakeReadOnly();

                if (privateKey is Gost3410_2012_256CryptoServiceProvider csp256)
                    csp256.SetContainerPassword(securePassword);
                else if (privateKey is Gost3410_2012_512CryptoServiceProvider csp512)
                    csp512.SetContainerPassword(securePassword);
                else if (privateKey is Gost3410CryptoServiceProvider csp3410)
                    csp3410.SetContainerPassword(securePassword);
            }

            byte[] dataToSign = Encoding.UTF8.GetBytes(data);
            var contentInfo = new ContentInfo(dataToSign);
            var signedCms = new CpSignedCms(contentInfo, detached: false);

            var signer = new CpCmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate, privateKey);
            signer.IncludeOption = X509IncludeOption.WholeChain;

            signedCms.ComputeSignature(signer, silent: true);

            byte[] signatureBytes = signedCms.Encode();
            return Result.Success(Convert.ToBase64String(signatureBytes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка подписи при авторизации True API");
            return Result.Failure<string>(ex.Message);
        }
    }

    private async Task<Result<string>> FinishAuth(string encodedData, string requestId, string inn)
    {
        using var httpClient = _httpClientFactory.CreateClient("TrueApiIntegration");
        httpClient.BaseAddress = new Uri(Url);

        DataWithUuid data = new()
        {
            Uuid = requestId,
            Data = encodedData,
        };

        if (!string.IsNullOrEmpty(inn))
            data.Inn = inn;

        try
        {
            var answer = await httpClient.PostAsJsonAsync(SignInPath, data);
            if (answer == null)
                throw new Exception($"Пустой ответ от {SignInPath}");

            if (!answer.IsSuccessStatusCode)
                throw new Exception($"{SignInPath} вернул код ошибки {answer.StatusCode}");

            var rawJson = await answer.Content.ReadAsStringAsync();
            _logger.LogDebug("Ответ simpleSignIn : {Json}", rawJson);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
            var answerData = await JsonHelpers.DeserializeAsync<AuthData>(stream);
            if (answerData == null)
                throw new Exception($"Ошибка преобразования ответа в {SignInPath}");

            if (string.IsNullOrEmpty(answerData.Token))
                throw new Exception(string.IsNullOrEmpty(answerData.ErrorMessage)
                    ? "True API не вернул токен"
                    : answerData.ErrorMessage);

            return Result.Success(answerData.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка получения данных авторизации в true api {err}", ex);
            return Result.Failure<string>($"Ошибка получения данных авторизации в true api {ex.Message}");
        }
    }
}
