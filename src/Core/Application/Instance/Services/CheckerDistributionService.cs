using Application.Instance.Interfaces;
using Application.SoftwareUpdates.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Dto.Checker;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.InstanceGroup;
using Domain.Entitys.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;
using Microsoft.Extensions.Logging;
using Shared.Json;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;

namespace Application.Instance.Services;

[AutoRegisterService]
public class CheckerDistributionService : ICheckerDistributionService
{
    private readonly IParametersService _parametersService;
    private readonly ICheckerTemplateLocator _templateLocator;
    private readonly ISoftwareUpdatesManagerService _softwareUpdates;
    private readonly IInstanceRepository _instances;
    private readonly IInstanceGroupRepository _groups;
    private readonly ILogger<CheckerDistributionService> _logger;

    /// <summary>
    /// Один zip в памяти: слот снимается, когда поток ответа закрыт.
    /// </summary>
    private static readonly SemaphoreSlim BuildGate = new(1, 1);

    public CheckerDistributionService(
        IParametersService parametersService,
        ICheckerTemplateLocator templateLocator,
        ISoftwareUpdatesManagerService softwareUpdates,
        IInstanceRepository instances,
        IInstanceGroupRepository groups,
        ILogger<CheckerDistributionService> logger)
    {
        _parametersService = parametersService;
        _templateLocator = templateLocator;
        _softwareUpdates = softwareUpdates;
        _instances = instances;
        _groups = groups;
        _logger = logger;
    }

