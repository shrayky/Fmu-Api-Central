using CSharpFunctionalExtensions;

namespace Domain.TrueApiIntegration;

public static class CryptoProKeyFile
{
    public static Result<byte[]> Read(Stream source, long declaredLength, int maxBytes)
    {
        if (declaredLength < 0 || declaredLength > maxBytes)
            return Result.Failure<byte[]>("Файл ключа слишком большой");

        var chunk = new byte[81920];
        using var output = new MemoryStream();
        long total = 0;
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            total += read;
            if (total > maxBytes || total > declaredLength)
                return Result.Failure<byte[]>("Файл ключа слишком большой");

            output.Write(chunk, 0, read);
        }

        return Result.Success(output.ToArray());
    }
}
