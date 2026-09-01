using Domain.Entitys.Interfaces;
using Domain.GisMt.Models;
using System.Runtime.Serialization;

namespace Domain.Entitys.Organization;

public class OrganizationEntity : IHaveStringId
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Inn { get; set; } = string.Empty;

    public TrueApiIntegrationSettings TrueApiIntegrationSettings { get; set; } = new();

    public GisMtLastStatus GisMtLastStatus { get; set; } = new();

    public List<GisMtConnectedProductGroup> GisMtProductGroups { get; set; } = [];

    public int? GisMtLastStatusCode { get; set; }

    public string GisMtLastStatusDescription { get; set; } = string.Empty;

    public DateTime? GisMtLastStatusAt { get; set; }

    public bool ShouldSerializeGisMtLastStatusCode() => false;

    public bool ShouldSerializeGisMtLastStatusDescription() => false;

    public bool ShouldSerializeGisMtLastStatusAt() => false;

    /// <summary>
    /// Переносит старые плоские поля CouchDB во вложенный объект.
    /// </summary>
    public void NormalizeGisMtLastStatus()
    {
        OnDeserialized(default);
    }

    [OnDeserialized]
    internal void OnDeserialized(StreamingContext context)
    {
        GisMtLastStatus ??= new();
        GisMtProductGroups ??= [];
        if (GisMtLastStatus.Code != null)
            return;

        if (GisMtLastStatusCode == null)
            return;

        GisMtLastStatus.Code = GisMtLastStatusCode;
        GisMtLastStatus.Description = GisMtLastStatusDescription ?? string.Empty;
        GisMtLastStatus.At = GisMtLastStatusAt;
    }
}
