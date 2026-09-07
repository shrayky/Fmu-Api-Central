using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Shared.Files;

namespace Shared.Extensions;

public static class FormFileExtension
{
    private static async Task<string> CalculateSha256HashAsync(this IFormFile file)
    {
        using var sha256 = SHA256.Create();
        await using var stream = file.OpenReadStream();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static async Task<bool> IsZipAsync(this IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length < 4)
            return false;

        await using var stream = file.OpenReadStream();
        var header = new byte[4];
        var read = await stream.ReadAsync(header.AsMemory(0, 4), cancellationToken);
        return read == 4 && ZipSignature.Matches(header);
    }

    public static async Task<string> SaveToTempAsync(this IFormFile file, string? subDirectory = null)
    {
        var tempPath = Path.GetTempPath();
        if (!string.IsNullOrEmpty(subDirectory))
        {
            tempPath = Path.Combine(tempPath, subDirectory);
        }

        Directory.CreateDirectory(tempPath);

        var tempFilePath = Path.Combine(tempPath, $"{Guid.NewGuid():N}.zip");

        await using var stream = new FileStream(tempFilePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return tempFilePath;
    }

    public static async Task<bool> VerifyHashAsync(this IFormFile file, string expectedHash)
    {
        var actualHash = await file.CalculateSha256HashAsync();
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
    