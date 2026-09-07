using System.Security.Cryptography;
using Application.SoftwareUpdates.Services;
using CSharpFunctionalExtensions;
using Domain.Dto.Responces;
using Domain.Entitys.Interfaces;
using Domain.Entitys.SoftwareUpdateFiles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests;

public class SoftwareUpdatesManagerServiceTests
{
    /// <summary>
    /// EXE с верным SHA256 не прикрепляется.
    /// </summary>
    [Fact]
    public async Task AttachFile_отклоняет_не_zip()
    {
        var content = new byte[] { 0x4D, 0x5A, 0x90, 0x00 };
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var repository = new FakeUpdatesRepository
        {
            Entity = new SoftwareUpdateFilesEntity { Id = "u1", Sha256 = sha }
        };
        var sut = new SoftwareUpdatesManagerService(
            NullLogger<SoftwareUpdatesManagerService>.Instance,
            repository);

        var result = await sut.AttachFile("u1", new FakeFormFile("update.exe", content));

        Assert.True(result.IsFailure);
        Assert.Contains("ZIP", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(repository.AttachCalled);
    }

    private sealed class FakeUpdatesRepository : ISoftwareUpdatesRepository
    {
        public SoftwareUpdateFilesEntity Entity { get; set; } = new();
        public bool AttachCalled { get; private set; }

        public Task<Result<PaginatedResponse<SoftwareUpdateFilesEntity>>> List(int pageNumber, int pageSize)
            => throw new NotSupportedException();

        public Task<Result<SoftwareUpdateFilesEntity>> Create(SoftwareUpdateFilesEntity entity)
            => throw new NotSupportedException();

        public Task<Result<bool>> Delete(string id) => Task.FromResult(Result.Success(true));

        public Task<Result<bool>> AttachFile(string entityId, string filePath, string contentType)
        {
            AttachCalled = true;
            return Task.FromResult(Result.Success(true));
        }

        public Task<Result<SoftwareUpdateFilesEntity>> ById(string id)
            => Task.FromResult(Result.Success(Entity));

        public Task<Result<SoftwareUpdateFilesEntity>> MaxUpdateEntity(string os, string architecture, int version, int assembly)
            => throw new NotSupportedException();

        public Task<Result<SoftwareUpdateFileDownload>> FmuApiUpdate(string updateId, long? rangeFrom)
            => throw new NotSupportedException();
    }

    private sealed class FakeFormFile(string fileName, byte[] content) : IFormFile
    {
        public string ContentType => "application/octet-stream";
        public string ContentDisposition => string.Empty;
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length => content.Length;
        public string Name => "file";
        public string FileName { get; } = fileName;

        public void CopyTo(Stream target) => target.Write(content);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            => target.WriteAsync(content, cancellationToken).AsTask();

        public Stream OpenReadStream() => new MemoryStream(content, writable: false);
    }
}
