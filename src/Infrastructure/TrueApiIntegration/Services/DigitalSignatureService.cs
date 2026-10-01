using CryptoPro.Security.Cryptography.X509Certificates;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace TrueApiIntegration.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class DigitalSignatureService : IDigitalSignatureService
{
    public List<DigitalSignature> List() => Read(includeExpired: false);

    public List<DigitalSignature> ListIncludingExpired() => Read(includeExpired: true);

    private static List<DigitalSignature> Read(bool includeExpired)
    {
        var answer = new List<DigitalSignature>();

        try
        {
            var store = new CpX509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            foreach (var certificate in store.Certificates)
            {
                if (!includeExpired && certificate.NotAfter <= DateTime.Now)
                    continue;

                if (certificate.Issuer.Contains("DO_NOT_TRUST"))
                    continue;

                var signature = new DigitalSignature
                {
                    Presentation = certificate.Subject,
                    WorkUntil = certificate.NotAfter,
                    Number = certificate.GetSerialNumberString()
                };

                var subjectLines = certificate.Subject.Split(",");
                var lineWithInn = subjectLines.FirstOrDefault(l => l.Contains("ИНН ЮЛ"));
                lineWithInn ??= subjectLines.FirstOrDefault(l => l.Contains("ИНН"));

                if (lineWithInn != null)
                {
                    var parts = lineWithInn.Split("=");
                    if (parts.Length >= 2)
                        signature.Inn = parts[1];
                }

                answer.Add(signature);
            }

            store.Close();
        }
        catch (Exception)
        {
            return [];
        }

        return answer;
    }

    public Result Install(Stream archive)
    {
        var container = CryptoProContainerArchive.Open(archive);
        if (container.IsFailure)
            return Result.Failure(container.Error);

        var csptest = FindCsptest();
        if (csptest == null)
            return Result.Failure("Не найден csptest КриптоПро");

        var storage = KeyStorageDirectory();
        try
        {
            Directory.CreateDirectory(storage);
            RestrictDirectory(storage);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Не удалось установить сертификат: {ex.Message}");
        }

        return CryptoProContainerInstall.Replace(storage, container.Value, destination =>
        {
            RestrictKeyPermissions(destination);
            return AbsorbCertificates(csptest, container.Value.Name);
        });
    }

    /// <summary>
    /// Windows: каталог HDIMAGE текущего пользователя. Linux: ключи пользователя службы.
    /// </summary>
    private static string KeyStorageDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Crypto Pro");
        }

        return Path.Combine("/var/opt/cprocsp/keys", Environment.UserName);
    }

    private static string? FindCsptest()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var path in new[]
            {
                @"C:\Program Files\Crypto Pro\CSP\csptest.exe",
                @"C:\Program Files (x86)\Crypto Pro\CSP\csptest.exe"
            })
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        const string linux = "/opt/cprocsp/bin/amd64/csptest";
        return File.Exists(linux) ? linux : null;
    }

    /// <summary>
    /// Сертификат лежит внутри контейнера. csptest -absorb переносит его в хранилище CurrentUser.
    /// </summary>
    private static Result AbsorbCertificates(string csptest, string containerName)
    {
        var start = new ProcessStartInfo
        {
            FileName = csptest,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-absorb");
        start.ArgumentList.Add("-certs");
        start.ArgumentList.Add("-silent");
        start.ArgumentList.Add("-pattern");
        start.ArgumentList.Add(containerName);

        using var process = Process.Start(start);
        if (process == null)
            return Result.Failure("Не удалось запустить csptest");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            return Result.Failure("csptest не завершился");
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            return Result.Failure(ShortError(stderr, stdout));

        if (!CryptoProAbsorb.LinksContainer(stdout, containerName))
            return Result.Failure("В контейнере нет сертификата");

        return Result.Success();
    }

    private static string ShortError(string stderr, string stdout)
    {
        var text = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        text = text.ReplaceLineEndings(" ").Trim();
        if (text.Length > 300)
            text = text[..300];

        return string.IsNullOrWhiteSpace(text)
            ? "КриптоПро не установил сертификат из контейнера"
            : text;
    }

    /// <summary>
    /// КриптоПро не читает ключи, если они доступны группе или остальным.
    /// </summary>
    private static void RestrictDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
            return;

        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void RestrictKeyPermissions(string directory)
    {
        RestrictDirectory(directory);
        if (OperatingSystem.IsWindows())
            return;

        foreach (var file in Directory.GetFiles(directory))
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
