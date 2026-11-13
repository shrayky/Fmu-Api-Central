import { copyText } from "../../utils/copyText.js";
import organizationService from "../../services/organizationService.js";
import { errorText, escapeHtml, toDate } from "./organizationFormat.js";

export function enterprisesBody(view) {
    return {
        id: view.NAMES.vetisEnterprisesTab,
        padding: 10,
        rows: [
            {
                cols: [
                    { view: "button", value: view.LABELS.enterprises, width: 190, click: () => probe(view, "enterprises") },
                    { view: "button", value: view.LABELS.selectAll, width: 150, click: () => setEnterpriseSelection(view, true) },
                    { view: "button", value: view.LABELS.clearAll, width: 140, click: () => setEnterpriseSelection(view, false) },
                    {}
                ]
            },
            enterprisesTable(view)
        ],
        on: {
            onViewShow() {
                const table = $$(view.NAMES.vetisEnterprises);
                if (table)
                    table.resize();
            }
        }
    };
}

export function showEnterprises(view, rows) {
    const table = $$(view.NAMES.vetisEnterprises);
    if (!table)
        return;

    table.clearAll();
    table.parse((rows || []).map(row => ({
        id: row.guid,
        guid: row.guid || "",
        name: row.name || "",
        address: row.address || "",
        presentation: row.presentation || "",
        incomingFrom: toDate(row.incomingFrom),
        loadedIncomingFrom: row.incomingFrom || "",
        participates: !!row.participates
    })));
}

export function openLetter(view) {
    const opened = $$(view.NAMES.vetisLetterWindow);
    if (opened) {
        opened.show();
        $$(view.NAMES.vetisLetter)?.focus();
        return;
    }

    // Иначе Esc и Alt+Enter карточки срабатывают вместе с окном письма.
    view._setOrganizationHotkeys(false);
    webix.ui({
        view: "window",
        id: view.NAMES.vetisLetterWindow,
        modal: true,
        move: true,
        position: "center",
        width: Math.min(window.innerWidth * 0.8, 640),
        head: {
            view: "toolbar",
            elements: [
                { view: "label", label: view.LABELS.letterTitle },
                {
                    view: "icon",
                    icon: "wxi-close",
                    click: () => closeLetter(view)
                }
            ]
        },
        on: {
            onDestruct() {
                view._setOrganizationHotkeys(true);
            }
        },
        body: {
            padding: 10,
            rows: [
                {
                    view: "textarea",
                    id: view.NAMES.vetisLetter,
                    label: view.LABELS.letter,
                    labelPosition: "top",
                    height: 220
                },
                {
                    view: "template",
                    id: view.NAMES.vetisLetterStatus,
                    autoheight: true,
                    borderless: true,
                    hidden: true,
                    template: ""
                },
                {
                    padding: { top: 10 },
                    cols: [
                        {
                            view: "button",
                            value: view.LABELS.fillLetter,
                            width: 180,
                            css: "webix_primary",
                            hotkey: "alt+enter",
                            click: () => applyLetter(view)
                        },
                        {
                            view: "button",
                            value: view.LABELS.cancelButton,
                            width: 140,
                            hotkey: "esc",
                            click: () => closeLetter(view)
                        },
                        {}
                    ]
                }
            ]
        }
    }).show();
    webix.delay(() => $$(view.NAMES.vetisLetter)?.focus());
}

export function closeLetter(view) {
    const windowView = $$(view.NAMES.vetisLetterWindow);
    if (windowView)
        windowView.close();
}

export async function probe(view, action) {
    setStatus(view, "Запрос к Ветис...", false);
    try {
        const payload = vetisPayload(view);
        const result = action === "enterprises"
            ? await organizationService.listVetisEnterprises(view.elementId, payload)
            : await organizationService.checkVetis(view.elementId, payload);
        if (action === "enterprises" && result.ok) {
            showEnterprises(view, result.enterprises || []);
            const tabs = $$(view.NAMES.vetisTabs);
            if (tabs)
                tabs.setValue(view.NAMES.vetisEnterprisesTab);
        }

        setStatus(view, result.message || "", !result.ok);
    } catch (error) {
        setStatus(view, errorText(error.message), true);
    }
}

