using System.IO.Compression;
using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration;

public static class CryptoProContainerArchive
{
    private const int MaxKeyFileBytes = 1024 * 1024;

    private static readonly string[] KeyFileNames =
    [
        "header.key",
        "masks.key",
        "masks2.key",
        "name.key",
        "primary.key",
        "primary2.key"
    ];

    public static Result<CryptoProContainer> Open(Stream stream)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return Result.Failure<CryptoProContainer>("Архив сертификата не читается");
        }

        using (archive)
        {
            string? folder = null;
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    continue;

                var parts = entry.FullName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    return Result.Failure<CryptoProContainer>("В архиве должна быть одна папка контейнера");

                if (folder == null)
                    folder = parts[0];
                else if (!string.Equals(folder, parts[0], StringComparison.Ordinal))
                    return Result.Failure<CryptoProContainer>("В архиве должна быть одна папка контейнера");

                if (!IsContainerName(folder))
                    return Result.Failure<CryptoProContainer>("Недопустимое имя папки контейнера");

                var fileName = KeyFileNames.FirstOrDefault(name => name.Equals(parts[1], StringComparison.OrdinalIgnoreCase));
                if (fileName == null)
                    return Result.Failure<CryptoProContainer>($"В контейнере лишний файл {parts[1]}");

                using var entryStream = entry.Open();
                var bytes = CryptoProKeyFile.Read(entryStream, entry.Length, MaxKeyFileBytes);
                if (bytes.IsFailure)
                    return Result.Failure<CryptoProContainer>($"Файл {fileName} слишком большой");

                if (!files.TryAdd(fileName, bytes.Value))
                    return Result.Failure<CryptoProContainer>($"Файл {fileName} повторяется");
            }

            if (folder == null)
                return Result.Failure<CryptoProContainer>("В архиве нет контейнера");

            foreach (var required in KeyFileNames)
            {
                if (!files.ContainsKey(required))
                    return Result.Failure<CryptoProContainer>($"В контейнере нет файла {required}");
            }

            return Result.Success(new CryptoProContainer(folder, files));
        }
    }

    /// <summary>
    /// Имя папки становится каталогом на диске, поэтому «..» и разделители путей нельзя.
    /// </summary>
    private static bool IsContainerName(string name)
    {
        if (name is "." or ".." || name.Length > 64)
            return false;

        foreach (var ch in name)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_' or '-')
                continue;

            return false;
        }

        return name.Length > 0;
    }
}
