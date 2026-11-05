import { formatIsoMoment, formatUnixMoment } from './vetisFormat.js';

const STATUSES = {
    Waiting: { label: "Ждёт приёмки", css: "etrn-status-waiting" },
    Sending: { label: "Отправляется", css: "etrn-status-sending" },
    Accepted: { label: "Получен", css: "etrn-status-accepted" },
    Rejected: { label: "Не принят", css: "etrn-status-rejected" },
    Closed: { label: "Закрыт", css: "etrn-status-closed" },
};

const METHODS = {
    "СБИС.СписокИзменений": "Список изменений",
    "СБИС.ПрочитатьДокумент": "Чтение документа",
    "СБИС.ЗаписатьВложение": "Запись вложения",
    "СБИС.ПодготовитьДействие": "Подготовка действия",
    "СБИС.ВыполнитьДействие": "Выполнение действия",
};

export function etrnStatusLabel(row) {
    return row?.statusLabel || STATUSES[row?.status]?.label || row?.status || "";
}

export function etrnStatusCss(status) {
    return STATUSES[status]?.css ?? "";
}

export function etrnMethodLabel(method) {
    return METHODS[method] ?? method ?? "";
}

export function etrnResponseText(row) {
    if (row?.error) {
        return "Ошибка";
    }

    return row?.completed ? "Получен" : "Ошибка";
}

export function etrnOperatorLabel(name, type) {
    const text = String(name ?? "").trim();
    return text || (String(type ?? "").toLowerCase() === "saby" ? "Saby" : String(type ?? ""));
}

export { formatIsoMoment, formatUnixMoment };
