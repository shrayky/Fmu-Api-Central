using Domain.Dto.FmuApiExchangeData;
using Domain.Entitys.SettingsSchema;

namespace Application.SettingsSchema;

/// <summary>
/// Применяет таймауты, товарные группы и хосты пинга схемы к снимку настроек инстанса.
/// </summary>
public static class GroupSettingsApply
{
    public static bool HasSnapshot(FmuApiSetting settings)
        => settings.Version > 0 || settings.Assembly > 0 || settings.Organizations.Count > 0;

    public static FmuApiSetting Apply(
        FmuApiSetting source,
        HttpRequestTimeouts timeouts,
        IReadOnlyList<GisMtProductMapping> mappings,
        IReadOnlyList<StringValue> hostsToPing)
        => source with
        {
            TimeOut = new TimeOutConfiguration
            {
                CdnRequest = timeouts.CdnRequestTimeout,
                TrueSignCheckRequest = timeouts.CheckMarkRequestTimeout,
                InternetConnectionCheck = timeouts.CheckInternetConnectionTimeout,
                SyncWithTsPiot = timeouts.SyncWithTsPiot
            },
            GisMtProductMappings = mappings
                .Select(item => new GisMtProductMapping
                {
                    AtolCode = item.AtolCode,
                    TrueApiGroupId = item.TrueApiGroupId,
                    Name = item.Name,
                    CheckSmp = item.CheckSmp
                })
                .ToList(),
            HostsToPing = hostsToPing
                .Select((item, index) => new StringValue
                {
                    Id = item.Id > 0 ? item.Id : index + 1,
                    Value = item.Value
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .ToList()
        };
}
