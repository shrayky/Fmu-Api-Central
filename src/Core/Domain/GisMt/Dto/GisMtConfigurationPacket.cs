using Domain.Configuration.Options;
using System.Text.Json.Serialization;

namespace Domain.GisMt.Dto;

/// <summary>
/// Пакет настроек, который Central отдаёт GisMt одним PUT.
/// </summary>
public class GisMtConfigurationPacket
{
    [JsonPropertyName("settings")]
    public GisMtRemoteSettings Settings { get; set; } = new();

    [JsonPropertyName("databaseConnection")]
    public DatabaseConnection DatabaseConnection { get; set; } = new();

    [JsonPropertyName("tokens")]
    public List<GisMtTokenItem> Tokens { get; set; } = [];
}
