/** Код 0 и тексты HttpClient — нет ответа сервиса, не статус ЧЗ. */
const NO_CONNECTION = /cancelled|canceled|connection|timeout|нет связи|недоступен/i;

export function formatGisMtStatus(obj, emptyLabel = "—") {
    const status = obj?.gisMtLastStatus || {};
    const code = status.code;
    if (code === null || code === undefined || code === "") {
        return emptyLabel;
    }

    if (!obj?.trueApiEnabled || isNoConnection(code, status.description)) {
        return emptyLabel;
    }

    const numeric = Number(code);
    const ok = numeric >= 200 && numeric < 300;
    const description = ok ? "" : (status.description || "");
    const text = `${code} ${description}`.trim()
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
    const color = ok ? "" : "color:#E74C3C;";
    return `<span style="${color}">${text}</span>`;
}

function isNoConnection(code, description) {
    if (Number(code) === 0) {
        return true;
    }

    return NO_CONNECTION.test(String(description || ""));
}
