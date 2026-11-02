using Domain.Vetis;

namespace Domain.Tests;

public class VetisAutoQuenchTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void Берёт_ВСД_у_которого_вышла_задержка_и_пропускает_свежий()
    {
        var vsds = new[]
        {
            Vsd("old", "CONFIRMED", "2026-10-30T08:00:00+07:00"),
            Vsd("fresh", "CONFIRMED", "2026-10-30T11:30:00+07:00"),
            Vsd("used", "UTILIZED", "2026-10-29T08:00:00+07:00"),
            Vsd("broken", "CONFIRMED", "не дата")
        };

        var due = VetisAutoQuench.DueUuids(vsds, delayHours: 2, Now, requireVehicle: false);

        Assert.Equal(["old"], due);
    }

    [Fact]
    public void Без_номера_автомобиля_отсекается_только_при_запрете()
    {
        var vsds = new[]
        {
            Vsd("car", "CONFIRMED", "2026-10-29T08:00:00+07:00", "<vetDocument><vehicleNumber>А000АА77</vehicleNumber></vetDocument>"),
            Vsd("wagon", "CONFIRMED", "2026-10-29T09:00:00+07:00", "<vetDocument><wagonNumber>123</wagonNumber></vetDocument>"),
            Vsd("empty", "CONFIRMED", "2026-10-29T07:00:00+07:00", "<vetDocument><vehicleNumber> </vehicleNumber></vetDocument>")
        };

        Assert.Equal(["empty", "car", "wagon"], VetisAutoQuench.DueUuids(vsds, 1, Now, requireVehicle: false));
        Assert.Equal(["car"], VetisAutoQuench.DueUuids(vsds, 1, Now, requireVehicle: true));
    }

    [Fact]
    public void Документ_без_даты_идёт_после_датированного()
    {
        Assert.True(VetisAutoQuench.CompareDocuments(100, "b", 200, "a") < 0);
        Assert.True(VetisAutoQuench.CompareDocuments(0, "a", 100, "b") > 0);
        Assert.True(VetisAutoQuench.CompareDocuments(100, "a", 100, "b") < 0);
    }

    private static VetisIncomingVsd Vsd(string uuid, string status, string issueDate, string rawXml = "<vetDocument/>") => new()
    {
        Uuid = uuid,
        Status = status,
        IssueDate = issueDate,
        RawXml = rawXml
    };
}
