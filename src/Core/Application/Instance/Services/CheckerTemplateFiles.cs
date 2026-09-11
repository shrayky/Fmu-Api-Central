namespace Application.Instance.Services;

public static class CheckerTemplateFiles
{
    public const string UpdateZipName = "update.zip";
    public const string UpdateIdFileName = "update.id";
    public const string ConfigFileName = "config.json";

    public static bool IsCheckerExe(string fileName)
    {
        return fileName.Equals("fmu-api-check.exe", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("fmu-api-checker.exe", StringComparison.OrdinalIgnoreCase);
    }

    public static string? FindExe(string folder)
    {
        if (!Directory.Exists(folder))
            return null;

        return Directory.EnumerateFiles(folder, "*.exe")
            .FirstOrDefault(path => IsCheckerExe(Path.GetFileName(path)));
    }
}
