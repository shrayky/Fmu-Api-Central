using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using Domain.Entitys.Interfaces;
using Domain.Entitys.Organization;

namespace Domain.Etrn;

public sealed class EtrnIncomingFilter
{
    public string OrganizationId { get; set; } = "";

    public string ConnectionId { get; set; } = "";

    public string Status { get; set; } = "";

    public string Number { get; set; } = "";

    public string Consignor { get; set; } = "";

    /// <summary>
    /// Точка в отборе. Пусто — отбора нет; пустая строка документа — только при OnlyWithoutSalePoint.
    /// </summary>
    public string SalePointUid { get; set; } = "";

    public bool OnlyWithoutSalePoint { get; set; }
}

public sealed class EtrnSyncState : IHaveStringId
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("organizationId")]
    public string OrganizationId { get; set; } = "";

    [JsonPropertyName("connectionId")]
    public string ConnectionId { get; set; } = "";

    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = "";

    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = "";

    [JsonPropertyName("eventAt")]
    public string EventAt { get; set; } = "";

    [JsonPropertyName("lastSuccessAt")]
    public string LastSuccessAt { get; set; } = "";

    [JsonPropertyName("lastError")]
    public string LastError { get; set; } = "";

    public static string BuildId(string connectionId) => $"sync:{connectionId}";

    [JsonIgnore]
    public EtrnPollPosition Position => new(EventId, DocumentId, EventAt);
}

public sealed class EtrnLineView
{
    public int Index { get; set; }

    public string Name { get; set; } = "";

    public int? Places { get; set; }

    public string GrossWeight { get; set; } = "";

    public int? FactPlaces { get; set; }

    public string FactWeight { get; set; } = "";

    public string Comment { get; set; } = "";

    public bool EditablePlaces { get; set; }

    public bool EditableWeight { get; set; }
}

public class EtrnIncomingRow
{
    public string Id { get; set; } = "";

    public string Number { get; set; } = "";

    public string Date { get; set; } = "";

    public string Consignor { get; set; } = "";

    public string DeliveryAddress { get; set; } = "";

    public string SalePointUid { get; set; } = "";

    public string SalePointName { get; set; } = "";

    public bool SalePointClosed { get; set; }

    public string OrganizationId { get; set; } = "";

    public string OrganizationName { get; set; } = "";

    public string ConnectionId { get; set; } = "";

    public string OperatorName { get; set; } = "";

    public string Status { get; set; } = "";

    public string StatusLabel { get; set; } = "";

    public string Action { get; set; } = "";

    public string LastError { get; set; } = "";

    public long SortAt { get; set; }
}

public sealed class EtrnIncomingView : EtrnIncomingRow
{
    public string Uid { get; set; } = "";

    public string GeneralComment { get; set; } = "";

    public string SignedAt { get; set; } = "";

    public List<EtrnLineView> Lines { get; set; } = [];

    public bool CanDecide { get; set; }

    public bool CanCorrect { get; set; }

    public int CorrectionNumber { get; set; }

    public string CorrectionDate { get; set; } = "";

    /// <summary>
    /// Открытые точки организации документа: из них выбирается точка в карточке.
    /// </summary>
    public List<SalePointOption> SalePoints { get; set; } = [];
}

public sealed class EtrnRowsPage<T>
{
    public List<T> Rows { get; set; } = [];

    public int Skip { get; set; }

    public bool HasMore { get; set; }
}

public sealed class EtrnFilterConnection
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Type { get; set; } = "";

    public bool Enable { get; set; }

    public string LastError { get; set; } = "";
}

public sealed class EtrnFilterOrganization
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public List<EtrnFilterConnection> Connections { get; set; } = [];

    public List<SalePointOption> SalePoints { get; set; } = [];
}

public interface IEtrnIncomingRepository
{
    Task<Result<EtrnIncomingDocumentEntity?>> Find(string id);

    Task<Result> Save(EtrnIncomingDocumentEntity document);

    Task<Result<List<EtrnIncomingDocumentEntity>>> List(EtrnIncomingFilter filter, int skip, int limit);

    /// <summary>
    /// Есть ли документ с этой точкой: проверка перед удалением точки из справочника.
    /// </summary>
    Task<Result<bool>> AnyBySalePoint(string organizationId, string salePointUid);

    /// <summary>
    /// Документы, зависшие в Sending настолько, что опрос должен сверить этап.
    /// </summary>
    Task<Result<List<EtrnIncomingDocumentEntity>>> StaleSending(long beforeUnix, int limit);
}

public interface IEtrnSyncStateRepository
{
    Task<Result<EtrnSyncState?>> Find(string connectionId);

    Task<Result> Save(EtrnSyncState state);
}
