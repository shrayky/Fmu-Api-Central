export const PERIOD_PRESETS = {
    today: "today",
    yesterday: "yesterday",
    week: "week",
    month: "month",
};

function formatDateParam(date) {
    const year = date.getFullYear();
    const month = (date.getMonth() + 1).toString().padStart(2, "0");
    const day = date.getDate().toString().padStart(2, "0");

    return `${year}-${month}-${day}`;
}

function startOfDay(date) {
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

/// Возвращает диапазон дат для быстрого выбора периода.
export function getPresetDates(preset) {
    const today = startOfDay(new Date());

    switch (preset) {
        case PERIOD_PRESETS.yesterday: {
            const yesterday = new Date(today);
            yesterday.setDate(yesterday.getDate() - 1);
            return {
                dateFrom: formatDateParam(yesterday),
                dateTo: formatDateParam(yesterday),
            };
        }
        case PERIOD_PRESETS.week: {
            const dateFrom = new Date(today);
            dateFrom.setDate(dateFrom.getDate() - 6);
            return {
                dateFrom: formatDateParam(dateFrom),
                dateTo: formatDateParam(today),
            };
        }
        case PERIOD_PRESETS.month: {
            const dateFrom = new Date(today);
            dateFrom.setDate(1);
            return {
                dateFrom: formatDateParam(dateFrom),
                dateTo: formatDateParam(today),
            };
        }
        case PERIOD_PRESETS.today:
        default:
            return {
                dateFrom: formatDateParam(today),
                dateTo: formatDateParam(today),
            };
    }
}

export function getDefaultFilters() {
    return {
        inn: "",
        periodPreset: PERIOD_PRESETS.today,
        ...getPresetDates(PERIOD_PRESETS.today),
    };
}

export const PRESET_LABELS = {
    [PERIOD_PRESETS.today]: "Сегодня",
    [PERIOD_PRESETS.yesterday]: "Вчера",
    [PERIOD_PRESETS.week]: "Неделя",
    [PERIOD_PRESETS.month]: "Месяц",
};
