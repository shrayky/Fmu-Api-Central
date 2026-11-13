import { Text } from "../../utils/ui.js";
import { newUuid } from "../../utils/uuid.js";
import organizationService from "../../services/organizationService.js";
import etrnService from "../../services/etrnService.js";
import { errorText, escapeHtml, fromDate, toDate } from "./organizationFormat.js";

// С этой даты ЭТрН обязательна.
const ETRN_INCOMING_FROM = "2026-09-01";
const OFD_API = "https://api.platformaofd.ru";

export function etrnBody(view) {
    return {
        id: view.NAMES.etrnTab,
        padding: 10,
        rows: [
            {
                cols: [
                    { view: "button", value: view.LABELS.etrnAdd, width: 140, click: () => addEtrnConnection(view) },
                    { view: "button", value: view.LABELS.etrnRemove, width: 140, click: () => removeSelectedEtrnConnection(view) },
                    {
                        view: "button",
                        id: view.NAMES.etrnCheck,
                        value: view.LABELS.etrnCheck,
                        width: 220,
                        click: () => checkEtrn(view)
                    },
                    {}
                ]
            },
            {
                view: "template",
                id: view.NAMES.etrnProbeStatus,
                height: 36,
                borderless: true,
                template: ""
            },
            {
                view: "datatable",
                id: view.NAMES.etrnConnections,
                gravity: 1,
                select: "row",
                editable: true,
                editaction: "click",
                columns: [
                    {
                        id: "enable",
                        header: "",
                        template: "{common.checkbox()}",
                        width: 40,
                        checkValue: true,
                        uncheckValue: false
                    },
                    { id: "name", header: view.LABELS.etrnName, fillspace: 1, editor: "text" },
                    {
                        id: "type",
                        header: view.LABELS.etrnType,
                        width: 180,
                        editor: "richselect",
                        options: [
                            { id: "saby", value: view.LABELS.etrnSaby },
                            { id: "ofd", value: view.LABELS.etrnOfd }
                        ]
                    },
                    {
                        id: "incomingFrom",
                        header: view.LABELS.etrnIncomingFrom,
                        width: 140,
                        editor: "date",
                        format: webix.Date.dateToStr("%d.%m.%Y")
                    },
                    {
                        id: "lastError",
                        header: view.LABELS.etrnError,
                        fillspace: 1.6,
                        template: (row) => row.lastError
                            ? `<span title="${escapeHtml(row.lastError)}">${escapeHtml(row.lastError)}</span>`
                            : ""
                    },
                    {
                        id: "remove",
                        header: "",
                        width: 40,
                        template: "<span class='webix_icon wxi-trash' title='Удалить'></span>"
                    }
                ],
                data: (view._loadedEtrn || []).map(connection => etrnRow(connection)),
                on: {
                    onSelectChange() {
                        showOfdFields(view);
                        showEdoField(view);
                    },
                    onAfterEditStop(state, editor) {
                        if (editor.column === "type") {
                            showOfdFields(view);
                            showEdoField(view);
                        }
                    },
                    onItemClick(cell) {
                        if (!cell || cell.column !== "remove")
                            return;

                        confirmRemoveEtrnConnection(view, cell.row);
                    }
                }
            },
            etrnSabyForm(view),
            etrnOfdForm(view)
        ]
    };
}

export function showEtrnConnections(view, connections) {
    const table = $$(view.NAMES.etrnConnections);
    if (!table)
        return;

    table.clearAll();
    table.parse((connections || []).map(connection => etrnRow(connection)));
    showOfdFields(view);
    showEdoField(view);
}

export function etrnConnectionsFromTable(view) {
    const table = $$(view.NAMES.etrnConnections);
    if (!table)
        return view._loadedEtrn || [];

    writeOfdFields(view);
    writeEdoField(view);
    return table.serialize().map(row => {
        const type = row.type === "ofd" ? "ofd" : "saby";
        return {
            id: row.id,
            type,
            name: String(row.name || "").trim(),
            enable: !!row.enable,
            subscriberId: type === "ofd" ? String(row.subscriberId || "").trim() : "",
            edoId: type === "saby" ? String(row.edoId || "").trim() : "",
            clientId: type === "ofd" ? String(row.clientId || "").trim() : "",
            apiUrl: type === "ofd" ? String(row.apiUrl || "").trim() : "",
            incomingFrom: fromDate(row.incomingFrom) || ETRN_INCOMING_FROM
        };
    });
}

/**
 * Ошибки опроса лежат отдельным документом и в карточку организации не приходят.
 */
export async function loadEtrnErrors(view) {
    const table = $$(view.NAMES.etrnConnections);
    if (!table)
        return;

    try {
        const organizations = await etrnService.filters();
        const organization = (organizations || []).find(item => item.id === view.elementId);
        if (!organization)
            return;

        const errors = new Map((organization.connections || []).map(item => [item.id, item.lastError || ""]));
        table.data.each(row => {
            row.lastError = errors.get(row.id) || "";
        });
        table.refresh();
    } catch {
        // Подсказка не критична: карточка уже показывает таблицу подключений.
    }
}

function etrnSabyForm(view) {
    return {
        id: view.NAMES.etrnSabyForm,
        view: "form",
        hidden: true,
        elements: [
            Text(view.LABELS.etrnEdoId, view.NAMES.etrnEdoId, "")
        ],
        on: {
            onChange() {
                writeEdoField(view);
            }
        }
    };
}

function showEdoField(view) {
    const form = $$(view.NAMES.etrnSabyForm);
    const table = $$(view.NAMES.etrnConnections);
    if (!form || !table)
        return;

    const row = table.getSelectedItem();
    if (!row || row.type === "ofd") {
        form.hide();
        return;
    }

    form.show();
    view._edoSilent = true;
    const field = $$(view.NAMES.etrnEdoId);
    if (field)
        field.setValue(row.edoId || "");
    view._edoSilent = false;
}

