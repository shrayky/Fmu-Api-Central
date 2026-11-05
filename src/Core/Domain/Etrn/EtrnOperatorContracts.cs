using CSharpFunctionalExtensions;

namespace Domain.Etrn;

public sealed class EtrnOperatorContext
{
    public string OrganizationId { get; init; } = "";

    public string Inn { get; init; } = "";

    public EtrnConnection Connection { get; init; } = new();

    public string CertificateNumber { get; init; } = "";

    public string CertificatePassword { get; init; } = "";

    public EtrnPollPosition Position { get; init; } = EtrnPollPosition.Empty;

    public EtrnOperator Operator => EtrnOperator.For(Connection);
}

public sealed class EtrnExchange
{
    public string Method { get; init; } = "";

    public string Parameters { get; init; } = "";

    public string RequestBody { get; init; } = "";

    public string ResponseBody { get; init; } = "";

    public bool Completed { get; init; }

    public string Error { get; init; } = "";
}

public sealed class EtrnFetchedDocument
{
    public string OperatorDocumentId { get; init; } = "";

    public string Number { get; init; } = "";

    public string Date { get; init; } = "";

    public string Consignor { get; init; } = "";

    public string Uid { get; init; } = "";

    public string DeliveryAddress { get; init; } = "";

    public string RawXml { get; init; } = "";

    public List<EtrnCargoLine> Lines { get; set; } = [];

    /// <summary>
    /// Титул прочитан, но в строке нет ни мест, ни массы.
    /// </summary>
    public string Error { get; init; } = "";

    public EtrnIncomingDocumentEntity ToEntity(EtrnOperatorContext context) => new()
    {
        Id = EtrnIncomingDocumentEntity.BuildId(context.OrganizationId, context.Connection.Id, OperatorDocumentId),
        OrganizationId = context.OrganizationId,
        OperatorDocumentId = OperatorDocumentId,
        Operator = EtrnOperator.For(context.Connection),
        Number = Number,
        Date = Date,
        Consignor = Consignor,
        Uid = Uid,
        DeliveryAddress = DeliveryAddress,
        Status = EtrnDocumentStatus.Waiting,
        Lines = Lines,
        RawXml = RawXml,
        LastError = Error,
        SortAt = EtrnIncomingDocumentEntity.SortDate(Date)
    };
}

public sealed class EtrnFetchResult
{
    public EtrnPollPosition Position { get; init; } = EtrnPollPosition.Empty;

    public List<EtrnFetchedDocument> Documents { get; init; } = [];

    /// <summary>
    /// Документы, у которых событие уже не на этапе приёмки: этап закрыт не нами.
    /// </summary>
    public List<string> ClosedDocumentIds { get; init; } = [];

    public List<EtrnExchange> Exchanges { get; init; } = [];

    public bool Completed { get; init; }

    public string Error { get; init; } = "";
}

public sealed class EtrnExecuteCommand
{
    public EtrnIncomingDocumentEntity Document { get; init; } = new();

    public EtrnDecision Decision { get; init; } = new();
}

public sealed class EtrnExecuteResult
{
    public bool Completed { get; init; }

    public string Error { get; init; } = "";

    public List<EtrnExchange> Exchanges { get; init; } = [];
}

public sealed class EtrnStageResult
{
    public bool Completed { get; init; }

    public string Error { get; init; } = "";

    public string Stage { get; init; } = "";

    public bool StillAcceptance => string.Equals(
        (Stage ?? "").Trim(),
        EtrnPollEvent.AcceptanceStage,
        StringComparison.OrdinalIgnoreCase);

    public List<EtrnExchange> Exchanges { get; init; } = [];
}

public sealed class EtrnSignatureRequest
{
    public string CertificateNumber { get; init; } = "";

    public string CertificatePassword { get; init; } = "";

    /// <summary>
    /// Хеш из ответа Saby в том виде, в котором он пришёл: перекодировать нельзя.
    /// </summary>
    public string Hash { get; init; } = "";

    /// <summary>
    /// Ссылка на файл вместо хеша.
    /// </summary>
    public string FileUrl { get; init; } = "";
}

public interface IEtrnSignaturePort
{
    Task<Result<string>> SignAsync(EtrnSignatureRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Имён методов Saby в Domain нет: Application работает только с этими двумя действиями и сверкой этапа.
/// </summary>
public interface IEtrnOperator
{
    string Type { get; }

    Task<Result<EtrnFetchResult>> FetchAsync(EtrnOperatorContext context, CancellationToken cancellationToken);

    Task<Result<EtrnExecuteResult>> ExecuteAsync(
        EtrnOperatorContext context,
        EtrnExecuteCommand command,
        CancellationToken cancellationToken);

    Task<Result<EtrnStageResult>> ReadStageAsync(
        EtrnOperatorContext context,
        string operatorDocumentId,
        CancellationToken cancellationToken);
}

public interface IEtrnIncomingLoader
{
    /// <summary>
    /// Последняя ошибка подключения за проход: строка фонового задания показывает её.
    /// </summary>
    string LastError { get; }

    /// <summary>
    /// Сколько разных входящих документов записано за проход.
    /// </summary>
    Task<int> LoadAsync(CancellationToken cancellationToken);
}

public interface IEtrnRequestCleaner
{
    Task<int> CleanAsync(CancellationToken cancellationToken);
}
