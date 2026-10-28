using Domain.Entitys.Organization;

namespace Domain.TrueApiIntegration;

public static class TrueApiTokenRefresh
{
    public static List<OrganizationEntity> Due(
        IEnumerable<OrganizationEntity> organizations,
        Func<string, TrueApiToken> currentToken)
    {
        var due = new List<OrganizationEntity>();
        foreach (var organisation in organizations)
        {
            var inn = organisation.Inn;
            if (string.IsNullOrEmpty(inn))
                continue;

            if (!(organisation.TrueApiIntegrationSettings?.Enable ?? false))
                continue;

            if (currentToken(inn).Token != string.Empty)
                continue;

            due.Add(organisation);
        }

        return due;
    }
}
