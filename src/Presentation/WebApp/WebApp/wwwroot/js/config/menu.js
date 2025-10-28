// js/config/menu.js
export const MENU_ITEMS = {
    INSTANCE_MONITORING: {
        id: "instanceMonitoring",
        value: "Мониторинг fmu-api"
    },
    SOFTWARE_UPDATES: {
        id: "softwareUpdates",
        value: "Обновления fmu-api"
    },
    SERVER: {
        id: "serverSettings",
        value: "Настройки сервера"
    },
    COUCHDB: {
        id: "couchDbSettings",
        value: "CouchDB"
    },
    TELEGRAM_BOT_SETTINGS: {
        id: "telegramBotSettings",
        value: "Telegram бот"
    },
    LOGS: {
        id: "logs",
        value: "Логи работы"
    },
    INFO: {
        id: "information",
        value: "Информация"
    },
};

// Вспомогательная функция для получения плоского списка всех ID
export const getAllMenuIds = () => {
    const ids = [];
    Object.values(MENU_ITEMS).forEach(item => {
        ids.push(item.id);
        if (item.data) {
            item.data.forEach(subItem => ids.push(subItem.id));
        }
    });
    return ids;
};

// Вспомогательная функция для получения пути к элементу меню
export const getMenuPath = (id) => {
    for (const [section, item] of Object.entries(MENU_ITEMS)) {
        if (item.id === id) return [section];
        if (item.data) {
            const subItem = item.data.find(sub => sub.id === id);
            if (subItem) return [section, subItem.id];
        }
    }
    return null;
};
