namespace Shared.FilesFolders
{
    public static class Folders
    {
        public static string LogFolder(string manufacture, string appName)
        {
            var user = Environment.UserName;
            var logFolder = string.Empty;

            if (OperatingSystem.IsWindows())
            {
                logFolder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                logFolder = Path.Combine(logFolder, manufacture, appName, "log");
            }
            else if (OperatingSystem.IsLinux())
            {
                logFolder = "/var/log";
                logFolder = Path.Combine(logFolder, appName);
            }

            return logFolder;
        }

        public static string CommonApplicationDataFolder(string manufacture, string appName)
        {
            var configFolder = string.Empty;

            if (OperatingSystem.IsWindows())
            {
                configFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    manufacture,
                    appName);
            }
            else if (OperatingSystem.IsLinux())
            {
                configFolder = Path.Combine("/var/lib", appName);
            }

            return configFolder;
        }
    }
}