function writeEdoField(view) {
    if (view._edoSilent)
        return;

    const table = $$(view.NAMES.etrnConnections);
    const row = table?.getSelectedItem();
    if (!table || !row || row.type === "ofd")
        return;

    row.edoId = String($$(view.NAMES.etrnEdoId)?.getValue() || "");
    table.updateItem(row.id, row);
}

function etrnOfdForm(view) {
    return {
        id: view.NAMES.etrnOfdForm,
        view: "form",
        hidden: true,
        elements: [
            Text(view.LABELS.etrnSubscriber, view.NAMES.etrnOfdSubscriber, ""),
            Text("Адрес API", view.NAMES.etrnOfdApi, "", {
                placeholder: "https://api.platformaofd.ru",
                tooltip: "Подставить боевой адрес",
                ...ofdFieldIcon(view.NAMES.etrnOfdApi, () => OFD_API)
            }),
            {
                view: "template",
                autoheight: true,
                borderless: true,
                template: "Иконка подставляет боевой адрес API. Токен запрашивается сертификатом организации."
            }
        ],
        on: {
            onChange() {
                writeOfdFields(view);
            }
        }
    };
}

function ofdFieldIcon(name, value) {
    return {
        icon: "wxi-sync",
        click() {
            $$(name)?.setValue(value());
        }
    };
}

function showOfdFields(view) {
    const form = $$(view.NAMES.etrnOfdForm);
    const table = $$(view.NAMES.etrnConnections);
    if (!form || !table)
        return;

    const row = table.getSelectedItem();
    if (!row || row.type !== "ofd") {
        form.hide();
        return;
    }

    form.show();
    view._ofdSilent = true;
    setOfdField(view.NAMES.etrnOfdSubscriber, row.subscriberId);
    setOfdField(view.NAMES.etrnOfdApi, row.apiUrl);
    view._ofdSilent = false;
}

function setOfdField(name, value) {
    const field = $$(name);
    if (field)
        field.setValue(value || "");
}

function writeOfdFields(view) {
    if (view._ofdSilent)
        return;

    const table = $$(view.NAMES.etrnConnections);
    const row = table?.getSelectedItem();
    if (!table || !row || row.type !== "ofd")
        return;

    row.subscriberId = ofdValue(view.NAMES.etrnOfdSubscriber);
    row.apiUrl = ofdValue(view.NAMES.etrnOfdApi);
    table.updateItem(row.id, row);
}

function ofdValue(name) {
    return String($$(name)?.getValue() || "");
}

async function checkEtrn(view) {
    writeOfdFields(view);
    writeEdoField(view);
    const table = $$(view.NAMES.etrnConnections);
    const row = table?.getSelectedItem();
    if (!row) {
        setEtrnStatus(view, "Выберите подключение", true);
        return;
    }

    const button = $$(view.NAMES.etrnCheck);
    if (button)
        button.disable();
    setEtrnStatus(view, "Проверка подключения...", false);
    try {
        const result = await organizationService.checkEtrn(view.elementId, {
            id: row.id,
            type: row.type,
            name: row.name || "",
            subscriberId: row.subscriberId || "",
            clientId: row.clientId || "",
            apiUrl: row.apiUrl || "",
            certificateNumber: String($$(view.NAMES.digitalSignature)?.getValue() || ""),
            certificatePassword: String($$(view.NAMES.password)?.getValue() || "")
        });
        setEtrnStatus(view, result.message || "", !result.ok);
    } catch (error) {
        setEtrnStatus(view, errorText(error.message), true);
    } finally {
        if (button)
            button.enable();
    }
}

function setEtrnStatus(view, text, failed) {
    const status = $$(view.NAMES.etrnProbeStatus);
    if (!status)
        return;

    status.setHTML(`<div style="color:${failed ? "#9b2335" : "#0f6b4c"};white-space:normal;">${escapeHtml(text)}</div>`);
}

function etrnRow(connection) {
    const type = connection.type === "ofd" ? "ofd" : "saby";
    return {
        id: connection.id || newUuid(),
        name: connection.name || "",
        type,
        enable: !!connection.enable,
        subscriberId: type === "ofd" ? (connection.subscriberId || "") : "",
        edoId: type === "saby" ? (connection.edoId || "") : "",
        clientId: type === "ofd" ? (connection.clientId || "") : "",
        apiUrl: type === "ofd" ? (connection.apiUrl || "") : "",
        incomingFrom: toDate(connection.incomingFrom || ETRN_INCOMING_FROM),
        lastError: ""
    };
}

function addEtrnConnection(view) {
    const table = $$(view.NAMES.etrnConnections);
    if (!table)
        return;

    const id = newUuid();
    table.add({
        id,
        name: "",
        type: "saby",
        enable: true,
        edoId: "",
        incomingFrom: toDate(ETRN_INCOMING_FROM),
        lastError: ""
    });
    table.select(id);
    table.editCell(id, "name");
}

function removeSelectedEtrnConnection(view) {
    const table = $$(view.NAMES.etrnConnections);
    const selected = table?.getSelectedId();
    if (!selected) {
        webix.message({ text: "Выберите подключение", type: "error" });
        return;
    }

    confirmRemoveEtrnConnection(view, selected);
}

function confirmRemoveEtrnConnection(view, rowId) {
    const table = $$(view.NAMES.etrnConnections);
    if (!table || !table.exists(rowId))
        return;

    view._setOrganizationHotkeys(false);
    webix.confirm({
        title: "Вы уверены?",
        text: "Удалить подключение ЭТрН?",
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
