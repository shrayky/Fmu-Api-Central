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

interface AlertViolationItem {
    productGroup: number;
    productGroupName: string;
    region: string;
    violationResult: string;
    violationResultName: string;
    violationNumber: number;
}

interface AlertOrganization {
    id: string;
    name: string;
    inn: string;
    certificateNumber: string;
    certificateWorkUntil?: string;
}

interface AlertCryptoProLicense {
    permanent: boolean;
    expiresAt?: string;
}

interface AlertViolationDay {
    inn: string;
    organizationName: string;
    date: number;
    dateIso: string;
    dateYmd: string;
    penaltyAmountRub: number;
    violations: AlertViolationItem[];
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
declare const violations: AlertViolationDay[];
declare const organizations: AlertOrganization[];
declare const cryptoProLicense: AlertCryptoProLicense;
declare const now: string;
declare const settings: AlertSettings;
declare function isVersionBelowThreshold(currentVersion: string, thresholdVersion: string): boolean;
`;
