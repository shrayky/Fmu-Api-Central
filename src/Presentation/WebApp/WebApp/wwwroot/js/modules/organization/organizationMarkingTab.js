import { CheckBox, Label } from "../../utils/ui.js";
import { copyText } from "../../utils/copyText.js";
import organizationService from "../../services/organizationService.js";

export function tokenStatusText(received, expired) {
    if (!received)
        return "Токен не получен";

    const until = tokenUntil(expired);
    return until ? `Получен, до ${until}` : "Получен, до";
}

export function markingBody(view, groups, trueApi = {}, token = {}) {
    const enabled = !!trueApi.enable;
    return {
        id: view.NAMES.trueApiTab,
        padding: 10,
        rows: [
            Label(view.NAMES.trueApiSection, view.LABELS.trueApiSection),
            {
                padding: { left: 20 },
                rows: [
                    CheckBox(view.LABELS.trueApiEnable, view.NAMES.enable, {
                        value: enabled,
                        on: {
                            onChange: (value) => setTokenButtonEnabled(view, value)
                        }
                    }),
                    {
                        cols: [
                            {
                                view: "button",
                                value: view.LABELS.LoadToken,
                                id: view.NAMES.loadToken,
                                width: 200,
                                disabled: !enabled,
                                click: () => loadToken(view)
                            },

                            {
                                view: "template",
                                id: view.NAMES.trueApiTokenStatus,
                                borderless: true,
                                autoheight: true,
                                template: tokenStatusHtml(token.received, token.expired)
                            },

                            {}
                        ]
                    }
                ]
            },
            { view: "label", label: view.LABELS.gisMtTab },
            groupsTable(view, groups)
        ]
    };
}

function showTokenStatus(view, received, expired) {
    const status = $$(view.NAMES.trueApiTokenStatus);
    if (!status)
        return;

    status.setHTML(tokenStatusHtml(received, expired));
}

function tokenStatusHtml(received, expired) {
    const color = received ? "inherit" : "#95A5A6";
    return `<div style="color:${color};white-space:normal;">${tokenStatusText(received, expired)}</div>`;
}

function tokenUntil(expired) {
    if (!expired)
        return "";

    const date = new Date(expired);
    if (Number.isNaN(date.getTime()))
        return "";

    return date.toLocaleString("ru-RU", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit"
    });
}

function setTokenButtonEnabled(view, enabled) {
    const button = $$(view.NAMES.loadToken);
    if (!button)
        return;

    if (enabled)
        button.enable();
    else
        button.disable();
}

async function loadToken(view) {
    const inn = String($$(view.NAMES.inn).getValue() || "").trim();
    if (!inn) {
        webix.message({ text: view.LABELS.invalidInnMessage, type: "error" });
        return;
    }

    const button = $$(view.NAMES.loadToken);
    if (button)
        button.disable();

    try {
        const data = await organizationService.getToken(inn);
        const token = data.token ?? data.Token ?? "";
        if (!token) {
            webix.message({ text: "Токен не получен", type: "error" });
            return;
        }

        showTokenStatus(view, true, data.expired ?? data.Expired);
        await copyText(token);
        webix.message("Токен получен и скопирован в буфер обмена");
    } catch (error) {
        const message = error.message || "Ошибка при получении токена";
        if (String(message).includes("Не найдена организация")) {
            webix.message({ text: view.LABELS.saveFirst, type: "error" });
            return;
        }

        webix.message({ text: message, type: "error" });
    } finally {
        setTokenButtonEnabled(view, !!$$(view.NAMES.enable).getValue());
    }
}

function groupsTable(view, groups) {
    return {
        view: "datatable",
        id: view.NAMES.gisMtGroupsTable,
        gravity: 1,
        scroll: "y",
        select: "row",
        columns: [
            { id: "code", header: view.LABELS.gisMtGroupCode, width: 80, sort: "int" },
            { id: "name", header: view.LABELS.gisMtGroupName, width: 160, sort: "string" },
            { id: "groupName", header: view.LABELS.gisMtGroupTitle, fillspace: true, sort: "string" }
        ],
        data: (groups || []).map((item, index) => ({
            id: item.name || item.code || index + 1,
            code: item.code,
            name: item.name || "",
            groupName: item.groupName || ""
        }))
    };
}
