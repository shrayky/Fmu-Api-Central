using Domain.Vetis;

namespace Domain.Tests;

public class VetisExpiryTests
{
    [Fact]
    public void Срок_годности_берётся_из_партии_а_не_из_даты_выработки()
    {
        var xml = """
            <vetDocument>
              <certifiedConsignment>
                <batch>
                  <dateOfProduction><firstDate><year>2020</year><month>1</month><day>1</day></firstDate></dateOfProduction>
                  <expiryDate>
                    <firstDate><year>2026</year><month>11</month><day>20</day></firstDate>
                    <informalDate>до конца года</informalDate>
                  </expiryDate>
                </batch>
              </certifiedConsignment>
            </vetDocument>
            """;

        Assert.Equal("20.11.2026", VetisExpiry.Text(xml));
    }

    [Fact]
    public void Из_нескольких_сроков_берётся_самый_ранний()
    {
        var xml = """
            <batch>
              <expiryDate>
                <firstDate><year>2026</year><month>12</month><day>1</day></firstDate>
                <secondDate><year>2026</year><month>11</month><day>3</day></secondDate>
              </expiryDate>
              <expiryDate><firstDate><year>2026</year><month>5</month><day>2</day></firstDate></expiryDate>
            </batch>
            """;

        Assert.Equal("02.05.2026", VetisExpiry.Text(xml));
    }

    [Fact]
    public void Неполный_срок_считается_с_первого_числа()
    {
        var month = """<expiryDate><firstDate><year>2026</year><month>5</month></firstDate></expiryDate>""";
        var year = """<expiryDate><firstDate><year>2026</year></firstDate></expiryDate>""";

        Assert.Equal("01.05.2026", VetisExpiry.Text(month));
        Assert.Equal("01.01.2026", VetisExpiry.Text(year));
    }

    [Fact]
    public void Без_даты_показывается_текст_срока()
    {
        var xml = """<expiryDate><informalDate>до вскрытия</informalDate></expiryDate>""";

        Assert.Equal("до вскрытия", VetisExpiry.Text(xml));
        Assert.Equal("", VetisExpiry.Earliest([xml]));
    }

    [Fact]
    public void В_документе_минимум_по_структурным_датам()
    {
        var early = """<expiryDate><firstDate><year>2026</year><month>5</month><day>2</day></firstDate></expiryDate>""";
        var late = """<expiryDate><firstDate><year>2026</year><month>11</month><day>20</day></firstDate></expiryDate>""";
        var text = """<expiryDate><informalDate>до вскрытия</informalDate></expiryDate>""";

        Assert.Equal("02.05.2026", VetisExpiry.Earliest([late, text, early]));
        Assert.Equal("", VetisExpiry.Earliest([text, "<vetDocument/>"]));
        Assert.Equal("", VetisExpiry.Text("не xml"));
    }

    [Fact]
    public void Истёкший_срок_раньше_сегодня_сегодняшний_ещё_нет()
    {
        var today = new DateOnly(2026, 11, 18);

        Assert.True(VetisExpiry.Expired("17.11.2026", today));
        Assert.False(VetisExpiry.Expired("18.11.2026", today));
        Assert.False(VetisExpiry.Expired("19.11.2026", today));
        Assert.False(VetisExpiry.Expired("до вскрытия", today));
        Assert.False(VetisExpiry.Expired("", today));
    }
}
