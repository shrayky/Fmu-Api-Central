using Domain.Bot;
using Domain.Entitys.Interfaces;

namespace Domain.Entitys.InstanceGroup;

public class InstanceGroupEntity : IHaveStringId
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool AutoUpdateAllowed { get; set; }
    public string SettingsSchemaId { get; set; } = string.Empty;
    public List<string> OrganizationIds { get; set; } = [];
    public AlertChannel AlertChannel { get; set; } = AlertChannel.Disabled();
}
