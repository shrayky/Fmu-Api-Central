using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Shared.FilesFolders;
using Shared.Installer.Interface;
using static System.Console;
using static System.ServiceProcess.ServiceController;

namespace Shared.Installer;

public class WindowsInstaller : IInstaller
{
    private readonly string _appName;
    private readonly string _serviceName;
    private readonly string _manufacture;
    private readonly int _serviceIpPort;

    public WindowsInstaller(string appName, string serviceName, string manufacture, int serviceIpPort)
    {
        _appName = appName;
        _serviceName = serviceName;
        _manufacture = manufacture;
        _serviceIpPort = serviceIpPort;
    }

    public void Install(string[] installerArgs)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        var installDirectory = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "",
            "Program Files",
            _manufacture,
            _appName);
        
        if (!Directory.Exists(installDirectory))
            Directory.CreateDirectory(installDirectory);

        var installerFileName = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        var exeName = Path.GetFileName(installerFileName);
        var setupFolder = Path.GetDirectoryName(installerFileName) ?? installerFileName.Replace(exeName, "");
        
        var binPath = Path.Combine(installDirectory, exeName);
        var wwwrootPath = Path.Combine(installDirectory, "wwwroot");

        StopService();
        
        if (File.Exists(binPath))
            File.Delete(binPath);
        
        if (Directory.Exists(wwwrootPath))
            Directory.Delete(wwwrootPath, true);
        
        Folders.CopyDirectory(setupFolder, installDirectory);

        CreateService(binPath);
        
        StartService();
    }
    
    public void Uninstall()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        var installDirectory = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "",
            "Program Files",
            _manufacture,
            _appName);
        
        if (!Directory.Exists(installDirectory))
            return;

        var exeName = $"{_appName}.exe";
        
        var binPath = Path.Combine(installDirectory, exeName);
        var wwwrootPath = Path.Combine(installDirectory, "wwwroot");

        StopService();

        RemoveService();
        
        if (File.Exists(binPath))
            File.Delete(binPath);
        
        if (Directory.Exists(wwwrootPath))
            Directory.Delete(wwwrootPath, true);
    }

    private void RemoveService()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        ServiceController? existingService;
        existingService = GetServices().FirstOrDefault(ser => ser.ServiceName == _serviceName);

        if (existingService == null) 
        {
            WriteLine($"Служба {_serviceName} не существует");
            return;
        }
        
        Process process = new();
        ProcessStartInfo startInfo = new()
        {
            WindowStyle = ProcessWindowStyle.Hidden
        };

        process.StartInfo = startInfo;
        startInfo.FileName = "cmd.exe";

        startInfo.Arguments = $"/c sc delete {_serviceName}";
        process.Start();
        
        startInfo.Arguments = $"/c netsh advfirewall firewall delete rule name = \"{_serviceName}\"";
        process.Start();
    }

    private void CreateService(string bin)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        ServiceController? existingService;
        existingService = GetServices().FirstOrDefault(ser => ser.ServiceName == _serviceName);

        if (existingService != null)
        {
            WriteLine($"Служба {_serviceName} уже существует");
            return;
        }

        Process process = new();
        ProcessStartInfo startInfo = new()
        {
            WindowStyle = ProcessWindowStyle.Hidden
        };

        process.StartInfo = startInfo;
        startInfo.FileName = "cmd.exe";

        startInfo.Arguments = $"/c sc create {_serviceName} binPath= \"{bin}\" DisplayName= \"{_serviceName}\" type= own start= auto";
        process.Start();

        startInfo.Arguments = $"/c sc failure \"{_serviceName}\" reset= 5 actions= restart/5000";
        process.Start();

        startInfo.Arguments = $"/c netsh advfirewall firewall delete rule name = \"{_serviceName}\"";
        process.Start();

        if (_serviceIpPort != 0)
        {
            startInfo.Arguments =
                $"/c netsh advfirewall firewall add rule name = \"{_serviceName}\" dir =in action = allow protocol = TCP localport = {_serviceIpPort}";
            process.Start();
        }

        startInfo.Arguments = $"/c net start {_serviceName}";
        process.Start();
    }
    
    private void StopService()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        ServiceController? existingService;
        
        existingService = GetServices().FirstOrDefault(ser => ser.ServiceName == _serviceName);

        if (existingService == null)
        {
            WriteLine($"Не существует службы {_serviceName}");
            return;
        }

        if (existingService.Status != ServiceControllerStatus.Running)
        {   
            WriteLine($"Служба {_serviceName} не выполняется");
            return;
        }

        existingService.Stop();
        existingService.WaitForStatus(ServiceControllerStatus.Stopped);
        
        WriteLine($"Служба {_serviceName}, остановлена");
    }
    
    private void StartService()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        ServiceController? existingService;
        
        existingService = GetServices().FirstOrDefault(ser => ser.ServiceName == _serviceName);

        if (existingService == null)
        {
            WriteLine($"Не удалось запустить службу {_serviceName} - ее не существует");
            return;
        }

        if (existingService.Status == ServiceControllerStatus.Running)
        {
            WriteLine($"Служба {_serviceName} уже запущена");
            return;
        }

        existingService.Start();
        existingService.WaitForStatus(ServiceControllerStatus.Running);
        
        WriteLine($"Служба {_serviceName}, запущена", _serviceName);
    }
}