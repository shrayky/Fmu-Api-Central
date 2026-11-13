/** В дате начала берётся календарный день, время в строке отбрасывается. */
export function toDate(value) {
    if (!value)
        return "";

    if (value instanceof Date)
        return value;

    const match = String(value).match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (!match)
        return "";

    return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
}

export function fromDate(value) {
    if (!value)
        return "";

    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime()))
        return "";

    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${date.getFullYear()}-${month}-${day}`;
}

export function escapeHtml(text) {
    return String(text || "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

/** Сервер иногда отдаёт текст ошибки JSON-строкой в кавычках. */
export function errorText(raw) {
    const text = String(raw || "").trim();
    if (text.startsWith("\"") && text.endsWith("\"")) {
        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    }

    return text;
}
