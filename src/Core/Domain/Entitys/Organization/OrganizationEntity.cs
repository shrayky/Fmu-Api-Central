using Domain.Entitys.Interfaces;

namespace Domain.Entitys.Organization;

public class OrganizationEntity : IHaveStringId
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Inn { get; set; } = string.Empty;

    public TrueApiIntegrationSettings TrueApiIntegrationSettings { get; set; } = new();
}
