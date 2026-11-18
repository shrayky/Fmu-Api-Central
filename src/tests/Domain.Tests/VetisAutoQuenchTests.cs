using Domain.Entitys.Organization;
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
    public void Окно_автогашения_с_девяти_включительно_до_семнадцати()
    {
        var settings = new OrganizationVetisSettings();

        Assert.True(settings.InsideQuenchWindow(new TimeOnly(9, 0)));
        Assert.True(settings.InsideQuenchWindow(new TimeOnly(16, 59)));
        Assert.False(settings.InsideQuenchWindow(new TimeOnly(8, 59)));
        Assert.False(settings.InsideQuenchWindow(new TimeOnly(17, 0)));
    }

    [Fact]
    public void Пустое_окно_автогашения_считается_с_девяти_до_семнадцати()
    {
        var settings = new OrganizationVetisSettings { AutoQuenchFrom = "", AutoQuenchTo = " " };

        Assert.True(settings.InsideQuenchWindow(new TimeOnly(9, 0)));
        Assert.False(settings.InsideQuenchWindow(new TimeOnly(17, 0)));
    }

    [Fact]
    public void Своё_окно_автогашения_режет_время_по_заданным_границам()
    {
        var settings = new OrganizationVetisSettings { AutoQuenchFrom = "10:30", AutoQuenchTo = "11:00" };

        Assert.True(settings.InsideQuenchWindow(new TimeOnly(10, 30)));
        Assert.False(settings.InsideQuenchWindow(new TimeOnly(11, 0)));
    }

    [Fact]
    public void Окно_автогашения_не_принимает_конец_не_позже_начала()
    {
        Assert.True(new OrganizationVetisSettings { AutoQuenchFrom = "17:00", AutoQuenchTo = "09:00" }.CheckWindow().IsFailure);
        Assert.True(new OrganizationVetisSettings { AutoQuenchFrom = "09:00", AutoQuenchTo = "09:00" }.CheckWindow().IsFailure);
        Assert.True(new OrganizationVetisSettings { AutoQuenchFrom = "abc", AutoQuenchTo = "17:00" }.CheckWindow().IsFailure);
        Assert.True(new OrganizationVetisSettings().CheckWindow().IsSuccess);
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
