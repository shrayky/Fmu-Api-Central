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
    /// Титул прочитан, но в строке нет ни мест, ни массы. Либо этап приёмки ещё не открыт.
    /// </summary>
    public string Error { get; set; } = "";

    public string Status { get; set; } = EtrnDocumentStatus.Waiting;

    /// <summary>
    /// Действие приёмки: «Получен», «Получен частично» или «Не принят».
    /// </summary>
    public string Action { get; set; } = "";

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
        Status = Status,
        Action = Action,
        Lines = Lines,
        RawXml = RawXml,
        LastError = Error,
        SortAt = EtrnIncomingDocumentEntity.SortDate(Date)
    };
}

public sealed class EtrnListedState
{
    public string OperatorDocumentId { get; init; } = "";

    public string Status { get; init; } = "";

    /// <summary>
    /// Действие приёмки из списка. Статуса мало: частичная приёмка — это Accepted плюс «Получен частично».
    /// </summary>
    public string Action { get; init; } = "";

    public string Error { get; init; } = "";
}

public sealed class EtrnFetchResult
{
    public EtrnPollPosition Position { get; init; } = EtrnPollPosition.Empty;

    public List<EtrnFetchedDocument> Documents { get; init; } = [];

    /// <summary>
    /// Документы, у которых событие уже не на этапе приёмки: этап закрыт не нами.
    /// </summary>
    public List<string> ClosedDocumentIds { get; init; } = [];

    /// <summary>
    /// Состояние из списка изменений, без титула. Отдельный список: пустой документ затёр бы строки карточки.
    /// </summary>
    public List<EtrnListedState> ListedStates { get; init; } = [];

    public List<EtrnExchange> Exchanges { get; init; } = [];

    public bool Completed { get; init; }

    public string Error { get; init; } = "";
}

public sealed class EtrnExecuteCommand
{
    public EtrnIncomingDocumentEntity Document { get; init; } = new();

    public EtrnDecision Decision { get; init; } = new();

    public EtrnCorrection? Correction { get; init; }
}

public sealed class EtrnExecuteResult
{
    public bool Completed { get; init; }

    /// <summary>
    /// Состояние 7 или 9: этап приёмки уже недоступен, карточку надо закрыть, а не вернуть в ожидание.
    /// </summary>
    public bool CloseDocument { get; init; }

    public string Error { get; init; } = "";

    public List<EtrnExchange> Exchanges { get; init; } = [];
}

public sealed class EtrnProbeResult
{
    public bool Ok { get; init; }

    public string Message { get; init; } = "";

    public List<EtrnExchange> Exchanges { get; init; } = [];
}

public sealed class EtrnProbeRequest
{
    public string Id { get; set; } = "";

    public string Type { get; set; } = "";

    public string Name { get; set; } = "";

    public string SubscriberId { get; set; } = "";

    public string? ApiKey { get; set; }

    public string ClientId { get; set; } = "";

    public string? ClientSecret { get; set; }

    public string KeycloakUrl { get; set; } = "";

    public string Realm { get; set; } = "";

    public string ApiUrl { get; set; } = "";

    public string CertificateNumber { get; set; } = "";

    public string? CertificatePassword { get; set; }
}

public sealed class EtrnProbeOutcome
{
    public bool Ok { get; init; }

    public string Message { get; init; } = "";
}

public sealed class EtrnStageResult
{
    public bool Completed { get; init; }

    public string Error { get; init; } = "";

    public string Stage { get; init; } = "";

    /// <summary>
    /// Этап приёмки у операторов называется по-разному, поэтому признак считает шлюз.
    /// </summary>
    public bool StillAcceptance { get; init; }

    /// <summary>
    /// Код состояния 7 или 9: документ в СБИС уже завершён, карточку надо закрыть.
    /// </summary>
    public bool CloseDocument { get; init; }

    public string StateName { get; init; } = "";

    /// <summary>
    /// Приёмка уже завершена в СБИС: Accepted или Rejected. Код состояния при этом может остаться 4.
    /// </summary>
    public string Status { get; init; } = "";

    public string Action { get; init; } = "";

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

    /// <summary>
    /// Байты титула. Если заданы, хеш и ссылка Saby не используются.
    /// </summary>
    public byte[] Content { get; init; } = [];
}

public sealed class EtrnSignatureMaterial
{
    public byte[]? Bytes { get; init; }

    /// <summary>
    /// Хеш ПодготовитьДействие уже посчитан. Подпись этих байт как файла даёт другой отпечаток.
    /// </summary>
    public bool PreparedHash { get; init; }

    public string FileUrl { get; init; } = "";

    public static Result<EtrnSignatureMaterial> Resolve(EtrnSignatureRequest request)
    {
        if (request.Content is { Length: > 0 })
            return Result.Success(new EtrnSignatureMaterial { Bytes = request.Content });

        var hash = (request.Hash ?? "").Trim();
        if (hash.Length > 0)
            return Result.Success(new EtrnSignatureMaterial
            {
                Bytes = Convert.FromBase64String(hash),
                PreparedHash = true
            });

        var url = (request.FileUrl ?? "").Trim();
        if (url.Length == 0)
            return Result.Failure<EtrnSignatureMaterial>("Saby не вернула хеш или ссылку для подписи.");

        return Result.Success(new EtrnSignatureMaterial { FileUrl = url });
    }
}

public interface IEtrnSignaturePort
{
    Task<Result<string>> SignAsync(EtrnSignatureRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Имён методов Saby в Domain нет: Application работает с опросом, приёмкой, сверкой этапа и проверкой входа.
/// </summary>
public interface IEtrnOperator
{
    string Type { get; }

    Task<Result<EtrnProbeResult>> ProbeAsync(EtrnOperatorContext context, CancellationToken cancellationToken);

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

    /// <summary>
    /// Карточка СБИС в ожидании больше не приходит в список: курсор ушёл вперёд. Состояние сверяем при открытии.
    /// </summary>
    Task SyncOpenAsync(string id, CancellationToken cancellationToken);
}

public interface IEtrnRequestCleaner
{
    Task<int> CleanAsync(CancellationToken cancellationToken);
}
