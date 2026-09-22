using System.Text.Json.Serialization;

namespace Domain.Entitys.Instance.Dto;

public record GroupAssignRequest
{
    [JsonPropertyName("tokens")]
    public List<string> Tokens { get; init; } = [];

    /// <summary>
    /// Идентификатор группы. Пустая строка снимает группу с инстанса.
    /// </summary>
    [JsonPropertyName("groupId")]
    public string GroupId { get; init; } = string.Empty;
}
