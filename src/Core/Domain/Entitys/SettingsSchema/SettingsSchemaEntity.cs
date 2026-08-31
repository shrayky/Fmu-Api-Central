using Domain.Dto.FmuApiExchangeData;
using Domain.Entitys.Interfaces;

namespace Domain.Entitys.SettingsSchema;

public class SettingsSchemaEntity : IHaveStringId
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HttpRequestTimeouts HttpRequestTimeouts { get; set; } = new();
    public List<GisMtProductMapping> GisMtProductMappings { get; set; } = [];
    public List<StringValue> HostsToPing { get; set; } = [];
}
