using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.TrueApiIntegration;
using Domain.TrueApiIntegration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace TrueApiIntegration.Services;

[AutoRegisterService(ServiceLifetime.Singleton)]
public class CryptoProLicenseService : ICryptoProLicenseService
{
    public Result<string> View() => Run("view", serial: null);

    public Result<string> Set(string serial)
    {
        var normalized = CryptoProLicenseSerial.Normalize(serial);
        if (normalized.IsFailure)
            return Result.Failure<string>(normalized.Error);

        var setResult = Run("set", normalized.Value);
        if (setResult.IsFailure)
            return setResult;

        return View();
    }

    private static Result<string> Run(string command, string? serial)
    {
        try
        {
            var cpconfig = FindCpconfig();
            if (cpconfig == null)
                return Result.Failure<string>("Не найден cpconfig КриптоПро");

            var start = new ProcessStartInfo
            {
                FileName = cpconfig,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add("-license");
            start.ArgumentList.Add("-" + command);
            if (serial != null)
                start.ArgumentList.Add(serial);

            using var process = Process.Start(start);
            if (process == null)
                return Result.Failure<string>("Не удалось запустить cpconfig");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                return Result.Failure<string>("cpconfig не завершился");
            }

            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
                return Result.Failure<string>(ShortError(stderr, stdout));

            var text = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
            return Result.Success(text.Trim());
        }
        catch (Exception ex)
        {
            return Result.Failure<string>($"Не удалось выполнить cpconfig: {ex.Message}");
        }
    }

    /// <summary>
    /// На Linux cpconfig лежит в sbin, не рядом с csptest.
    /// </summary>
    private static string? FindCpconfig()
    {
        if (OperatingSystem.IsWindows())
        {
            foreach (var path in new[]
            {
                @"C:\Program Files\Crypto Pro\CSP\cpconfig.exe",
                @"C:\Program Files (x86)\Crypto Pro\CSP\cpconfig.exe"
            })
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        foreach (var path in new[]
        {
            "/opt/cprocsp/sbin/amd64/cpconfig",
            "/opt/cprocsp/sbin/ia32/cpconfig"
        })
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string ShortError(string stderr, string stdout)
    {
        var text = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        text = text.ReplaceLineEndings(" ").Trim();
        if (text.Length > 300)
            text = text[..300];

        return string.IsNullOrWhiteSpace(text)
            ? "КриптоПро не принял лицензию"
            : text;
    }
}
