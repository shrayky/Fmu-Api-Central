export function organizationTabCells(salePoints, marking, vetis, etrn, vetisOn, etrnOn) {
    return [
        salePoints,
        marking,
        ...(vetisOn ? [vetis] : []),
        ...(etrnOn ? [etrn] : [])
    ];
}

export function etrnTabState(certificateNumber, currentTab, markingTab, etrnTab) {
    const enabled = String(certificateNumber ?? "").trim() !== "";
    if (enabled)
        return { enabled: true, tab: currentTab };

    return {
        enabled: false,
        tab: currentTab === etrnTab ? markingTab : currentTab
    };
}