function enterprisesTable(view) {
    return {
        view: "datatable",
        id: view.NAMES.vetisEnterprises,
        gravity: 1,
        scroll: "y",
        editable: true,
        editaction: "click",
        columns: [
            {
                id: "participates",
                header: "",
                template: "{common.checkbox()}",
                width: 40,
                checkValue: true,
                uncheckValue: false
            },
            { id: "presentation", header: view.LABELS.presentation, fillspace: 1, editor: "text" },
            { id: "address", header: view.LABELS.address, fillspace: 1.4 },
            {
                id: "incomingFrom",
                header: view.LABELS.rowDate,
                width: 140,
                editor: "date",
                format: webix.Date.dateToStr("%d.%m.%Y")
            },
            {
                id: "remove",
                header: "",
                width: 40,
                template: "<span class='webix_icon wxi-trash' title='Удалить'></span>"
            }
        ],
        on: {
            onCheck(rowId, column, state) {
                if (column !== "participates" || !state)
                    return;

                fillEmptyDate(view, this.getItem(rowId), this);
            },
            onItemClick(cell) {
                if (!cell)
                    return;

                if (cell.column === "remove") {
                    confirmRemoveEnterprise(view, this, cell.row);
                    return;
                }

                if (cell.column !== "address")
                    return;

                const row = this.getItem(cell.row);
                if (row?.guid)
                    copyGuid(row.guid);
            }
        }
    };
}

function confirmRemoveEnterprise(view, table, rowId) {
    const row = table.getItem(rowId);
    const caption = confirmText(row?.presentation || "площадку");
    view._setOrganizationHotkeys(false);
    webix.confirm({
        title: "Вы уверены?",
        text: `Удалить «${caption}» из списка?`,
        ok: "Да",
        cancel: "Нет"
    }).then(() => {
        if (table.exists(rowId))
            table.remove(rowId);
    }).catch(() => {
        // «Нет» отклоняет промис webix.confirm.
    }).finally(() => {
        view._setOrganizationHotkeys(true);
    });
}

function confirmText(value) {
    return String(value || "площадку").trim()
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;") || "площадку";
}

async function applyLetter(view) {
    const text = String($$(view.NAMES.vetisLetter)?.getValue() || "");
    try {
        const parsed = await organizationService.parseVetisLetter(text);
        setIfPresent(view.NAMES.apiUser, parsed.login);
        setIfPresent(view.NAMES.apiPassword, parsed.password);
        setIfPresent(view.NAMES.apiKey, parsed.apiKey);
        setIfPresent(view.NAMES.issuerId, parsed.issuerId);
        const found = ["login", "password", "apiKey", "issuerId"].filter(name => parsed[name]);
        if (!found.length) {
            setLetterStatus(view, "В письме нет Login, Password, APIKey и IssuerID.", true);
            return;
        }

        setStatus(view, "Заполнено из письма. Логин оператора в письме не приходит.", false);
        closeLetter(view);
    } catch (error) {
        setLetterStatus(view, error.message || "Не удалось разобрать письмо", true);
    }
}

function setLetterStatus(view, text, failed) {
    const status = $$(view.NAMES.vetisLetterStatus);
    if (!status)
        return;

    status.define("template", `<div style="color:${failed ? "#9b2335" : "#0f6b4c"};white-space:normal;">${escapeHtml(text)}</div>`);
    status.show();
    status.resize();
}

function setIfPresent(id, value) {
    if (!value)
        return;

    const field = $$(id);
    if (field)
        field.setValue(value);
}

function vetisPayload(view) {
    return {
        apiKey: String($$(view.NAMES.apiKey)?.getValue() || ""),
        basicLogin: String($$(view.NAMES.apiUser)?.getValue() || ""),
        basicPassword: String($$(view.NAMES.apiPassword)?.getValue() || ""),
        issuerId: String($$(view.NAMES.issuerId)?.getValue() || ""),
        initiatorLogin: String($$(view.NAMES.operatorLogin)?.getValue() || "")
    };
}

function setEnterpriseSelection(view, selected) {
    const table = $$(view.NAMES.vetisEnterprises);
    if (!table)
        return;

    table.data.each(row => {
        row.participates = selected;
        if (selected && !row.incomingFrom) {
            const general = $$(view.NAMES.vetisIncomingFrom)?.getValue();
            if (general)
                row.incomingFrom = general;
        }
    });
    table.refresh();
}

function fillEmptyDate(view, row, table) {
    if (!row || row.incomingFrom)
        return;

    const general = $$(view.NAMES.vetisIncomingFrom)?.getValue();
    if (!general)
        return;

    row.incomingFrom = general;
    if (table && row.id)
        table.updateItem(row.id, row);
}

async function copyGuid(guid) {
    try {
        await copyText(guid);
        webix.message("GUID скопирован");
    } catch (error) {
        webix.message({ text: error.message || "Не удалось скопировать GUID", type: "error" });
    }
}

function setStatus(view, text, failed) {
    const status = $$(view.NAMES.vetisStatus);
    if (!status)
        return;

    status.setHTML(`<div style="color:${failed ? "#9b2335" : "#0f6b4c"};white-space:normal;">${escapeHtml(text)}</div>`);
}
