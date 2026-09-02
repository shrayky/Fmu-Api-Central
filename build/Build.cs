using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Release);

    const string HostExeName = "fmu-api-central.exe";
    const string ApiProductName = "fmu-api-central-api";
    const string WebProductName = "fmu-api-central-web";

    [Parameter("Версия архива, например 1-7")]
    readonly string Version = default!;

    AbsolutePath WebApiProject => RootDirectory / "src" / "Presentation" / "WebApi" / "WebApi.csproj";
    AbsolutePath WebAppProject => RootDirectory / "src" / "Presentation" / "WebApp" / "WebApp" / "WebApp.csproj";
    AbsolutePath HostAppProject => RootDirectory / "src" / "Presentation" / "HostApp" / "HostApp.csproj";
    AbsolutePath BuildsDirectory => RootDirectory / "builds";

    AbsolutePath ApiWinX64 => BuildsDirectory / "api-win-x64";
    AbsolutePath WebWinX64 => BuildsDirectory / "web-win-x64";
    AbsolutePath ApiLinuxX64 => BuildsDirectory / "api-linux-x64";
    AbsolutePath WebLinuxX64 => BuildsDirectory / "web-linux-x64";
    AbsolutePath HostWinX64 => BuildsDirectory / "ha-win-x64";

    string? _archiveVersion;
    string? _versionFolder;

    Target PublishApps => _ => _
        .Executes(() =>
        {
            PublishApp(WebApiProject, "win-x64", ApiWinX64);
            PublishApp(WebAppProject, "win-x64", WebWinX64);
            PublishApp(WebApiProject, "linux-x64", ApiLinuxX64);
            PublishApp(WebAppProject, "linux-x64", WebLinuxX64);
        });

    Target PublishHost => _ => _
        .Executes(() =>
        {
            PublishHostApp(HostAppProject, "win-x64", HostWinX64);
        });

    Target ResolveVersion => _ => _
        .Unlisted()
        .Executes(() => EnsureArchiveVersion());

    Target PackWindows => _ => _
        .DependsOn(PublishApps, PublishHost, ResolveVersion)
        .Executes(() =>
        {
            var hostExe = HostWinX64 / HostExeName;
            Assert.True(hostExe.FileExists(), $"Не найден host: {hostExe}");

            var staging = CreateCleanStaging("win-x64");
            var versionFolder = EnsureVersionFolder();

            CopyItem(hostExe, staging / HostExeName);
            PackProduct(ApiWinX64, ApiProductName, staging / ApiProductName / versionFolder, windows: true, requireWwwroot: false);
            PackProduct(WebWinX64, WebProductName, staging / WebProductName / versionFolder, windows: true, requireWwwroot: true);

            ZipStaging(staging, BuildsDirectory / $"{EnsureArchiveVersion()}-x64-win.zip");
        });

    Target PackLinux => _ => _
        .DependsOn(PublishApps, ResolveVersion)
        .Executes(() =>
        {
            var staging = CreateCleanStaging("linux-x64");

            PackProduct(ApiLinuxX64, ApiProductName, staging / ApiProductName, windows: false, requireWwwroot: false);
            PackProduct(WebLinuxX64, WebProductName, staging / WebProductName, windows: false, requireWwwroot: true);

            ZipStaging(staging, BuildsDirectory / $"{EnsureArchiveVersion()}-x64-linux.zip");
        });

    Target Release => _ => _
        .DependsOn(PackWindows, PackLinux)
        .Executes(() =>
        {
            var version = EnsureArchiveVersion();
            Serilog.Log.Information("Archives successfully created:");
            Serilog.Log.Information("- {Zip}", BuildsDirectory / $"{version}-x64-win.zip");
            Serilog.Log.Information("- {Zip}", BuildsDirectory / $"{version}-x64-linux.zip");
        });

    /// <summary>
    /// Публикует приложение как self-contained single-file.
    /// </summary>
    void PublishApp(AbsolutePath project, string runtime, AbsolutePath output)
    {
        output.CreateOrCleanDirectory();
        DotNetPublish(s => s
            .SetProject(project)
            .SetConfiguration("Release")
            .SetRuntime(runtime)
            .SetSelfContained(true)
            .SetPublishSingleFile(true)
            .SetOutput(output));
    }

    /// <summary>
    /// Native AOT нельзя совмещать с PublishSingleFile — SDK выдаёт ошибку линковки.
    /// </summary>
    void PublishHostApp(AbsolutePath project, string runtime, AbsolutePath output)
    {
        output.CreateOrCleanDirectory();
        DotNetPublish(s => s
            .SetProject(project)
            .SetConfiguration("Release")
            .SetRuntime(runtime)
            .SetSelfContained(true)
            .SetProperty("PublishAot", true)
            .SetOutput(output));
    }

    /// <summary>
    /// Кладёт бинарник продукта и при необходимости wwwroot в каталог архива.
    /// </summary>
    void PackProduct(
        AbsolutePath publishDir,
        string productName,
        AbsolutePath targetDir,
        bool windows,
        bool requireWwwroot)
    {
        var binaryName = windows ? $"{productName}.exe" : productName;
        var sourceBinary = publishDir / binaryName;
        Assert.True(sourceBinary.FileExists(), $"Не найден продукт: {sourceBinary}");

        targetDir.CreateDirectory();
        CopyItem(sourceBinary, targetDir / binaryName);

        var wwwroot = publishDir / "wwwroot";
        if (requireWwwroot)
        {
            Assert.True(wwwroot.DirectoryExists(), $"Не найден wwwroot: {wwwroot}");
            CopyItem(wwwroot, targetDir / "wwwroot");
            return;
        }

        if (wwwroot.DirectoryExists())
            CopyItem(wwwroot, targetDir / "wwwroot");
    }

    /// <summary>
    /// Возвращает версию архива; если не передана --Version, спрашивает в консоли.
    /// </summary>
    string EnsureArchiveVersion()
    {
        if (!string.IsNullOrWhiteSpace(_archiveVersion))
            return _archiveVersion;

        var value = string.IsNullOrWhiteSpace(Version)
            ? ReadVersionFromConsole()
            : Version;

        Assert.False(string.IsNullOrWhiteSpace(value), "Sorry, wrong version number.");
        _archiveVersion = value;
        _versionFolder = value.Replace("-", ".", StringComparison.Ordinal);
        return value;
    }

    /// <summary>
    /// Каталог версии для хоста: 1-7 -> 1.7, чтобы сработал Version.TryParse.
    /// </summary>
    string EnsureVersionFolder()
    {
        EnsureArchiveVersion();
        return _versionFolder!;
    }

    /// <summary>
    /// Спрашивает версию в консоли, как в fmu-api.
    /// </summary>
    static string ReadVersionFromConsole()
    {
        Console.Write("Print software version (for example 1-7): ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Создаёт пустой каталог раскладки архива.
    /// </summary>
    AbsolutePath CreateCleanStaging(string suffix)
    {
        var staging = TemporaryDirectory / $"fmu-central-archive-{suffix}";
        staging.CreateOrCleanDirectory();
        return staging;
    }

    /// <summary>
    /// Упаковывает содержимое staging в zip и удаляет временный каталог.
    /// </summary>
    void ZipStaging(AbsolutePath staging, AbsolutePath zip)
    {
        BuildsDirectory.CreateDirectory();
        if (zip.FileExists())
            zip.DeleteFile();
        staging.ZipTo(zip);
        staging.DeleteDirectory();
        Serilog.Log.Information("Создан {Zip}", zip);
    }

    /// <summary>
    /// Копирует файл или каталог в назначение.
    /// </summary>
    static void CopyItem(AbsolutePath source, AbsolutePath destination)
    {
        if (source.FileExists())
        {
            destination.Parent.CreateDirectory();
            File.Copy(source, destination, overwrite: true);
            return;
        }

        Assert.True(source.DirectoryExists(), $"Не найден каталог: {source}");
        destination.CreateDirectory();

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = destination / relative;
            target.Parent.CreateDirectory();
            File.Copy(file, target, overwrite: true);
        }
    }
}
