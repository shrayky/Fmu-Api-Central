using Shared.FilesFolders;
using Shared.Installer.Interface;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace Shared.Installer;

[SupportedOSPlatform("windows")]
public class WindowsInstaller : IInstaller
{
    private readonly string _appName;
    private readonly string _serviceName;
    private readonly string _manufacture;
    private readonly int _serviceIpPort;
    private string _logFilePath;
    private string _logDirectory;
    private string _exeName;

    public WindowsInstaller(string appName, string serviceName, string manufacture, int serviceIpPort)
    {
        _appName = appName;
        _serviceName = serviceName;
        _manufacture = manufacture;
        _serviceIpPort = serviceIpPort;

        _logDirectory = Folders.CommonApplicationDataFolder(_manufacture, _appName);
        _logFilePath = Path.Combine(_logDirectory, "updateLog.txt");

        var installerFileName = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        _exeName = Path.GetFileName(installerFileName);
    }

    public void Install(string[] installerArgs)
    {
        StartOperationLog("installer");
        LogInfo("Старт установки сервиса.");
        LogInstallerDiagnostics();
        LogInfo($"Аргументы установки: {string.Join(" ", installerArgs.Select(a => $"\"{a}\""))}");

        var installDirectory = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "",
            "Program Files",
            _manufacture,
            _appName);
        
        if (!Directory.Exists(installDirectory))
            Directory.CreateDirectory(installDirectory);

        var installerFileName = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        var setupFolder = Path.GetDirectoryName(installerFileName) ?? installerFileName.Replace(_exeName, "");
        
        var binPath = Path.Combine(installDirectory, _exeName);
        var wwwrootPath = Path.Combine(installDirectory, "wwwroot");

        StopService();

        LogInfo("После остановки сервиса.");
        LogInstallerDiagnostics();

        var binDelete = DeleteFileWithRetry(binPath);
        var wwwRootDelete = false;

        if (binDelete)
            wwwRootDelete = DeleteDirectoryWithRetry(wwwrootPath);

        if (!(binDelete && wwwRootDelete))
        {
            LogError($"Статус очистки каталога wwwroot {wwwRootDelete} удаление bin {binDelete}");
            return;
        }

        try
        {
            LogInfo("Копирую новые файлы");
            Folders.CopyDirectory(setupFolder, installDirectory);
        }
        catch (Exception ex)
        {
            LogError($"Ошибка при копировании новых файлов {ex}");
            return;
        }

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
        existingService = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == _serviceName);

        if (existingService == null) 
        {
            Console.WriteLine($"Служба {_serviceName} не существует");
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
        existingService = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == _serviceName);

        if (existingService != null)
        {
            Console.WriteLine($"Служба {_serviceName} уже существует");
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
        ServiceController? existingService;
        
        existingService = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == _serviceName);

        if (existingService == null)
        {
            Console.WriteLine($"Не существует службы {_serviceName}");
            return;
        }

        if (existingService.Status != ServiceControllerStatus.Running)
        {
            Console.WriteLine($"Служба {_serviceName} не выполняется");
            return;
        }

        existingService.Stop();
        Task.Delay(TimeSpan.FromSeconds(15));

        try
        {
            existingService.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1));
        }
        catch
        {
            LogInfo($"Служба не остановлена за 1 минуту, принудительно убиваю процесс");
            KillService();
        }

        Task.Delay(TimeSpan.FromSeconds(10));

        Console.WriteLine($"Служба {_serviceName}, остановлена");
    }
    
    private void StartService()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;
        
        ServiceController? existingService;
        
        existingService = ServiceController.GetServices().FirstOrDefault(ser => ser.ServiceName == _serviceName);

        if (existingService == null)
        {
            Console.WriteLine($"Не удалось запустить службу {_serviceName} - ее не существует");
            return;
        }

        if (existingService.Status == ServiceControllerStatus.Running)
        {
            Console.WriteLine($"Служба {_serviceName} уже запущена");
            return;
        }

        existingService.Start();
        existingService.WaitForStatus(ServiceControllerStatus.Running);
        
        Console.WriteLine($"Служба {_serviceName}, запущена", _serviceName);
    }

    private static bool DeleteFileWithRetry(string filePath, int retries = 5, int delaySeconds = 5)
    {
        if (!File.Exists(filePath))
            return true;

        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                File.Delete(filePath);
                Console.WriteLine($"[Installer] Файл удален: {filePath}");
                return true;
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[Installer] Попытка {attempt}/{retries} удалить файл {filePath} (IOException): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[Installer] Попытка {attempt}/{retries} удалить файл {filePath} (UnauthorizedAccessException): {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Installer] Неожиданная ошибка при удалении {filePath}: {ex}");
                return false;
            }

            if (attempt < retries)
                Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        if (File.Exists(filePath))
        {
            Console.WriteLine($"[Installer] Не удалось удалить файл после {retries} попыток: {filePath}");
            return false;
        }

        return true;
    }
    private static bool DeleteDirectoryWithRetry(string directoryPath, int retries = 5, int delaySeconds = 5)
    {
        if (!Directory.Exists(directoryPath))
            return true;

        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                Directory.Delete(directoryPath, true);
                Console.WriteLine($"[Installer] Каталог удален: {directoryPath}");
                return true;
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[Installer] Попытка {attempt}/{retries} удалить каталог {directoryPath} (IOException): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[Installer] Попытка {attempt}/{retries} удалить каталог {directoryPath} (UnauthorizedAccessException): {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Installer] Неожиданная ошибка при удалении {directoryPath}: {ex}");
                return false;
            }

            if (attempt < retries)
                Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        if (Directory.Exists(directoryPath))
        {
            Console.WriteLine($"[Installer] Не удалось удалить каталог после {retries} попыток: {directoryPath}");
            return false;
        }

        return true;
    }

    private void KillService()
    {
        var currentPid = Environment.ProcessId;

        var processes = Process.GetProcessesByName(_exeName);
        LogInfo($"Процессы с именем '{_exeName}' (образ {_appName}): найдено {processes.Length}.");

        foreach (var p in processes)
        {
            try
            {
                if (p.Id == currentPid)
                    continue;

                LogInfo($"Убиваю {p.Id} {p.MainModule?.FileName ?? "(не найден путь)"}");

                p.Kill(true);
                p.WaitForExit(TimeSpan.FromSeconds(5));

                Task.Delay(TimeSpan.FromSeconds(10));
            }
            finally
            {
                p.Dispose();
            }
        }
    }

    private void StartOperationLog(string operationName)
    {
        Directory.CreateDirectory(_logDirectory);
        _logFilePath = Path.Combine(_logDirectory, "updateLog.txt");
        File.WriteAllText(_logFilePath, string.Empty);
        WriteLog("INFO", $"Старт операции '{operationName}'.");
    }

    private void LogInfo(string message)
    {
        WriteLog("INFO", message);
    }

    private void LogError(string message)
    {
        WriteLog("ERROR", message);
    }

    private void WriteLog(string level, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}][{level}] {message}{Environment.NewLine}";

        Console.Write(line);

        if (!string.IsNullOrWhiteSpace(_logFilePath))
            File.AppendAllText(_logFilePath, line);
    }

    private void LogInstallerDiagnostics()
    {
        LogInfo($"Текущий процесс установки: PID={Environment.ProcessId}, путь={Environment.ProcessPath ?? "(неизвестно)"}");

        var processes = Process.GetProcessesByName(_exeName);
        LogInfo($"Процессы с именем '{_exeName}' (образ {_appName}): найдено {processes.Length}.");

        foreach (var p in processes)
        {
            try
            {
                var imagePath = p.MainModule?.FileName ?? "(нет)";
                LogInfo($"  PID={p.Id}, путь к образу={imagePath}");
            }
            catch (Exception ex)
            {
                LogInfo($"  PID={p.Id}, путь к образу недоступен: {ex.Message}");
            }
            finally
            {
                p.Dispose();
            }
        }
    }
}