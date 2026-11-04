using Domain.Configuration;
using Domain.Entitys.Organization;
using Domain.Vetis;

namespace Domain.Tests;

public class VetisConnectionTests
{
    /// <summary>
    /// На странице компонента действующий шлюз — 2.1. 3.0 там опытная версия.
    /// </summary>
    [Fact]
    public void Боевой_контур_это_сервис_2_1()
    {
        Assert.Equal(
            "https://api.vetrf.ru/platform/services/2.1/ApplicationManagementService",
            VetisEndpoints.ProductionApplicationManagement);
        Assert.Equal("mercury-g2b.service:2.1", VetisEndpoints.ServiceId);
    }

    /// <summary>
    /// Пустой адрес в настройках означает боевой ApplicationManagementService.
    /// </summary>
    [Fact]
    public void Resolve_пустой_адрес_ведёт_на_боевой_контур()
    {
        Assert.Equal(VetisEndpoints.ProductionApplicationManagement, VetisEndpoints.Resolve(null));
        Assert.Equal(VetisEndpoints.ProductionApplicationManagement, VetisEndpoints.Resolve("  "));
    }

    /// <summary>
    /// В настройке лежит площадка. Путь сервиса 2.1 к ней добавляет код.
    /// </summary>
    [Fact]
    public void Resolve_к_площадке_добавляет_сервис_заявок_2_1()
    {
        Assert.Equal(
            "https://api2.vetrf.ru:8002/platform/services/2.1/ApplicationManagementService",
            VetisEndpoints.Resolve(" https://api2.vetrf.ru:8002 "));
        Assert.Equal(
            "https://api2.vetrf.ru:8002/platform/services/2.1/EnterpriseService",
            VetisEndpoints.EnterpriseService("https://api2.vetrf.ru:8002"));
    }

    /// <summary>
    /// Уже сохранённый полный адрес сворачивается до схемы, хоста и порта.
    /// </summary>
    [Fact]
    public void Origin_полный_адрес_сворачивается_до_площадки()
    {
        Assert.Equal("https://api.vetrf.ru", VetisEndpoints.Origin(null));
        Assert.Equal("https://api.vetrf.ru", VetisEndpoints.Origin("  "));
        Assert.Equal(
            "https://api.vetrf.ru",
            VetisEndpoints.Origin("https://api.vetrf.ru/platform/services/3.0/ApplicationManagementService"));
        Assert.Equal(
            "https://api2.vetrf.ru:8002",
            VetisEndpoints.Origin(" https://api2.vetrf.ru:8002/platform/services/2.1/ApplicationManagementService "));
    }

    /// <summary>
    /// Уже сохранённый адрес 3.0 не должен оставлять вызовы на опытной версии.
    /// </summary>
    [Fact]
    public void Resolve_сохранённый_адрес_3_0_ведёт_на_2_1()
    {
        Assert.Equal(
            "https://api.vetrf.ru/platform/services/2.1/ApplicationManagementService",
            VetisEndpoints.Resolve("https://api.vetrf.ru/platform/services/3.0/ApplicationManagementService"));
        Assert.Equal(
            "https://api2.vetrf.ru:8002/platform/services/2.1/ApplicationManagementService",
            VetisEndpoints.Resolve(" https://api2.vetrf.ru:8002/platform/services/3.0/ApplicationManagementService "));
    }

    /// <summary>
    /// Пустая строка допустима. Непустой адрес — только http или https.
    /// </summary>
    [Theory]
    [InlineData("", true)]
    [InlineData("https://api.vetrf.ru/platform/services/3.0/ApplicationManagementService", true)]
    [InlineData("ftp://api.vetrf.ru/platform", false)]
    [InlineData("api.vetrf.ru", false)]
    public void Accept_проверяет_схему_адреса(string endpoint, bool acceptable)
    {
        Assert.Equal(acceptable, VetisEndpoints.Accept(endpoint));
    }

    /// <summary>
    /// IssuerID в письме стоит на следующей строке, имя в скобках в идентификатор не входит.
    /// </summary>
    [Fact]
    public void Parse_берёт_реквизиты_из_письма()
    {
        var letter = """
            Здравствуйте!
            Login: user-260901
            Password: secret-value
            APIKey: KEY-TOKEN
            IssuerID:
            aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee (ХОЗЯЙСТВО)
            Письмо сгенерировано автоматически.
            """;

        var parsed = VetisAccessLetter.Parse(letter);

        Assert.Equal("user-260901", parsed.Login);
        Assert.Equal("secret-value", parsed.Password);
        Assert.Equal("KEY-TOKEN", parsed.ApiKey);
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", parsed.IssuerId);
    }

