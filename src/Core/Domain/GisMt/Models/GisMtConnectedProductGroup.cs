using System.Text.Json.Serialization;

namespace Domain.GisMt.Models;

/// <summary>
/// Подключённая товарная группа Честного знака у организации.
/// </summary>
public class GisMtConnectedProductGroup
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("groupName")]
    public string GroupName { get; set; } = string.Empty;
}
