/** Шестерёнка как в FC: в наборе webix нет иконки настроек. */
export function formatInstanceName(obj) {
    const name = escapeHtml(obj?.name || "");
    let html = name;

    if (obj?.settingsModified) {
        html += ' <span style="color:#FF9800;font-size:18px;margin-left:8px;vertical-align:middle;line-height:1;" title="Выгрузка настроек">⚙</span>';
    }

    if (obj?.forcedUpdateId) {
        html += ' <span class="webix_icon wxi-download" style="color: #0d6efd;" title="Принудительная установка"></span>';
    }

    return html;
}

function escapeHtml(value) {
    return value
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");
}
