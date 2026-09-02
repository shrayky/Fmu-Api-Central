export const ALERT_SCRIPT_DTS = `
interface AlertLocalModule {
    id: number;
    address: string;
    version: string;
    lastSync: number;
    status: string;
    operationMode: string;
}

interface AlertTsPiot {
    name: string;
    address: string;
    online: boolean;
    version: string;
    licenseActiveTill?: string;
}

interface AlertInstance {
    id: string;
    name: string;
    address: string;
    version: string;
    lastUpdated: string;
    hoursSinceUpdate: number;
    localModules: AlertLocalModule[];
    tsPiots: AlertTsPiot[];
}

interface AlertStatistic {
    nodeId: string;
    instanceName: string;
    date: number;
    dateIso: string;
    total: number;
    successfulOnlineChecks: number;
    successfulOfflineChecks: number;
    successRatePercentage: number;
}

interface AlertLocalModuleSettings {
    versionAlert: string;
    daysWithoutSynchronization: number;
}

interface AlertTsPiotSettings {
    statusAlertEnabled: boolean;
    licenseAlertEnabled: boolean;
    licenseAlertDays: number;
    versionAlert: string;
}

interface AlertSettings {
    offlineNodeAlertInterval: number;
    localModuleAlerts: AlertLocalModuleSettings;
    tsPiotAlerts: AlertTsPiotSettings;
}

interface AlertDataset {
    title?: string;
    message?: string;
    items?: string[];
}

declare const instances: AlertInstance[];
declare const statistics: AlertStatistic[];
declare const now: string;
declare const settings: AlertSettings;
declare function isVersionBelowThreshold(currentVersion: string, thresholdVersion: string): boolean;
`;
