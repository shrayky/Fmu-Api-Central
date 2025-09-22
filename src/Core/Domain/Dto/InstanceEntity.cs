using Domain.Dto.fmuApiStateInformation;
using Domain.Dto.Interfaces;

namespace Domain.Dto;

public class InstanceEntity : IHaveStringId
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string SecretKey { get; set; } = string.Empty;
    public FmuApiParameters Settings { get; set; } = new();
    public List<CdnData> Cdn { get; set; } = [];
    public object LocalModules { get; set; } = new object();
    public bool SettingsModified { get; set; } = false;
}