using System.Runtime.InteropServices;
using Shared.Installer.Interface;

namespace Shared.Installer;

public class InstallerFabric
{
    public static void Install(string[] args, string appName, string serviceName, string manufacture, int serviceIpPort)
    {
        var installer = Installer(appName, serviceName, manufacture, serviceIpPort);

        installer.Install(args);
    }

    public static void Uninstall(string appName, string serviceName, string manufacture, int serviceIpPort)
    {
        var installer = Installer(appName, serviceName, manufacture, serviceIpPort);

        installer.Uninstall();
    }
    
    private static IInstaller Installer(string appName, string serviceName, string manufacture, int serviceIpPort)
    {
        IInstaller installer;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            installer = new WindowsInstaller(appName, serviceName, manufacture, serviceIpPort);
        else
            installer = new LinuxInstaller();
        return installer;
    }
}