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

public class EtrnIncomingDocumentTests
{
    [Fact]
    public void Id_складывается_из_организации_подключения_и_документа_оператора()
    {
        Assert.Equal("doc:o1:c1:d-7", EtrnIncomingDocumentEntity.BuildId("o1", "c1", "d-7"));
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
