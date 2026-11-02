const settingFlags = [
    "checkSmp",
    "checkMrp",
    "checkExpireDate",
    "haveExpireDate",
    "forbidSaleByExpireDate"
];

export function hasGroupSetting(row) {
    if (!row)
        return false;

    return settingFlags.some((flag) => !!row[flag]);
}
