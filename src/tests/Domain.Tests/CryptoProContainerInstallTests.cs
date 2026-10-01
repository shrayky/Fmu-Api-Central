using CSharpFunctionalExtensions;
using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class CryptoProContainerInstallTests
{
    /// <summary>
    /// absorb смотрит контейнер по имени в каталоге ключей. Старый каталог нельзя удалять,
    /// пока absorb не подтвердил новый: иначе при ошибке ключи пропадают.
    /// </summary>
    [Fact]
    public void Replace_при_ошибке_absorb_оставляет_прежний_контейнер()
    {
        using var workspace = new KeyWorkspace();
        File.WriteAllBytes(workspace.HeaderPath, [9]);
        var container = Container();
        byte[]? writtenBeforeCommit = null;

        var result = CryptoProContainerInstall.Replace(workspace.Storage, container, destination =>
        {
            writtenBeforeCommit = File.ReadAllBytes(Path.Combine(destination, "header.key"));
            return Result.Failure("absorb");
        });

        Assert.True(result.IsFailure);
        Assert.Equal(new byte[] { 1 }, writtenBeforeCommit);
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(workspace.HeaderPath));
        Assert.Single(Directory.GetDirectories(workspace.Storage));
        Assert.DoesNotContain(Directory.GetDirectories(workspace.Root), path => path.Contains(".fmu-bak-", StringComparison.Ordinal));
    }

    [Fact]
    public void Replace_после_успешного_absorb_оставляет_новый_контейнер()
    {
        using var workspace = new KeyWorkspace();
        File.WriteAllBytes(workspace.HeaderPath, [9]);
        var container = Container();

        var result = CryptoProContainerInstall.Replace(workspace.Storage, container, _ => Result.Success());

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(workspace.HeaderPath));
        Assert.Single(Directory.GetDirectories(workspace.Storage));
        Assert.DoesNotContain(Directory.GetDirectories(workspace.Root), path => path.Contains(".fmu-bak-", StringComparison.Ordinal));
    }

    [Fact]
    public void Replace_при_ошибке_первой_установки_убирает_новый_каталог()
    {
        using var workspace = new KeyWorkspace();
        if (Directory.Exists(workspace.Destination))
            Directory.Delete(workspace.Destination);

        var result = CryptoProContainerInstall.Replace(workspace.Storage, Container(), _ => Result.Failure("absorb"));

        Assert.True(result.IsFailure);
        Assert.False(Directory.Exists(workspace.Destination));
    }

    private static CryptoProContainer Container()
    {
        using var zip = Zip("2560", KeyFiles());
        return CryptoProContainerArchive.Open(zip).Value;
    }

    private static MemoryStream Zip(string folder, IReadOnlyDictionary<string, byte[]> files)
    {
        var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry($"{folder}/{file.Key}");
                using var entryStream = entry.Open();
                entryStream.Write(file.Value);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static Dictionary<string, byte[]> KeyFiles() => new()
    {
        ["header.key"] = [1],
        ["masks.key"] = [2],
        ["masks2.key"] = [3],
        ["name.key"] = [4],
        ["primary.key"] = [5],
        ["primary2.key"] = [6]
    };

    private sealed class KeyWorkspace : IDisposable
    {
        public KeyWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "fmu-install-" + Guid.NewGuid().ToString("N"));
            Storage = Path.Combine(Root, "keys");
            Destination = Path.Combine(Storage, "2560");
            Directory.CreateDirectory(Destination);
        }

        public string Root { get; }

        public string Storage { get; }

        public string Destination { get; }

        public string HeaderPath => Path.Combine(Destination, "header.key");

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
