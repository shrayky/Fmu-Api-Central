export const MENU_ITEMS = {
    INSTANCE_MONITORING: {
        id: "instanceMonitoring",
        value: "Мониторинг fmu-api",
        icon: "mdi mdi-monitor-dashboard"
    },
    MARK_CHECK_STATISTICS: {
        id: "markCheckStatistics",
        value: "Статистика проверок",
        icon: "mdi mdi-monitor-dashboard"
    },
    SOFTWARE_UPDATES: {
        id: "softwareUpdates",
        value: "Обновления fmu-api",
        icon: "mdi mdi-file-document"
    },
    SERVER: {
        id: "serverSettings",
        value: "Настройки сервера",
        icon: "mdi mdi-server"
    },
    COUCHDB: {
        id: "couchDbSettings",
        value: "CouchDB",
        icon: "mdi mdi-database"
    },
    TELEGRAM_BOT_SETTINGS: {
        id: "telegramBotSettings",
        value: "Оповещения",
        icon: "mdi mdi-telegram"
    },
    LOGS: {
        id: "logs",
        value: "Логи работы",
        icon: "mdi mdi-file-log"
    },
    SERVICE: {
        id: "service",
        value: "Сервис",
        icon: "mdi mdi-toolbox"
    },
    INFO: {
        id: "information",
        value: "Информация",
        icon: "mdi mdi-information"
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
