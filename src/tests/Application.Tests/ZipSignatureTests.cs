using Shared.Files;

namespace Application.Tests;

public class ZipSignatureTests
{
    /// <summary>
    /// Локальный заголовок ZIP (PK\\x03\\x04).
    /// </summary>
    [Fact]
    public void Matches_принимает_локальный_заголовок()
    {
        Assert.True(ZipSignature.Matches([0x50, 0x4B, 0x03, 0x04]));
    }

    /// <summary>
    /// Пустой архив (PK\\x05\\x06).
    /// </summary>
    [Fact]
    public void Matches_принимает_пустой_архив()
    {
        Assert.True(ZipSignature.Matches([0x50, 0x4B, 0x05, 0x06]));
    }

    /// <summary>
    /// EXE, PNG и короткий буфер — не ZIP.
    /// </summary>
    [Theory]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 })]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData(new byte[] { 0x50, 0x4B })]
    [InlineData(new byte[] { })]
    public void Matches_отклоняет_чужой_заголовок(byte[] header)
    {
        Assert.False(ZipSignature.Matches(header));
    }
}
