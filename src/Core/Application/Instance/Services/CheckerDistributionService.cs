using Application.Instance.Interfaces;
using Application.SoftwareUpdates.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Attributes;
using Domain.Configuration.Interfaces;
using Domain.Dto.Checker;
using Domain.Entitys.Instance.Dto;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;
using Microsoft.Extensions.Logging;
using Shared.Json;
using System.IO.Compression;
using System.Text.Json;

namespace Application.Instance.Services;

[AutoRegisterService]
public class CheckerDistributionService : ICheckerDistributionService
{
    private readonly IParametersService _parametersService;
    private readonly ICheckerTemplateLocator _templateLocator;
    private readonly ISoftwareUpdatesManagerService _softwareUpdates;
    private readonly ILogger<CheckerDistributionService> _logger;

    public CheckerDistributionService(
        IParametersService parametersService,
        ICheckerTemplateLocator templateLocator,
        ISoftwareUpdatesManagerService softwareUpdates,
        ILogger<CheckerDistributionService> logger)
    {
        _parametersService = parametersService;
        _templateLocator = templateLocator;
        _softwareUpdates = softwareUpdates;
        _logger = logger;
    }

    public async Task<Result<CheckerDistributionDownload>> Build(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Result.Failure<CheckerDistributionDownload>("Не указан токен инстанса");

        var parameters = await _parametersService.Current();
        var address = parameters.ServerSettings.PublicAddress?.Trim() ?? string.Empty;
        if (address.Length == 0)
            return Result.Failure<CheckerDistributionDownload>("Не задан публичный адрес сервера");

        var templateZip = await EnsureTemplateZip();
        if (templateZip.IsFailure)
            return Result.Failure<CheckerDistributionDownload>(templateZip.Error);

        var stream = new MemoryStream();
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
        }

        stream.Position = 0;
        return Result.Success(new CheckerDistributionDownload
        {
            Content = stream,
            FileName = "fmu-api.zip"
        });
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
}
