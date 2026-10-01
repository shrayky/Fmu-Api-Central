using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class CryptoProKeyFileTests
{
    /// <summary>
    /// Длина в заголовке zip может быть неизвестна или занижена, поэтому читаем не больше лимита.
    /// </summary>
    [Fact]
    public void Read_отклоняет_поток_длиннее_лимита_даже_если_заголовок_врёт()
    {
        using var source = new MemoryStream(new byte[20]);

        var unknown = CryptoProKeyFile.Read(source, declaredLength: -1, maxBytes: 8);
        Assert.True(unknown.IsFailure);

        source.Position = 0;
        var understated = CryptoProKeyFile.Read(source, declaredLength: 4, maxBytes: 8);
        Assert.True(understated.IsFailure);
    }

    [Fact]
    public void Read_возвращает_байты_если_поток_в_лимите()
    {
        using var source = new MemoryStream([1, 2, 3, 4]);

        var result = CryptoProKeyFile.Read(source, declaredLength: 4, maxBytes: 8);

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Value);
    }
}
