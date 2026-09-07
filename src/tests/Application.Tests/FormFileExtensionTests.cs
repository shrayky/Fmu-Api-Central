using Microsoft.AspNetCore.Http;
using Shared.Extensions;

namespace Application.Tests;

public class FormFileExtensionTests
{
    /// <summary>
    /// На диск пишется guid.zip, имя клиента не попадает в путь.
    /// </summary>
    [Fact]
    public async Task SaveToTempAsync_пишет_guid_zip()
    {
        var file = new FakeFormFile("..\\..\\windows\\evil.exe", [0x50, 0x4B, 0x03, 0x04, 0x00]);
        string? path = null;

        try
        {
            path = await file.SaveToTempAsync("fmu-central-uploads");

            var name = Path.GetFileName(path);
            Assert.Matches("^[0-9a-f]{32}\\.zip$", name);
            Assert.DoesNotContain("evil", path, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(
                Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fmu-central-uploads")),
                Path.GetDirectoryName(Path.GetFullPath(path)));
        }
        finally
        {
            if (path != null && File.Exists(path))
                File.Delete(path);
        }
    }

    /// <summary>
    /// Сигнатура PK — zip, MZ — нет.
    /// </summary>
    [Fact]
    public async Task IsZip_отличает_zip_от_чужого()
    {
        var zip = new FakeFormFile("a.zip", [0x50, 0x4B, 0x03, 0x04, 0x00]);
        var exe = new FakeFormFile("a.exe", [0x4D, 0x5A, 0x90, 0x00]);

        Assert.True(await zip.IsZipAsync());
        Assert.False(await exe.IsZipAsync());
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
