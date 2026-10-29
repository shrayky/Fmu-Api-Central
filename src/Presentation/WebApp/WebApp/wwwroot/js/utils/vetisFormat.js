const INCOMING_STATUSES = {
    accepted: { label: "Принят", css: "vetis-status-accepted" },
    partial: { label: "Принят частично", css: "vetis-status-partial" },
    notAccepted: { label: "Не принят", css: "vetis-status-not-accepted" },
    rejected: { label: "Отклонён", css: "vetis-status-rejected" },
};

const OPERATIONS = {
    GetVetDocumentChangesList: "Загрузка ВСД",
    GetBusinessEntityUser: "Проверка соединения",
    GetActivityLocationList: "Список предприятий",
};

const VSD_STATUSES = {
    CONFIRMED: "Оформлен",
    UTILIZED: "Погашен",
    FINALIZED: "Погашен",
    WITHDRAWN: "Аннулирован",
    CANCELED: "Аннулирован",
};

const ISO_MOMENT = /^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2}))?/;

const pad = (value) => String(value).padStart(2, "0");

export function incomingStatusLabel(status) {
    return INCOMING_STATUSES[status]?.label ?? status ?? "";
}

export function incomingStatusCss(status) {
    return INCOMING_STATUSES[status]?.css ?? "";
}

export function operationLabel(operation) {
    return OPERATIONS[operation] ?? operation ?? "";
}

export function vsdStatusLabel(status) {
    return VSD_STATUSES[status] ?? status ?? "";
}

const INVALID_FILE_CHAR = /[\\/:*?"<>|]/g;

export function vsdXmlFileName(vsd) {
    const number = String(vsd?.issueNumber ?? "").trim();
    const uuid = String(vsd?.uuid ?? "").trim();
    const base = (number || uuid || "vsd").replace(INVALID_FILE_CHAR, "_");
    return `${base}.xml`;
}

export function vsdCountText({ acceptedCount = 0, vsdCount = 0 }) {
    return `${acceptedCount} из ${vsdCount}`;
}

export function requestStateText({ hasResponse, error }) {
    if (error) {
        return "Ошибка";
    }

    return hasResponse ? "Получен" : "Нет ответа";
}

export function incomingReceivedLabel(row) {
    return formatIsoMoment(row?.receivedAt);
}

export function incomingDocumentLabel(row) {
    if (row?.loose)
        return "Без документа";

    return String(row?.number ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;");
}

export function incomingFromForSave(loaded, pickerDay) {
    const day = String(pickerDay ?? "").trim();
    const stored = String(loaded ?? "").trim();
    if (!day)
        return "";

    if (stored.slice(0, 10) === day && stored.includes("T"))
        return stored;

    return day;
}

export function formatUnixMoment(seconds) {
    if (!seconds) {
        return "";
    }

    const moment = new Date(seconds * 1000);
    const date = `${pad(moment.getDate())}.${pad(moment.getMonth() + 1)}.${moment.getFullYear()}`;
    return `${date} ${pad(moment.getHours())}:${pad(moment.getMinutes())}`;
}

/** Дату ВетИС показываем как пришла, без пересчёта часового пояса: смещение в строке относится к площадке, а не к браузеру. */
export function formatIsoMoment(value) {
    const match = ISO_MOMENT.exec(value ?? "");
    if (!match) {
        return value ?? "";
    }

    const [, year, month, day, hours, minutes] = match;
    const date = `${day}.${month}.${year}`;
    return hours === undefined ? date : `${date} ${hours}:${minutes}`;
}
