using Domain.Dto.FmuApiExchangeData.Request;
using Domain.Dto.Interfaces;

namespace Domain.Dto;

public class InstanceEntity : IHaveStringId
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string SecretKey { get; set; } = string.Empty;
    public FmuApiSetting Settings { get; set; } = new();
    public List<CdnInformation> Cdn { get; set; } = [];
    public List<LocalModuleInformation> LocalModules { get; set; } = [];
    public bool SettingsModified { get; set; } = false;
}