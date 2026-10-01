using System.IO.Compression;
using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class CryptoProContainerArchiveTests
{
    /// <summary>
    /// Имя папки в zip — имя контейнера, его читает КриптоПро.
    /// </summary>
    [Fact]
    public void Open_архив_контейнера_возвращает_имя_папки_и_файлы_ключей()
    {
        using var zip = Zip("2560", KeyFiles());

        var result = CryptoProContainerArchive.Open(zip);

        Assert.True(result.IsSuccess);
        Assert.Equal("2560", result.Value.Name);
        Assert.Equal(
            ["header.key", "masks.key", "masks2.key", "name.key", "primary.key", "primary2.key"],
            result.Value.FileNames.OrderBy(name => name));
    }

    /// <summary>
    /// Имя папки попадает в путь на диске, «..» вынесет ключи из каталога КриптоПро.
    /// </summary>
    [Fact]
    public void Open_имя_папки_с_выходом_наверх_отклоняется()
    {
        using var zip = Zip("..", KeyFiles());

        var result = CryptoProContainerArchive.Open(zip);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Open_без_обязательного_файла_ключа_отклоняется()
    {
        var files = KeyFiles();
        files.Remove("header.key");
        using var zip = Zip("2560", files);

        var result = CryptoProContainerArchive.Open(zip);

        Assert.True(result.IsFailure);
    }

    /// <summary>
    /// КриптоПро ищет файлы в нижнем регистре, zip с Windows может отдать HEADER.KEY.
    /// </summary>
    [Fact]
    public void Open_приводит_имена_файлов_ключей_к_нижнему_регистру()
    {
        var files = new Dictionary<string, byte[]>
        {
            ["HEADER.KEY"] = [1],
            ["MASKS.KEY"] = [2],
            ["MASKS2.KEY"] = [3],
            ["NAME.KEY"] = [4],
            ["PRIMARY.KEY"] = [5],
            ["PRIMARY2.KEY"] = [6]
        };
        using var zip = Zip("2560", files);

        var result = CryptoProContainerArchive.Open(zip);

        Assert.True(result.IsSuccess);
        Assert.Contains("header.key", result.Value.FileNames);
    }

    /// <summary>
    /// csptest печатает Match: HDIMAGE\имя\ на Windows и HDIMAGE/имя/ на Linux.
    /// </summary>
    [Fact]
    public void LinksContainer_узнаёт_контейнер_в_выводе_absorb()
    {
        Assert.True(CryptoProAbsorb.LinksContainer("Match: HDIMAGE\\\\2560\\B9AC\r\nOK.", "2560"));
        Assert.True(CryptoProAbsorb.LinksContainer(@"Match: HDIMAGE\2560\B9AC", "2560"));
        Assert.True(CryptoProAbsorb.LinksContainer("Match: HDIMAGE/2560/B9AC\nOK.", "2560"));
        Assert.False(CryptoProAbsorb.LinksContainer("No cert for AT_KEYEXCHANGE key\r\nOK.", "2560"));
    }

    [Fact]
    public void WriteTo_кладёт_файлы_ключей_в_каталог_контейнера()
    {
        using var zip = Zip("2560", KeyFiles());
        var container = CryptoProContainerArchive.Open(zip).Value;
        var directory = Path.Combine(Path.GetTempPath(), "fmu-container-" + Guid.NewGuid().ToString("N"));

        try
        {
            container.WriteTo(directory);

            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(Path.Combine(directory, "header.key")));
            Assert.Equal(new byte[] { 6 }, File.ReadAllBytes(Path.Combine(directory, "primary2.key")));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static MemoryStream Zip(string folder, IReadOnlyDictionary<string, byte[]> files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
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
}
