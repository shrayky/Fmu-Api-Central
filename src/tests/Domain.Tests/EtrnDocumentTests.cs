using Domain.Etrn;
using Domain.Entitys.Organization;

namespace Domain.Tests;

public class EtrnOperatorTests
{
    [Fact]
    public void Пустое_имя_подключения_заменяется_подписью_типа()
    {
        var connection = new EtrnConnection { Id = "c1", Type = EtrnOperatorType.Saby, Name = "  " };

        var op = EtrnOperator.For(connection);

        Assert.Equal("c1", op.ConnectionId);
        Assert.Equal(EtrnOperatorType.Saby, op.Type);
        Assert.Equal("Saby", op.DisplayName);
    }

    [Fact]
    public void Имя_подключения_сохраняется_в_снимке_оператора()
    {
        var connection = new EtrnConnection { Id = "c1", Type = EtrnOperatorType.Saby, Name = "СБИС Север" };

        var op = EtrnOperator.For(connection);

        Assert.Equal("СБИС Север", op.DisplayName);
        Assert.Equal("СБИС Север", op.Name);
    }

    [Fact]
    public void Подпись_типа_для_saby()
    {
        Assert.Equal("Saby", EtrnOperatorType.Label("SABY"));
        Assert.Equal("Другой", EtrnOperatorType.Label("Другой"));
    }

    [Fact]
    public void Подпись_типа_для_Платформы_ЭТрН()
    {
        Assert.Equal("Платформа ЭТрН", EtrnOperatorType.Label("ofd"));
        Assert.Equal("Платформа ЭТрН", EtrnOperatorType.Label("OFD"));
    }

    [Fact]
    public void Адрес_API_определяет_хост_токена_по_сертификату()
    {
        var empty = new EtrnConnection();
        Assert.Equal("", empty.ResolvedEdoUrl());
        Assert.Equal("Не задан адрес API Платформы ЭТрН", empty.MissingOfdAddress());

        var unknown = new EtrnConnection { ApiUrl = "https://api.example/" };
        Assert.Equal("https://api.example", unknown.ResolvedApiUrl());
        Assert.Equal("", unknown.ResolvedEdoUrl());
        Assert.Equal("Для этого адреса API нет хоста токена по сертификату", unknown.MissingOfdAddress());

        Assert.Equal(
            "",
            new EtrnConnection { ApiUrl = "https://api-test.platformaofd.ru" }.ResolvedEdoUrl());
        Assert.Equal(
            "https://edo.platformaofd.ru",
            new EtrnConnection { ApiUrl = "https://epd.platformaofd.ru/" }.ResolvedEdoUrl());
        Assert.Equal(
            "https://edo.platformaofd.ru",
            new EtrnConnection { ApiUrl = "https://api.platformaofd.ru" }.ResolvedEdoUrl());
    }
}

public class EtrnDocumentStatusTests
{
    [Theory]
    [InlineData(EtrnDocumentStatus.Waiting, "", "Ждёт приёмки")]
    [InlineData(EtrnDocumentStatus.Sending, "", "Отправляется")]
    [InlineData(EtrnDocumentStatus.Accepted, EtrnAcceptanceAction.Received, "Получен")]
    [InlineData(EtrnDocumentStatus.Accepted, EtrnAcceptanceAction.Partially, "Получен частично")]
    [InlineData(EtrnDocumentStatus.Rejected, EtrnAcceptanceAction.Rejected, "Не принят")]
    [InlineData(EtrnDocumentStatus.Rejected, "", "Не принят")]
    [InlineData(EtrnDocumentStatus.Closed, "", "Закрыт")]
    [InlineData(EtrnDocumentStatus.Foreign, "", "Другой оператор")]
    public void Подписи_статусов(string status, string action, string label)
    {
        Assert.Equal(label, EtrnDocumentStatus.Label(status, action));
    }

    [Theory]
    [InlineData(EtrnDocumentStatus.Accepted)]
    [InlineData(EtrnDocumentStatus.Rejected)]
    [InlineData(EtrnDocumentStatus.Closed)]
    public void Конечные_статусы(string status)
    {
        Assert.True(EtrnDocumentStatus.IsFinal(status));
        Assert.False(EtrnDocumentStatus.CanAccept(status));
    }

    [Fact]
    public void Ждущий_документ_принимает_команду()
    {
        Assert.True(EtrnDocumentStatus.CanAccept(EtrnDocumentStatus.Waiting));
        Assert.False(EtrnDocumentStatus.CanAccept(EtrnDocumentStatus.Sending));
    }
}

public class EtrnConsigneeBoxTests
{
    private const string Ours = "2BE-ours";

    private const string OurFile =
        "ON_TRNACLGROT_2BM-sender_2BE-ours_2BM-shipper_0_20261110_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.xml";

    private const string ForeignFile =
        "ON_TRNACLGROT_2BE-side_2PS-potok_2BE-other_0_20261016_aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    [Fact]
    public void Второй_идентификатор_титула_это_ящик_грузополучателя()
    {
        Assert.Equal(Ours, EtrnConsigneeBox.Of(OurFile));
        Assert.Equal("2PS-potok", EtrnConsigneeBox.Of(ForeignFile));
        Assert.False(EtrnConsigneeBox.IsForeign(OurFile, Ours));
        Assert.True(EtrnConsigneeBox.IsForeign(ForeignFile, Ours));
        Assert.False(EtrnConsigneeBox.IsForeign(ForeignFile, "  "));
        Assert.False(EtrnConsigneeBox.IsForeign("ON_TRNACLGROT_1.xml", Ours));
    }
}

