using System.Text.Json.Nodes;

namespace Application.InstanceGroup.Services;

public static class CheckerSettingsFile
{
    public static bool IsJsonObject(byte[] content)
    {
        if (content == null || content.Length == 0)
            return false;

        try
        {
            return JsonNode.Parse(content) is JsonObject;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
