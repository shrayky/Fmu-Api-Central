using Domain.Dto.FmuApiExchangeData.Answer;
using Domain.Entitys.Organization;

namespace Domain.TrueApiIntegration;

public static class XApiKeyExchange
{
    public static List<XApiKeyExchangeItem> ForExchange(
        IEnumerable<OrganizationEntity> organizations,
        IReadOnlyList<TrueApiToken> keys,
        IReadOnlyCollection<string> allowedOrganizationIds,
        DateTime now)
    {
        if (allowedOrganizationIds.Count == 0)
            return [];

        var allowed = new HashSet<string>(
            allowedOrganizationIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
            return [];

        var liveByInn = new Dictionary<string, TrueApiToken>(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            var inn = (key.Inn ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(inn) || string.IsNullOrWhiteSpace(key.Token))
                continue;

            if (key.LiveUntil < now)
                continue;

            liveByInn[inn] = key;
        }

        var result = new List<XApiKeyExchangeItem>();

        foreach (var organization in organizations)
        {
            if (!allowed.Contains(organization.Id ?? string.Empty))
                continue;

            var inn = (organization.Inn ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(inn))
                continue;

            var signature = organization.TrueApiIntegrationSettings?.DigitalSignature?.Trim() ?? string.Empty;
            if (signature.Length == 0)
                continue;

            if (liveByInn.TryGetValue(inn, out var key))
            {
                result.Add(new XApiKeyExchangeItem
                {
                    Inn = inn,
                    XApiKey = key.Token,
                    XApiKeyExpired = false,
                    ExpiresAt = key.LiveUntil
                });
                continue;
            }

            var expiresAt = organization.XApiKeyExpiresAt;
            if (expiresAt == null || expiresAt.Value >= now)
                continue;

            result.Add(new XApiKeyExchangeItem
            {
                Inn = inn,
                XApiKey = string.Empty,
                XApiKeyExpired = true,
                ExpiresAt = expiresAt
            });
        }

        return result;
    }
}
