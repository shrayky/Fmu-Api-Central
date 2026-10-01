using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class CryptoProLicenseSerialTests
{
    /// <summary>
    /// Регистр серийника КриптоПро значим, его нельзя приводить к верхнему.
    /// </summary>
    [Fact]
    public void Normalize_сохраняет_регистр_пяти_групп()
    {
        var result = CryptoProLicenseSerial.Normalize("  Ab12C-dE34F-56789-zzzzz-YYYYY  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Ab12C-dE34F-56789-zzzzz-YYYYY", result.Value);
    }

    [Fact]
    public void Normalize_отклоняет_посторонние_символы()
    {
        var result = CryptoProLicenseSerial.Normalize("Ab12C-dE34F-56789-zzzzz-YYY Y");

        Assert.True(result.IsFailure);
    }
}
