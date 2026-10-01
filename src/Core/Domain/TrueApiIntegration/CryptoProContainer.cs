namespace Domain.TrueApiIntegration;

public sealed class CryptoProContainer
{
    private readonly Dictionary<string, byte[]> _files;

    public CryptoProContainer(string name, Dictionary<string, byte[]> files)
    {
        Name = name;
        _files = files;
    }

    public string Name { get; }

    public IReadOnlyCollection<string> FileNames => _files.Keys;

    public void WriteTo(string directory)
    {
        Directory.CreateDirectory(directory);
        var root = Path.GetFullPath(directory);

        foreach (var file in _files)
        {
            var path = Path.GetFullPath(Path.Combine(root, file.Key));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Файл {file.Key} выходит за пределы контейнера");

            File.WriteAllBytes(path, file.Value);
        }
    }
}
