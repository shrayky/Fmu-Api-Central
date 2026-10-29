import { MENU_ITEMS } from '../config/menu.js';
import { menuInsertIndex } from '../config/menuAvailability.js';
import { loadConfiguration } from './ConfigurationService.js';

const SIDEBAR_ID = "mainSidebar";
const VETIS_ID = "vetis";

export function setVetisMenuVisible(enabled) {
    const sidebar = $$(SIDEBAR_ID);
    if (!sidebar) {
        return;
    }

    const shown = sidebar.exists(VETIS_ID);
    if (enabled && !shown) {
        const shownIds = Object.values(MENU_ITEMS).map((item) => item.id).filter((id) => sidebar.exists(id));
        const item = Object.values(MENU_ITEMS).find((entry) => entry.id === VETIS_ID);
        sidebar.add(item, menuInsertIndex(MENU_ITEMS, shownIds, VETIS_ID));
    }

    if (!enabled && shown) {
        sidebar.remove(VETIS_ID);
    }
}

export async function syncVetisMenu() {
    const configuration = await loadConfiguration();
    setVetisMenuVisible(!!configuration?.value?.Content?.vetisSettings?.enable);
}
