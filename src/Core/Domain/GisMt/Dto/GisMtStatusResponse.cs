using System.Text.Json.Serialization;

namespace Domain.GisMt.Dto;

/// <summary>
/// Снимок статусов обмена GisMt по организациям.
/// </summary>
public class GisMtStatusResponse
{
    [JsonPropertyName("organizations")]
    public List<GisMtOrganizationStatus> Organizations { get; set; } = [];
}
