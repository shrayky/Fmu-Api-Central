using Application.Instance.Services;
using Application.SoftwareUpdates.Interfaces;
using CSharpFunctionalExtensions;
using Domain.Configuration;
using Domain.Configuration.Interfaces;
using Domain.Dto.Responces;
using Domain.Entitys.Instance.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;
using System.Text.Json;

namespace Application.Tests;

public class CheckerDistributionServiceTests
{
    /// <summary>
    /// Отдаётся пакет как в базе: хост, каталоги checker и config.json рядом с exe.
    /// </summary>
    [Fact]
    public async Task Build_кладёт_config_рядом_с_exe_и_сохраняет_структуру()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "update.zip"), ZipWithFullPackage());
            var parameters = WithAddress();
            var sut = CreateSut(parameters, dir.FullName, new FakeUpdates());

            var result = await sut.Build("inst-token-1");

            Assert.True(result.IsSuccess);
            using var zip = new ZipArchive(result.Value.Content, ZipArchiveMode.Read);
            Assert.NotNull(zip.GetEntry("fmu-api.exe"));
            Assert.NotNull(zip.GetEntry("fmu-api-check/12.1/fmu-api-check.exe"));
            Assert.NotNull(zip.GetEntry("fmu-api-check/12.1/wwwroot/index.js"));
            var json = zip.GetEntry("fmu-api-check/12.1/config.json");
            Assert.NotNull(json);
            await using var jsonStream = json.Open();
            var settings = await JsonSerializer.DeserializeAsync<JsonElement>(jsonStream);
            Assert.Equal("https://central.example:2579", settings.GetProperty("serverAddress").GetString());
            Assert.Equal("inst-token-1", settings.GetProperty("token").GetString());
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>
    /// Json не пишется в шаблон: у каждого скачивания свой токен.
    /// </summary>
    [Fact]
    public async Task Build_каждый_раз_кладёт_токен_скачивания_в_config()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "update.zip"), ZipWithFullPackage());
            var sut = CreateSut(WithAddress(), dir.FullName, new FakeUpdates());

            var first = await TokenFromBuild(sut, "token-a");
            var second = await TokenFromBuild(sut, "token-b");

            Assert.Equal("token-a", first);
            Assert.Equal("token-b", second);
            Assert.False(File.Exists(Path.Combine(dir.FullName, "config.json")));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>
    /// В базе более новая сборка — шаблон zip перекачивается.
    /// </summary>
    [Fact]
    public async Task Build_обновляет_шаблон_если_в_базе_новее()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "update.zip"), ZipWithFullPackage("12.1"));
            await File.WriteAllTextAsync(Path.Combine(dir.FullName, "update.id"), "12_1_x64_windows");

            var updates = new FakeUpdates
            {
                Items =
                [
                    new SoftwareUpdateFilesEntity
                    {
                        Id = "12_1_x64_windows",
                        Version = 12,
                        Assembly = 1,
                        Os = "windows",
                        Architecture = "x64"
                    },
                    new SoftwareUpdateFilesEntity
                    {
                        Id = "12_2_x64_windows",
                        Version = 12,
                        Assembly = 2,
                        Os = "windows",
                        Architecture = "x64"
                    }
                ],
                UpdateZip = ZipWithFullPackage("12.2")
            };

            var sut = CreateSut(WithAddress(), dir.FullName, updates);

            var result = await sut.Build("inst-token-1");

            Assert.True(result.IsSuccess);
            using var zip = new ZipArchive(result.Value.Content, ZipArchiveMode.Read);
            Assert.NotNull(zip.GetEntry("fmu-api-check/12.2/fmu-api-check.exe"));
            Assert.NotNull(zip.GetEntry("fmu-api-check/12.2/config.json"));
            Assert.Null(zip.GetEntry("fmu-api-check/12.1/fmu-api-check.exe"));
            Assert.Equal("12_2_x64_windows", await File.ReadAllTextAsync(Path.Combine(dir.FullName, "update.id")));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    /// <summary>
    /// Нет шаблона — zip обновления из базы копируется в каталог шаблона целиком.
    /// </summary>
    [Fact]
    public async Task Build_скачивает_zip_обновления_если_шаблона_нет()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var package = ZipWithFullPackage();
            var updates = new FakeUpdates
            {
                Items =
                [
                    new SoftwareUpdateFilesEntity
                    {
                        Id = "12_1_x64_windows",
                        Version = 12,
                        Assembly = 1,
                        Os = "windows",
                        Architecture = "x64"
                    }
                ],
                UpdateZip = package
            };

            var sut = CreateSut(WithAddress(), dir.FullName, updates);

            var result = await sut.Build("inst-token-1");

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(Path.Combine(dir.FullName, "update.zip")));
            using var zip = new ZipArchive(result.Value.Content, ZipArchiveMode.Read);
            Assert.NotNull(zip.GetEntry("fmu-api.exe"));
            Assert.NotNull(zip.GetEntry("fmu-api-check/12.1/fmu-api-check.exe"));
            Assert.NotNull(zip.GetEntry("fmu-api-check/12.1/config.json"));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public async Task Build_ошибка_если_нет_адреса()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "update.zip"), ZipWithFullPackage());
            var sut = CreateSut(new FakeParametersService(), dir.FullName, new FakeUpdates());

            var result = await sut.Build("inst-token-1");

            Assert.True(result.IsFailure);
            Assert.Contains("адрес", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public async Task Build_ошибка_если_нет_шаблона_и_нет_обновления()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var sut = CreateSut(WithAddress(), dir.FullName, new FakeUpdates());

            var result = await sut.Build("inst-token-1");

            Assert.True(result.IsFailure);
            Assert.Contains("обновлен", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public async Task Build_ошибка_если_нет_токена()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "update.zip"), ZipWithFullPackage());
            var sut = CreateSut(WithAddress(), dir.FullName, new FakeUpdates());

            var result = await sut.Build("  ");

            Assert.True(result.IsFailure);
            Assert.Contains("токен", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    private static FakeParametersService WithAddress()
    {
        var parameters = new FakeParametersService();
        parameters.CurrentValue.ServerSettings.PublicAddress = "https://central.example:2579";
        return parameters;
    }

    private static CheckerDistributionService CreateSut(
        IParametersService parameters,
        string folder,
        ISoftwareUpdatesManagerService updates)
        => new(parameters, new FakeLocator(folder), updates, NullLogger<CheckerDistributionService>.Instance);

    private static async Task<string?> TokenFromBuild(CheckerDistributionService sut, string token)
    {
        var result = await sut.Build(token);
        Assert.True(result.IsSuccess);
        using var zip = new ZipArchive(result.Value.Content, ZipArchiveMode.Read);
        var json = zip.GetEntry("fmu-api-check/12.1/config.json");
        Assert.NotNull(json);
        await using var jsonStream = json.Open();
        var settings = await JsonSerializer.DeserializeAsync<JsonElement>(jsonStream);
        return settings.GetProperty("token").GetString();
    }

    private static byte[] ZipWithFullPackage(string checkVersion = "12.1")
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var main = zip.CreateEntry("fmu-api.exe").Open())
                main.Write("host"u8.ToArray());
            using (var exe = zip.CreateEntry($"fmu-api-check/{checkVersion}/fmu-api-check.exe").Open())
                exe.Write("check"u8.ToArray());
            using (var www = zip.CreateEntry($"fmu-api-check/{checkVersion}/wwwroot/index.js").Open())
                www.Write("www"u8.ToArray());
        }

        return stream.ToArray();
    }

    private sealed class FakeParametersService : IParametersService
    {
        public Parameters CurrentValue { get; } = new();

        public Task<Parameters> Current() => Task.FromResult(CurrentValue);

        public Task<bool> Update(Parameters parameters) => Task.FromResult(true);
    }

    private sealed class FakeLocator : ICheckerTemplateLocator
    {
        public FakeLocator(string folder) => Folder = folder;

        public string Folder { get; }
    }

    private sealed class FakeUpdates : ISoftwareUpdatesManagerService
    {
        public List<SoftwareUpdateFilesEntity> Items { get; init; } = [];
        public byte[]? UpdateZip { get; init; }

        public Task<Result<PaginatedResponse<SoftwareUpdateFilesEntity>>> List(int pageNumber, int pageSize)
            => Task.FromResult(Result.Success(new PaginatedResponse<SoftwareUpdateFilesEntity>
            {
                Content = Items,
                TotalCount = Items.Count,
                CurrentPage = 1,
                PageSize = pageSize
            }));

        public Task<Result<Stream>> FmuApiUpdateFile(string id)
            => Task.FromResult(UpdateZip == null
                ? Result.Failure<Stream>("нет файла")
                : Result.Success<Stream>(new MemoryStream(UpdateZip)));

        public Task<Result<string>> Create(string data) => throw new NotSupportedException();
        public Task<Result<bool>> Delete(string id) => throw new NotSupportedException();
        public Task<Result<bool>> AttachFile(string id, IFormFile file) => throw new NotSupportedException();
        public Task<(bool, string)> NeedUpdate(string os, string architecture, int version, int assembly)
            => throw new NotSupportedException();
        public Task<Result<SoftwareUpdateFilesEntity>> ById(string id) => throw new NotSupportedException();
        public Task<Result<SoftwareUpdateFileDownload>> FmuApiUpdateData(string os, string architecture, int version, int assembly, long? rangeFrom)
            => throw new NotSupportedException();
        public Task<Result<SoftwareUpdateFileDownload>> FmuApiUpdateById(string updateId, long? rangeFrom)
            => throw new NotSupportedException();
    }
}
