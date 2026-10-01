using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration;

public static class CryptoProContainerInstall
{
    public static Result Replace(string storageDirectory, CryptoProContainer container, Func<string, Result> commit)
    {
        Directory.CreateDirectory(storageDirectory);
        var destination = Path.Combine(storageDirectory, container.Name);
        // Каталог ключей КриптоПро сканирует целиком, backup внутри него попадёт в -pattern.
        string? backupDirectory = null;

        try
        {
            if (Directory.Exists(destination))
            {
                backupDirectory = storageDirectory + ".fmu-bak-" + Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(backupDirectory);
                Directory.Move(destination, Path.Combine(backupDirectory, container.Name));
            }

            container.WriteTo(destination);
            var committed = commit(destination);
            if (committed.IsFailure)
            {
                Restore(destination, backupDirectory, container.Name);
                return committed;
            }
        }
        catch (Exception ex)
        {
            Restore(destination, backupDirectory, container.Name);
            return Result.Failure($"Не удалось установить сертификат: {ex.Message}");
        }

        RemoveDirectory(backupDirectory);
        return Result.Success();
    }

    private static void Restore(string destination, string? backupDirectory, string containerName)
    {
        if (backupDirectory == null)
        {
            if (Directory.Exists(destination))
                Directory.Delete(destination, recursive: true);

            return;
        }

        var backup = Path.Combine(backupDirectory, containerName);
        if (!Directory.Exists(backup))
        {
            RemoveDirectory(backupDirectory);
            return;
        }

        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        Directory.Move(backup, destination);
        RemoveDirectory(backupDirectory);
    }

    private static void RemoveDirectory(string? directory)
    {
        if (directory != null && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
