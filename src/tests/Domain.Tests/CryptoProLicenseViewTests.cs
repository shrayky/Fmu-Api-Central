using Domain.TrueApiIntegration;

namespace Domain.Tests;

public class CryptoProLicenseViewTests
{
    [Fact]
    public void Parse_берет_дату_окончания()
    {
        var parsed = CryptoProLicenseView.Parse(
            """
            License validity:
            ABCDE-12345-FGHIJ-67890-KLMNO
            Expires: 18.11.2026
            """);

        Assert.False(parsed.Permanent);
        Assert.Equal(new DateOnly(2026, 11, 18), parsed.ExpiresAt);
    }

    /// <summary>
    /// Бессрочная лицензия не должна попадать в оповещение об истечении.
    /// </summary>
    [Fact]
    public void Parse_бессрочная_без_даты()
    {
        var parsed = CryptoProLicenseView.Parse("License type: Client, Permanent");

        Assert.True(parsed.Permanent);
        Assert.Null(parsed.ExpiresAt);
    }

    [Fact]
    public void Parse_iso_дату_и_unlimited()
    {
        var iso = CryptoProLicenseView.Parse("Expiration date: 2026-11-18");
        Assert.Equal(new DateOnly(2026, 11, 18), iso.ExpiresAt);

        var unlimited = CryptoProLicenseView.Parse("Expires: Unlimited");
        Assert.True(unlimited.Permanent);
        Assert.Null(unlimited.ExpiresAt);

        var slash = CryptoProLicenseView.Parse("Срок действия: 18/11/2026");
        Assert.Equal(new DateOnly(2026, 11, 18), slash.ExpiresAt);

        var russian = CryptoProLicenseView.Parse("Лицензия бессрочная");
        Assert.True(russian.Permanent);

        var unknown = CryptoProLicenseView.Parse("ABCDE-12345-FGHIJ-67890-KLMNO");
        Assert.False(unknown.Permanent);
        Assert.Null(unknown.ExpiresAt);
    }

    /// <summary>
    /// Linux cpconfig печатает дни, а не дату: Expires: 78 day(s).
    /// </summary>
    [Fact]
    public void Parse_дни_из_expires_day()
    {
        var parsed = CryptoProLicenseView.Parse(
            """
            License validity:
            DEMO000000000000000000000
            Expires: 78 day(s)
            License type: Demo.
            """,
            new DateOnly(2026, 9, 30));

        Assert.False(parsed.Permanent);
        Assert.Equal(new DateOnly(2026, 12, 17), parsed.ExpiresAt);
    }
}
