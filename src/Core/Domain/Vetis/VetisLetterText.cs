using System.Text.Json.Serialization;

namespace Domain.Vetis;

public sealed class VetisLetterText
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}
