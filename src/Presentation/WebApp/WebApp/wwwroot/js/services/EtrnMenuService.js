import { MENU_ITEMS } from '../config/menu.js';
import { menuInsertIndex } from '../config/menuAvailability.js';
import { loadConfiguration } from './ConfigurationService.js';

const SIDEBAR_ID = "mainSidebar";
const ETRN_ID = "etrn";

export function setEtrnMenuVisible(enabled) {
    const sidebar = $$(SIDEBAR_ID);
    if (!sidebar) {
        return;
    }

    const shown = sidebar.exists(ETRN_ID);
    if (enabled && !shown) {
        const shownIds = Object.values(MENU_ITEMS).map((item) => item.id).filter((id) => sidebar.exists(id));
        const item = Object.values(MENU_ITEMS).find((entry) => entry.id === ETRN_ID);
        sidebar.add(item, menuInsertIndex(MENU_ITEMS, shownIds, ETRN_ID));
    }

    if (!enabled && shown) {
        sidebar.remove(ETRN_ID);
    }
}

export async function syncEtrnMenu() {
    const configuration = await loadConfiguration();
    setEtrnMenuVisible(configuration?.value?.Content?.etrnSettings?.enable === true);
}