public class EtrnIncomingDocumentTests
{
    [Fact]
    public void Id_складывается_из_организации_подключения_и_документа_оператора()
    {
        Assert.Equal("doc:o1:c1:d-7", EtrnIncomingDocumentEntity.BuildId("o1", "c1", "d-7"));
    }

    [Fact]
    public void Этап_перевозчика_не_подписывается_как_ожидание_приёмки()
    {
        var waiting = new EtrnIncomingDocumentEntity
        {
            Status = EtrnDocumentStatus.Waiting,
            Lines = [new EtrnCargoLine { Index = 1, Name = "Сыр", Places = 2, GrossWeight = "3.79" }]
        };
        var carrier = new EtrnIncomingDocumentEntity
        {
            Status = EtrnDocumentStatus.Waiting,
            LastError = EtrnDocumentState.StageUnavailable("Получение груза"),
            Lines = [new EtrnCargoLine { Index = 1, Name = "Сыр", Places = 2, GrossWeight = "3.79" }]
        };

        Assert.Equal("Ждёт приёмки", waiting.StatusLabel);
        Assert.True(waiting.CanDecide);
        Assert.Equal("Получение груза", carrier.StatusLabel);
        Assert.False(carrier.CanDecide);
    }

    [Fact]
    public void Чужой_ящик_не_ждёт_приёмки()
    {
        var document = new EtrnIncomingDocumentEntity
        {
            Status = EtrnDocumentStatus.Foreign,
            LastError = EtrnDocumentState.ForeignNote("2PS-potok"),
            Lines = [new EtrnCargoLine { Index = 1, Name = "Сыр", Places = 2, GrossWeight = "3" }]
        };

        Assert.Equal("Другой оператор", document.StatusLabel);
        Assert.False(document.CanDecide);
        Assert.False(EtrnDocumentStatus.CanAccept(EtrnDocumentStatus.Foreign));
        Assert.False(EtrnDocumentStatus.IsFinal(EtrnDocumentStatus.Foreign));
    }

    [Fact]
    public void Документ_без_количества_не_принимается()
    {
        var document = new EtrnIncomingDocumentEntity
        {
            Status = EtrnDocumentStatus.Waiting,
            Lines =
            [
                new EtrnCargoLine { Index = 1, Name = "Груз", Places = 1, GrossWeight = "10" },
                new EtrnCargoLine { Index = 2, Name = "Без количества" }
            ]
        };

        Assert.False(document.CanDecide);
        Assert.False(document.HasQuantity);
        Assert.False(new EtrnIncomingDocumentEntity
        {
            Status = EtrnDocumentStatus.Waiting,
            Lines = [new EtrnCargoLine { Index = 1 }]
        }.CanDecide);
    }

    [Fact]
    public void Исправление_доступно_только_подписанной_Платформе()
    {
        var document = new EtrnIncomingDocumentEntity
        {
            Status = EtrnDocumentStatus.Accepted,
            Operator = new EtrnOperator { Type = EtrnOperatorType.Ofd }
        };

        Assert.True(document.CanCorrect);

        document.Status = EtrnDocumentStatus.Rejected;
        Assert.True(document.CanCorrect);

        document.Status = EtrnDocumentStatus.Waiting;
        Assert.False(document.CanCorrect);

        document.Status = EtrnDocumentStatus.Accepted;
        document.Operator.Type = EtrnOperatorType.Saby;
        Assert.False(document.CanCorrect);

        document.Operator.Type = EtrnOperatorType.Ofd;
        document.Status = EtrnDocumentStatus.Closed;
        Assert.False(document.CanCorrect);
    }

    [Theory]
    [InlineData(0, "20.11.2026", EtrnCorrection.BadNumber)]
    [InlineData(1000, "20.11.2026", EtrnCorrection.BadNumber)]
    [InlineData(1, "2026-11-20", EtrnCorrection.BadDate)]
    [InlineData(1, "", EtrnCorrection.BadDate)]
    public void Номер_и_дата_исправления_проверяются(int number, string date, string error)
    {
        var read = EtrnCorrection.Read(number, date);

        Assert.True(read.IsFailure);
        Assert.Equal(error, read.Error);
    }

    [Fact]
    public void Оператор_пишется_в_документ()
    {
        var document = new EtrnIncomingDocumentEntity
        {
            Operator = new EtrnOperator { ConnectionId = "c1", Type = EtrnOperatorType.Saby, Name = "СБИС" }
        };

        Assert.Equal("c1", document.Operator.ConnectionId);
        Assert.Equal("СБИС", document.Operator.DisplayName);
    }

    [Fact]
    public void Организация_хранит_подключения_ЭТрН()
    {
        var organization = new OrganizationEntity
        {
            EtrnConnections = [new EtrnConnection { Id = "c1", Type = EtrnOperatorType.Saby, Name = "СБИС", Enable = true }]
        };

        Assert.True(Assert.Single(organization.EtrnConnections).Enable);
    }
}
