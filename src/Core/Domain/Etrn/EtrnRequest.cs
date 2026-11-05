using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Domain.Entitys.Interfaces;

namespace Domain.Etrn;

public static class EtrnMethods
{
    public const string ChangesList = "СБИС.СписокИзменений";
    public const string ReadDocument = "СБИС.ПрочитатьДокумент";
    public const string WriteEnclosure = "СБИС.ЗаписатьВложение";
    public const string PrepareAction = "СБИС.ПодготовитьДействие";
    public const string ExecuteAction = "СБИС.ВыполнитьДействие";
    public const string AuthByCertificate = "СБИС.АутентифицироватьПоСертификату";

    public static readonly string[] Service =
    [
        ChangesList,
        ReadDocument,
        WriteEnclosure,
        PrepareAction,
        ExecuteAction
    ];

    public static string Label(string? method) => method switch
    {
        ChangesList => "Список изменений",
        ReadDocument => "Чтение документа",
        WriteEnclosure => "Запись вложения",
        PrepareAction => "Подготовка действия",
        ExecuteAction => "Выполнение действия",
        _ => method ?? ""
    };
}

public sealed class EtrnRequestEntity : IHaveStringId
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("parameters")]
    public string Parameters { get; set; } = "";

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = "";

    [JsonPropertyName("operator")]
    public EtrnOperator Operator { get; set; } = new();

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; } = "";

    [JsonPropertyName("requestBody")]
    public string RequestBody { get; set; } = "";

    [JsonPropertyName("responseBody")]
    public string ResponseBody { get; set; } = "";

    [JsonPropertyName("hasResponse")]
    public bool HasResponse { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    public static EtrnRequestEntity From(
        EtrnExchange exchange,
        string organizationId,
        EtrnOperator @operator,
        DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Method = exchange.Method,
        Parameters = exchange.Parameters,
        OrganizationId = organizationId,
        Operator = @operator,
        Completed = exchange.Completed,
        Error = EtrnDocumentState.Truncate(exchange.Error),
        RequestBody = exchange.RequestBody,
        ResponseBody = exchange.ResponseBody,
        HasResponse = !string.IsNullOrWhiteSpace(exchange.ResponseBody),
        CreatedAt = now.ToUnixTimeSeconds()
    };
}

public static class EtrnRequestRetention
{
    public static readonly TimeSpan Success = TimeSpan.FromDays(1);

    public static readonly TimeSpan Failure = TimeSpan.FromDays(7);

    public static bool IsExpired(bool completed, long createdAt, DateTimeOffset now) =>
        IsExpired(completed, DateTimeOffset.FromUnixTimeSeconds(createdAt), now);

    /// <summary>
    /// Ровно срок ещё хранится; удаляется то, что старше на секунду.
    /// </summary>
    public static bool IsExpired(bool completed, DateTimeOffset createdAt, DateTimeOffset now) =>
        now - createdAt > (completed ? Success : Failure);

    public static TimeSpan Retention(bool completed) => completed ? Success : Failure;
}

public sealed class EtrnRequestFilter
{
    public string OrganizationId { get; set; } = "";

    public string ConnectionId { get; set; } = "";
}

public class EtrnRequestRow
{
    public string Id { get; set; } = "";

    public string Method { get; set; } = "";

    public string Parameters { get; set; } = "";

    public string OrganizationId { get; set; } = "";

    public string OrganizationName { get; set; } = "";

    public string OperatorName { get; set; } = "";

    public string ConnectionId { get; set; } = "";

    public bool Completed { get; set; }

    public string Error { get; set; } = "";

    public long CreatedAt { get; set; }
}

public sealed class EtrnRequestView : EtrnRequestRow
{
    public string RequestBody { get; set; } = "";

    public string ResponseBody { get; set; } = "";
}

public interface IEtrnRequestRepository
{
    Task<Result> Save(EtrnRequestEntity request);

    Task<Result<EtrnRequestEntity?>> Find(string id);

    Task<Result<List<EtrnRequestEntity>>> List(EtrnRequestFilter filter, int skip, int limit);

    Task<Result<List<EtrnRequestEntity>>> ListExpired(bool completed, long createdBefore, int limit);

    Task<Result> Delete(IReadOnlyCollection<string> ids);
}
