using System.Diagnostics;

namespace Shared.Installer;

/// <summary>
/// Запуск host-приложения из WebApi/WebApp для --install/--uninstall.
/// </summary>
public static class HostProcessLauncher
{
    public const string HostExeName = "fmu-api-central.exe";

    private static readonly string[] HostCommands =
    [
        "--install",
        "--uninstall",
        "--register",
        "--unregister"
    ];

    /// <summary>
    /// Проверяет, что среди аргументов есть команда хоста.
    /// </summary>
    public static bool IsHostCommand(string[] args) =>
        args.Any(a => HostCommands.Contains(a.Trim(), StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Запускает fmu-api-central.exe с теми же аргументами.
    /// </summary>
    public static int Run(string[] args)
    {
        var hostPath = FindHostExe();
        if (hostPath is null)
        {
            Console.WriteLine(
                $"Не найден {HostExeName}. Установку выполняет хост: {HostExeName} --install");
            return 1;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = hostPath,
            WorkingDirectory = Path.GetDirectoryName(hostPath) ?? AppContext.BaseDirectory,
            UseShellExecute = false
        };

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.WriteLine($"Не удалось запустить {hostPath}");
            return 1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static string? FindHostExe()
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        string[] relative =
        [
            HostExeName,
            Path.Combine("..", HostExeName),
            Path.Combine("..", "..", HostExeName)
        ];

        foreach (var rel in relative)
        {
            var full = Path.GetFullPath(Path.Combine(dir, rel));
            if (File.Exists(full))
                return full;
        }

        return null;
    }
}
