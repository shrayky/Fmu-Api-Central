using Domain.Dto.FmuApiExchangeData.Answer;
using Domain.Entitys.Organization;

namespace Domain.TrueApiIntegration;

public static class TrueApiExchangeTokens
{
    public static List<TrueApiExchangeTokenItem> ForExchange(
        IEnumerable<OrganizationEntity> organizations,
        IReadOnlyList<TrueApiToken> tokens,
        IReadOnlyCollection<string> allowedOrganizationIds)
    {
        if (allowedOrganizationIds.Count == 0)
            return [];

        var allowed = new HashSet<string>(
            allowedOrganizationIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.OrdinalIgnoreCase);

        if (allowed.Count == 0)
            return [];

        var byInn = new Dictionary<string, TrueApiToken>(StringComparer.Ordinal);

        foreach (var token in tokens)
        {
            var inn = (token.Inn ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(inn) || string.IsNullOrWhiteSpace(token.Token))
                continue;

            byInn[inn] = token;
        }

        var result = new List<TrueApiExchangeTokenItem>();

        foreach (var organization in organizations)
        {
            if (!allowed.Contains(organization.Id ?? string.Empty))
                continue;

            var inn = (organization.Inn ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(inn))
                continue;

            if (!byInn.TryGetValue(inn, out var token))
                continue;

            result.Add(new TrueApiExchangeTokenItem
            {
                Inn = inn,
                Token = token.Token,
                Expired = token.LiveUntil
            });
        }

        return result;
    }
}
