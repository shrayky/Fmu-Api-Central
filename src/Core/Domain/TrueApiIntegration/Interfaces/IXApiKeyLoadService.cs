using Domain.Entitys.Organization;

namespace Domain.TrueApiIntegration.Interfaces;

public interface IXApiKeyLoadService
{
    Task Refresh(IEnumerable<OrganizationEntity> organizations, CancellationToken cancellationToken);
}