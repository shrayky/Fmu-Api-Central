export const VETIS_DEFAULT_ENDPOINT = "https://api.vetrf.ru";

export function vetisEndpoint(value) {
    const text = String(value || "").trim();
    if (!text)
        return VETIS_DEFAULT_ENDPOINT;

    try {
        const url = new URL(text);
        if (url.protocol !== "http:" && url.protocol !== "https:")
            return text;

        return url.origin;
    }
    catch {
        return text;
    }
}

function lines(value) {
    return String(value || "")
        .split(/\r?\n/)
        .map((item) => item.trim())
        .filter((item) => item.length > 0);
}

export function buildConfigurationUpdaters(values) {
    return {
        serverSettings: _ => ({
            apiIpPort: parseInt(values.apiIpPort),
            publicAddress: String(values.publicAddress || "").trim(),
            corsOrigins: lines(values.corsOrigins),
            trustedProxies: lines(values.trustedProxies)
        }),
        loggerSettings: _ => ({
            isEnabled: !!values.isEnabled,
            logDepth: parseInt(values.logDepth),
            logLevel: values.logLevel
        }),
        security: prev => ({
            ...prev,
            allowLegacyAgentApi: !!values.allowLegacyAgentApi
        }),
        // На форме нет таймаута и опроса результата — их нельзя затирать.
        vetisSettings: prev => ({
            ...prev,
            enable: !!values.vetisEnable,
            endpoint: vetisEndpoint(values.vetisEndpoint),
            incomingFetchIntervalMinutes: parseInt(values.vetisIncomingInterval),
            incomingFetchPageSize: parseInt(values.vetisIncomingPageSize),
            autoQuenchIntervalMinutes: parseInt(values.vetisAutoQuenchInterval),
            autoQuenchBatchSize: parseInt(values.vetisAutoQuenchBatch),
            requireVehicleForQuench: !!values.vetisRequireVehicle,
            applicationRetentionDays: parseInt(values.vetisApplicationRetention),
            failedApplicationRetentionDays: parseInt(values.vetisFailedApplicationRetention)
        })
    };
}
