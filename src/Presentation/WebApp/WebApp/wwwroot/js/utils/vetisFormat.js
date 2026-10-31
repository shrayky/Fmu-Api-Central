const INCOMING_STATUSES = {
    accepted: { label: "Принят", css: "vetis-status-accepted" },
    partial: { label: "Принят частично", css: "vetis-status-partial" },
    notAccepted: { label: "Не принят", css: "vetis-status-not-accepted" },
    rejected: { label: "Отклонён", css: "vetis-status-rejected" },
};

const OPERATIONS = {
    GetVetDocumentChangesList: "Загрузка ВСД",
    ProcessIncomingConsignment: "Гашение",
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

export function incomingDocumentLabel(row) {
    if (row?.loose)
        return "Без документа";

    return String(row?.number ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;");
}

export function vetisCountLabel(name, count) {
    const total = Number(count) || 0;
    return total > 0 ? `${name} — ${total}` : String(name ?? "");
}

export function editedActualVolume(incoming, edited) {
    const volume = parseVolume(incoming);
    const text = String(edited ?? "").trim();
    const fact = parseVolume(text);
    if (!text || volume == null || fact == null || fact < 0 || fact > volume)
        return { ok: false };

    return { ok: true, value: text.replace(",", ".") };
}

export function canQuenchRows(vsds) {
    return (vsds ?? []).some((vsd) => String(vsd?.status ?? "").toUpperCase() === "CONFIRMED");
}

export function sameIncomingDocument(openedId, requestId) {
    return openedId != null && openedId === requestId;
}

export function incomingWindowClosable(openedId, busyId) {
    return busyId == null || busyId !== openedId;
}

export function quenchStageText({ index = 0, total = 0, issueNumber = "" } = {}) {
    const count = Number(total) || 0;
    const current = Number(index) || 0;
    const lines = [count > 0 ? `Гашение ${current} из ${count}` : "Гашение"];
    const number = String(issueNumber ?? "").trim();
    if (number)
        lines.push(`ВСД ${number}`);

    lines.push("Отправка в Меркурий");
    return lines.join("\n");
}

export function quenchSummary({ quenched = 0, skipped = 0, errors = [] } = {}) {
    const parts = [`Погашено ${quenched}`];
    if (skipped)
        parts.push(`пропущено ${skipped}`);
    if (errors.length)
        parts.push(errors.map((item) => item.message).filter(Boolean).join("; "));

    return parts.join(". ");
}

function parseVolume(value) {
    const number = Number(String(value ?? "").trim().replace(",", "."));
    return Number.isFinite(number) ? number : null;
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