    /// <summary>
    /// Нет нужной строки — поле пустое, остальные строки письма не подставляются.
    /// </summary>
    [Fact]
    public void Parse_не_заполняет_отсутствующее_поле()
    {
        var parsed = VetisAccessLetter.Parse("Login: only-user\nТекст письма без пароля.");

        Assert.Equal("only-user", parsed.Login);
        Assert.Equal("", parsed.Password);
        Assert.Equal("", parsed.ApiKey);
        Assert.Equal("", parsed.IssuerId);
    }

    /// <summary>
    /// Пустой API-ключ и маска не затирают уже сохранённый. Новый ключ записывается.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(SecretMask.Placeholder)]
    public void Apply_не_стирает_api_ключ(string incomingKey)
    {
        var current = new OrganizationVetisSettings { ApiKey = "stored-key" };
        var update = new OrganizationVetisSettings { ApiKey = incomingKey };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("stored-key", current.ApiKey);
    }

    /// <summary>
    /// Пустой пароль и маска не затирают уже сохранённый.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(SecretMask.Placeholder)]
    public void Apply_не_стирает_пароль(string incomingPassword)
    {
        var current = new OrganizationVetisSettings { BasicPassword = "stored-secret" };
        var update = new OrganizationVetisSettings { BasicPassword = incomingPassword };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("stored-secret", current.BasicPassword);
    }

    /// <summary>
    /// Признак «Используется» хранится у организации, как у маркировки.
    /// </summary>
    [Fact]
    public void Apply_сохраняет_признак_используется()
    {
        var current = new OrganizationVetisSettings();
        OrganizationVetisScope.Apply(current, new OrganizationVetisSettings { Enable = true, ApiKey = "key" });
        Assert.True(current.Enable);

        OrganizationVetisScope.Apply(current, new OrganizationVetisSettings { Enable = false, ApiKey = "key" });
        Assert.False(current.Enable);
    }

