import { vetisEndpoint } from "./saveSettings.js";

export function mapServerSettings(configuration) {
    const server = configuration.serverSettings ?? {};
    const logger = configuration.loggerSettings ?? {};
    const vetis = configuration.vetisSettings ?? {};
    const etrn = configuration.etrnSettings ?? {};

    return {
        server: {
            apiIpPort: server.apiIpPort,
            publicAddress: server.publicAddress || "",
            corsOrigins: (server.corsOrigins || []).join("\n"),
            trustedProxies: (server.trustedProxies || []).join("\n"),
            allowLegacyAgentApi: configuration.security?.allowLegacyAgentApi !== false,
            isEnabled: logger.isEnabled,
            logDepth: logger.logDepth,
            logLevel: logger.logLevel,
        },
        vetis: {
            enable: !!vetis.enable,
            endpoint: vetisEndpoint(vetis.endpoint),
            incomingFetchIntervalMinutes: vetis.incomingFetchIntervalMinutes ?? 5,
            incomingFetchPageSize: vetis.incomingFetchPageSize ?? 100,
            applicationRetentionDays: vetis.applicationRetentionDays ?? 1,
            failedApplicationRetentionDays: vetis.failedApplicationRetentionDays ?? 7,
            autoQuenchIntervalMinutes: vetis.autoQuenchIntervalMinutes ?? 15,
            autoQuenchBatchSize: vetis.autoQuenchBatchSize ?? 20,
            requireVehicleForQuench: !!vetis.requireVehicleForQuench,
        },
        etrn: {
            incomingFetchIntervalMinutes: etrn.incomingFetchIntervalMinutes ?? 5,
        }
    };
}
