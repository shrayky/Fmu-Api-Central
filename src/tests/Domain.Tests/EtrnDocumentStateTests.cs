using Domain.Etrn;

namespace Domain.Tests;

public class EtrnDocumentStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ошибка_обрезается_ровно_до_300_символов()
    {
        var document = Waiting();

        EtrnDocumentState.Fail(document, new string('я', 400));

        Assert.Equal(300, document.LastError.Length);
        Assert.Equal(EtrnDocumentStatus.Waiting, document.Status);
    }

    [Fact]
    public void Короткая_ошибка_остаётся_целой()
    {
        var document = Waiting();

        EtrnDocumentState.Fail(document, new string('я', 300));

        Assert.Equal(300, document.LastError.Length);
        Assert.Equal(new string('я', 300), document.LastError);
    }

    [Fact]
    public void Sending_ровно_10_минут_не_трогается()
    {
        var document = Waiting();
        Assert.True(EtrnDocumentState.StartSending(document, Now).IsSuccess);

        var result = EtrnDocumentState.Reconcile(document, stillAcceptanceStage: true, Now.AddMinutes(10));

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnDocumentStatus.Sending, document.Status);
    }

    [Fact]
    public void Sending_старше_10_минут_возвращается_в_ожидание_на_этапе_приёмки()
    {
        var document = Waiting();
        EtrnDocumentState.StartSending(document, Now);

        var result = EtrnDocumentState.Reconcile(document, stillAcceptanceStage: true, Now.AddMinutes(10).AddSeconds(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnDocumentStatus.Waiting, document.Status);
        Assert.Null(document.SendingAt);
    }

    [Fact]
    public void Sending_старше_10_минут_закрывается_на_другом_этапе()
    {
        var document = Waiting();
        EtrnDocumentState.StartSending(document, Now);

        var result = EtrnDocumentState.Reconcile(document, stillAcceptanceStage: false, Now.AddMinutes(11));

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnDocumentStatus.Closed, document.Status);
    }

    [Fact]
    public void Повтор_во_время_отправки_отклоняется()
    {
        var document = Waiting();
        EtrnDocumentState.StartSending(document, Now);

        var result = EtrnDocumentState.StartSending(document, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Приёмка уже выполняется", result.Error);
    }

    [Theory]
    [InlineData(EtrnDocumentStatus.Accepted)]
    [InlineData(EtrnDocumentStatus.Rejected)]
    [InlineData(EtrnDocumentStatus.Closed)]
    public void Конечный_статус_команду_не_принимает(string status)
    {
        var document = Waiting();
        document.Status = status;

        var result = EtrnDocumentState.StartSending(document, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Документ уже закрыт", result.Error);
    }

    [Fact]
    public void Успех_пишет_факты_комментарии_действие_и_время()
    {
        var document = Waiting();
        EtrnDocumentState.StartSending(document, Now);
        var decision = new EtrnDecision
        {
            Action = EtrnAcceptanceAction.Partially,
            Status = EtrnDocumentStatus.Accepted,
            GeneralComment = "общий",
            Lines =
            [
                new EtrnDecidedLine { Index = 1, FactPlaces = 9, FactWeight = "99.5", Comment = "свой" },
                new EtrnDecidedLine { Index = 2, FactPlaces = 5, FactWeight = "50", Comment = "общий" }
            ]
        };

        var result = EtrnDocumentState.Succeed(document, decision, Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(EtrnDocumentStatus.Accepted, document.Status);
        Assert.Equal(EtrnAcceptanceAction.Partially, document.Action);
        Assert.Equal("общий", document.GeneralComment);
        Assert.NotEqual("", document.SignedAt);
        Assert.Null(document.SendingAt);
        Assert.Equal(9, document.Lines[0].FactPlaces);
        Assert.Equal("99.5", document.Lines[0].FactWeight);
        Assert.Equal("свой", document.Lines[0].Comment);
        Assert.Equal("общий", document.Lines[1].Comment);
    }

    [Fact]
    public void Сбой_возвращает_в_ожидание_и_пишет_ошибку()
    {
        var document = Waiting();
        EtrnDocumentState.StartSending(document, Now);

        EtrnDocumentState.Fail(document, "Saby недоступна");

        Assert.Equal(EtrnDocumentStatus.Waiting, document.Status);
        Assert.Equal("Saby недоступна", document.LastError);
        Assert.Null(document.SendingAt);
    }

    [Fact]
    public void Опрос_закрывает_документ_если_этап_приёмки_закрыт_не_нами()
    {
        var document = Waiting();

        EtrnDocumentState.Close(document);

        Assert.Equal(EtrnDocumentStatus.Closed, document.Status);
    }

    private static EtrnIncomingDocumentEntity Waiting() => new()
    {
        Id = "doc:o:c:d",
        Status = EtrnDocumentStatus.Waiting,
        Lines =
        [
            new EtrnCargoLine { Index = 1, Name = "Груз 1", Places = 10, GrossWeight = "100" },
            new EtrnCargoLine { Index = 2, Name = "Груз 2", Places = 5, GrossWeight = "50" }
        ]
    };
}