    /// <summary>
    /// Новый пароль записывается, снятая галка площадку не удаляет.
    /// </summary>
    [Fact]
    public void Apply_сохраняет_снятую_площадку_и_новый_пароль()
    {
        var current = new OrganizationVetisSettings { BasicPassword = "old" };
        var update = new OrganizationVetisSettings
        {
            BasicPassword = "new-secret",
            Enterprises =
            [
                new OrganizationEnterprise
                {
                    Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    Name = "Склад",
                    Participates = false
                }
            ]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("new-secret", current.BasicPassword);
        var enterprise = Assert.Single(current.Enterprises);
        Assert.False(enterprise.Participates);
        Assert.Equal("Склад", enterprise.Presentation);
    }

    /// <summary>
    /// Повтор guid отбрасывается. Одинаковые имена различаются адресом.
    /// </summary>
    [Fact]
    public void Apply_различает_площадки_с_одним_именем()
    {
        var current = new OrganizationVetisSettings();
        var update = new OrganizationVetisSettings
        {
            Enterprises =
            [
                new OrganizationEnterprise { Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", Name = "Склад", Address = "ул. Ленина, 12" },
                new OrganizationEnterprise { Guid = "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA", Name = "Другой" },
                new OrganizationEnterprise { Guid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", Name = "Склад", Address = "ул. Кирова, 4" }
            ]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal(2, current.Enterprises.Count);
        Assert.Equal("ул. Ленина, 12", current.Enterprises[0].Presentation);
        Assert.Equal("ул. Кирова, 4", current.Enterprises[1].Presentation);
    }

    /// <summary>
    /// addressView Ветис начинается со страны и города. В списке нужна улица с домом.
    /// Уже записанное длинное представление пересобирается, вручную введённое остаётся.
    /// </summary>
    [Fact]
    public void Apply_представление_берёт_улицу_и_дом()
    {
        var current = new OrganizationVetisSettings();
        var update = new OrganizationVetisSettings
        {
            Enterprises =
            [
                new OrganizationEnterprise
                {
                    Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    Name = "Колбасный дворик",
                    Address = "Российская Федерация, Красноярский край, г. Красноярск, Металлургов пр-кт, д. 32"
                },
                new OrganizationEnterprise
                {
                    Guid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                    Name = "ИП СИДИЧЕНКО ПАВЕЛ СЕРГЕЕВИЧ",
                    Address = "Российская Федерация, Красноярский край, г. Красноярск, Щорса ул., д. 76",
                    Presentation = "ИП СИДИЧЕНКО ПАВЕЛ СЕРГЕЕВИЧ, Российская Федерация, Красноярский край, г. Красноярск, Щорса ул., д. 76"
                },
                new OrganizationEnterprise
                {
                    Guid = "cccccccc-cccc-cccc-cccc-cccccccccccc",
                    Name = "ИП СИДИЧЕНКО ПАВЕЛ СЕРГЕЕВИЧ",
                    Address = "Российская Федерация, Красноярский край, г. Красноярск, 9 Мая ул., д. 36А"
                },
                new OrganizationEnterprise
                {
                    Guid = "dddddddd-dddd-dddd-dddd-dddddddddddd",
                    Name = "ИП СИДИЧЕНКО ПАВЕЛ СЕРГЕЕВИЧ",
                    Address = "Российская Федерация, Красноярский край, г. Красноярск, Ладо Кецховели ул., зд. 54Г"
                },
                new OrganizationEnterprise
                {
                    Guid = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
                    Name = "Склад",
                    Address = "ул. Ленина, 12",
                    Presentation = "Север"
                }
            ]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("Металлургов пр-кт, д. 32", current.Enterprises[0].Presentation);
        Assert.Equal("Щорса ул., д. 76", current.Enterprises[1].Presentation);
        Assert.Equal("9 Мая ул., д. 36А", current.Enterprises[2].Presentation);
        Assert.Equal("Ладо Кецховели ул., зд. 54Г", current.Enterprises[3].Presentation);
        Assert.Equal("Север", current.Enterprises[4].Presentation);
    }

    /// <summary>
    /// Включённая площадка без своей даты получает общую дату. Заполненная дата не переписывается.
    /// </summary>
    [Fact]
    public void Apply_подставляет_общую_дату_только_в_пустую_включённую()
    {
        var current = new OrganizationVetisSettings();
        var update = new OrganizationVetisSettings
        {
            IncomingFrom = "01.10.2026",
            Enterprises =
            [
                new OrganizationEnterprise
                {
                    Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    Name = "Склад",
                    Participates = true
                },
                new OrganizationEnterprise
                {
                    Guid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
                    Name = "Магазин",
                    Participates = true,
                    IncomingFrom = "2026-09-01"
                },
                new OrganizationEnterprise
                {
                    Guid = "cccccccc-cccc-cccc-cccc-cccccccccccc",
                    Name = "Офис",
                    Participates = false
                }
            ]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("2026-10-01", current.IncomingFrom);
        Assert.Equal("2026-10-01", current.Enterprises[0].IncomingFrom);
        Assert.Equal("2026-09-01", current.Enterprises[1].IncomingFrom);
        Assert.Equal("", current.Enterprises[2].IncomingFrom);
    }

    /// <summary>
    /// Уже заданное представление не заменяется вычисленным.
    /// </summary>
    [Fact]
    public void Apply_не_затирает_введённое_представление()
    {
        var current = new OrganizationVetisSettings();
        var update = new OrganizationVetisSettings
        {
            Enterprises =
            [
                new OrganizationEnterprise
                {
                    Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                    Name = "Склад",
                    Address = "ул. Ленина, 12",
                    Presentation = "Север"
                }
            ]
        };

        OrganizationVetisScope.Apply(current, update);

        Assert.Equal("Север", Assert.Single(current.Enterprises).Presentation);
    }

    /// <summary>
    /// Свежий список Ветис не снимает галку и не стирает дату уже записанной площадки.
    /// Площадка, которой больше нет в ответе, остаётся.
    /// </summary>
    [Fact]
    public void Merge_сохраняет_выбор_и_добавляет_новую_площадку()
    {
        var stored = new List<OrganizationEnterprise>
        {
            new()
            {
                Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                Name = "Склад",
                Address = "ул. Ленина, 12",
                Presentation = "Север",
                IncomingFrom = "2026-09-01",
                Participates = true
            },
            new()
            {
                Guid = "cccccccc-cccc-cccc-cccc-cccccccccccc",
                Name = "Офис",
                Participates = false
            }
        };
        var fresh = new List<VetisEnterprise>
        {
            new() { Guid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", Name = "Склад", Address = "ул. Ленина, 12" },
            new() { Guid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", Name = "Магазин", Address = "ул. Кирова, 4" }
        };

        var merged = OrganizationVetisScope.Merge(stored, fresh);

        Assert.Equal(3, merged.Count);
        Assert.Equal("Север", merged[0].Presentation);
        Assert.Equal("2026-09-01", merged[0].IncomingFrom);
        Assert.True(merged[0].Participates);
        Assert.Equal("ул. Кирова, 4", merged[1].Presentation);
        Assert.False(merged[1].Participates);
        Assert.Equal("Офис", merged[2].Presentation);
    }
}