    public async Task<Result<CheckerDistributionDownload>> Build(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure<CheckerDistributionDownload>("Не указан токен инстанса");

        var instance = await _instances.ByToken(token.Trim());
        if (instance.IsFailure)
            return Result.Failure<CheckerDistributionDownload>(instance.Error);

        var parameters = await _parametersService.Current();
        var address = parameters.ServerSettings.PublicAddress?.Trim() ?? string.Empty;
        if (address.Length == 0)
            return Result.Failure<CheckerDistributionDownload>("Не задан публичный адрес сервера");

        CheckerSettingsRead bundled = new(false, []);
        if (!string.IsNullOrEmpty(instance.Value.GroupId))
        {
            var read = await _groups.ReadCheckerSettings(instance.Value.GroupId);
            if (read.IsFailure)
                return Result.Failure<CheckerDistributionDownload>(CheckerSettingsAttachment.ReadFailedMessage);

            bundled = read.Value;
        }

        if (!await BuildGate.WaitAsync(0))
            return Result.Failure<CheckerDistributionDownload>(CheckerDistributionLimits.Busy);

        var handedOff = false;
        MemoryStream? stream = null;
        try
        {
            var templateZip = await EnsureTemplateZip();
            if (templateZip.IsFailure)
                return Result.Failure<CheckerDistributionDownload>(templateZip.Error);

            stream = new MemoryStream();
            await using (var template = File.OpenRead(templateZip.Value))
                await template.CopyToAsync(stream);

            stream.Position = 0;
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
            {
                var exeEntry = zip.Entries.FirstOrDefault(item => CheckerTemplateFiles.IsCheckerExe(item.Name));
                if (exeEntry == null)
                    return Result.Failure<CheckerDistributionDownload>("В пакете обновления нет fmu-api-check.exe");

                var configName = ZipDirectoryPrefix(exeEntry.FullName) + CheckerTemplateFiles.ConfigFileName;
                foreach (var existing in zip.Entries.Where(item =>
                             item.FullName.Replace('\\', '/').Equals(configName, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    existing.Delete();
                }

                var jsonEntry = zip.CreateEntry(configName);
                await using var jsonStream = jsonEntry.Open();
                await JsonSerializer.SerializeAsync(jsonStream, new CheckerClientSettings
                {
                    ServerAddress = address,
                    Token = token.Trim()
                }, JsonSerializeOptionsProvider.Default());

                if (bundled.Exists)
                {
                    var settingsName = ZipDirectoryPrefix(exeEntry.FullName) + CheckerSettingsAttachment.Name;
                    foreach (var existing in zip.Entries.Where(item =>
                                 item.FullName.Replace('\\', '/').Equals(settingsName, StringComparison.OrdinalIgnoreCase)).ToList())
                    {
                        existing.Delete();
                    }

                    var settingsEntry = zip.CreateEntry(settingsName);
                    await using var settingsStream = settingsEntry.Open();
                    await settingsStream.WriteAsync(bundled.Content);
                }
            }

            stream.Position = 0;
            var content = new ReleaseOnDisposeStream(stream, ReleaseBuildGate);
            handedOff = true;
            return Result.Success(new CheckerDistributionDownload
            {
                Content = content,
                FileName = "fmu-api.zip"
            });
        }
        finally
        {
            if (!handedOff)
            {
                if (stream != null)
                    await stream.DisposeAsync();
                BuildGate.Release();
            }
        }
    }

    public async Task<Result<string>> InstallCommand(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            var instance = await _instances.ByToken(token.Trim());
            if (instance.IsFailure)
                return Result.Failure<string>(instance.Error);
        }

        var parameters = await _parametersService.Current();
        return CheckerInstallCommand.Build(parameters.ServerSettings.PublicAddress, token);
    }

    private async Task<Result<string>> EnsureTemplateZip()
    {
        var folder = _templateLocator.Folder;
        var zipPath = Path.Combine(folder, CheckerTemplateFiles.UpdateZipName);
        var idPath = Path.Combine(folder, CheckerTemplateFiles.UpdateIdFileName);

        var list = await _softwareUpdates.List(1, 100);
        if (list.IsFailure)
            return Result.Failure<string>(list.Error);

        var update = LatestWindows(list.Value.Content);
        if (update == null)
        {
            if (File.Exists(zipPath))
                return Result.Success(zipPath);

            return Result.Failure<string>("Нет обновления fmu-api в базе");
        }

        var cachedId = File.Exists(idPath) ? (await File.ReadAllTextAsync(idPath)).Trim() : string.Empty;
        if (File.Exists(zipPath) && cachedId.Equals(update.Id, StringComparison.OrdinalIgnoreCase))
            return Result.Success(zipPath);

        _logger.LogInformation("Скачиваю шаблон zip обновления {UpdateId}", update.Id);

        var file = await _softwareUpdates.FmuApiUpdateFile(update.Id);
        if (file.IsFailure)
            return Result.Failure<string>(file.Error);

        Directory.CreateDirectory(folder);
        await using (var source = file.Value)
        await using (var target = File.Create(zipPath))
        {
            await source.CopyToAsync(target);
        }

        await File.WriteAllTextAsync(idPath, update.Id);
        _logger.LogInformation("Шаблон zip сохранён в {Path} ({UpdateId})", zipPath, update.Id);
        return Result.Success(zipPath);
    }

    private static void ReleaseBuildGate() => BuildGate.Release();

    private static string ZipDirectoryPrefix(string fullName)
    {
        var normalized = fullName.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? string.Empty : normalized[..(slash + 1)];
    }

    private static SoftwareUpdateFilesEntity? LatestWindows(IEnumerable<SoftwareUpdateFilesEntity> items)
    {
        return items
            .OrderByDescending(item => string.Equals(item.Os, "windows", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Version)
            .ThenByDescending(item => item.Assembly)
            .ThenByDescending(item => string.Equals(item.Architecture, "x64", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    /// <summary>
    /// Слот сборки живёт, пока ответ не дочитан: иначе два zip одновременно лежат в памяти.
    /// </summary>
    private sealed class ReleaseOnDisposeStream : Stream
    {
        private readonly Stream _inner;
        private readonly Action _release;
        private int _released;

        public ReleaseOnDisposeStream(Stream inner, Action release)
        {
            _inner = inner;
            _release = release;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => _inner.CopyToAsync(destination, bufferSize, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            Release();
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            Release();
        }

        private void Release()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                _release();
        }
    }
}
