import organizationService from "../../services/organizationService.js";
import { newUuid } from "../../utils/uuid.js";
import {
    salePointFieldError,
    salePointLabel,
    salePointsFromAddresses,
    salePointsToPayload,
    salePointsToRows
} from "../../utils/salePointFormat.js";
import { errorText, escapeHtml } from "./organizationFormat.js";

export function salePointsBody(view) {
    return {
        id: view.NAMES.salePointsTab,
        padding: 10,
        rows: [
            {
                cols: [
                    {
                        view: "button",
                        value: view.LABELS.salePointAdd,
                        width: 140,
                        click: () => openSalePointWindow(view, null)
                    },
                    {
                        view: "button",
                        value: view.LABELS.salePointImport,
                        width: 160,
                        click: () => openImportWindow(view)
                    },
                    {}
                ]
            },
            salePointsTable(view)
        ],
        on: {
            onViewShow() {
                $$(view.NAMES.salePointsTable)?.resize();
            }
        }
    };
}

export function showSalePoints(view, points) {
    const table = $$(view.NAMES.salePointsTable);
    if (!table)
        return;

    table.clearAll();
    table.parse(salePointsToRows(points || []));
}

export function salePointsFromTable(view) {
    const table = $$(view.NAMES.salePointsTable);
    return table ? salePointsToPayload(table.serialize()) : [];
}

export function closeSalePointWindow(view) {
    $$(view.NAMES.salePointWindow)?.close();
}

export function closeImportWindow(view) {
    $$(view.NAMES.salePointImportWindow)?.close();
}

export function openSalePointWindow(view, rowId) {
    const table = $$(view.NAMES.salePointsTable);
    const row = rowId && table ? table.getItem(rowId) : null;
    const aliases = (row?.aliases || []).map((alias, index) => ({
        id: alias.uid || `alias-${index}`,
        uid: alias.uid || "",
        address: alias.address || ""
    }));

    $$(view.NAMES.salePointWindow)?.destructor();
    // Иначе Esc и Alt+Enter окна срабатывают вместе с карточкой.
    view._setOrganizationHotkeys(false);

    webix.ui({
        view: "window",
        id: view.NAMES.salePointWindow,
        modal: true,
        move: true,
        resize: true,
        position: "center",
        width: Math.min(window.innerWidth - 40, 760),
        height: Math.min(window.innerHeight - 20, 680),
        head: {
            view: "toolbar",
            elements: [
                { view: "label", label: row ? view.LABELS.salePointEdit : view.LABELS.salePointAdd },
                { view: "icon", icon: "wxi-close", click: () => closeSalePointWindow(view) }
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
                    view: "text",
                    id: view.NAMES.salePointName,
                    label: view.LABELS.salePointName,
                    labelPosition: "top",
                    attributes: { maxlength: 150 },
                    value: row?.name || ""
                },
                {
                    view: "text",
                    id: view.NAMES.salePointAddress,
                    label: view.LABELS.salePointAddress,
                    labelPosition: "top",
                    attributes: { maxlength: 250 },
                    value: row?.address || ""
                },
                {
                    view: "text",
                    id: view.NAMES.salePointFias,
                    label: view.LABELS.salePointFias,
                    labelPosition: "top",
                    attributes: { maxlength: 36 },
                    value: row?.fiasId || ""
                },
                {
                    cols: [
                        {
                            view: "button",
                            value: view.LABELS.salePointAliasAdd,
                            width: 200,
                            click: () => $$(view.NAMES.salePointAliases)?.add({ id: newUuid(), uid: "", address: "" })
                        },
                        {}
                    ]
                },
                {
                    view: "datatable",
                    id: view.NAMES.salePointAliases,
                    editable: true,
                    editaction: "click",
                    gravity: 1,
                    minHeight: 200,
                    columns: [
                        { id: "address", header: view.LABELS.salePointAliasesHeader, fillspace: 1, editor: "text" },
                        {
                            id: "remove",
                            header: "",
                            width: 40,
                            template: "<span class='webix_icon wxi-trash' title='Удалить'></span>"
                        },
                        {}
                    ],
                    data: aliases,
                    on: {
                        onItemClick(cell) {
                            if (cell?.column === "remove")
                                this.remove(cell.row);
                        },
                        onBeforeEditStop(values) {
                            if (String(values.value || "").trim().length <= 250)
                                return;

                            webix.message({ text: "Адрес синонима: не больше 250 символов", type: "error" });
                            return false;
                        }
                    }
                },
                {
                    view: "checkbox",
                    id: view.NAMES.salePointClosed,
                    label: view.LABELS.salePointClosed,
                    labelPosition: "left",
                    value: row?.closed ? 1 : 0
                },
                {
                    padding: { top: 10 },
                    cols: [
                        {
                            view: "button",
                            value: view.LABELS.salePointOk,
                            width: 160,
                            css: "webix_primary",
                            hotkey: "alt+enter",
                            click: () => applySalePoint(view, rowId)
                        },
                        {
                            view: "button",
                            value: view.LABELS.cancelButton,
                            width: 140,
                            hotkey: "esc",
                            click: () => closeSalePointWindow(view)
                        },
                        {}
                    ]
                }
            ]
        }
    }).show();
}

export function openImportWindow(view) {
    $$(view.NAMES.salePointImportWindow)?.destructor();
    view._setOrganizationHotkeys(false);

    webix.ui({
        view: "window",
        id: view.NAMES.salePointImportWindow,
        modal: true,
        move: true,
        resize: true,
        position: "center",
        width: Math.min(window.innerWidth - 40, 760),
        height: Math.min(window.innerHeight - 20, 560),
        head: {
            view: "toolbar",
            elements: [
                { view: "label", label: view.LABELS.salePointImportTitle },
                { view: "icon", icon: "wxi-close", click: () => closeImportWindow(view) }
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
                    id: view.NAMES.salePointImport,
                    label: view.LABELS.salePointImportAddresses,
                    labelPosition: "top",
                    gravity: 1,
                    minHeight: 200,
                    placeholder: view.LABELS.salePointImportHint
                },
                {
                    padding: { top: 10 },
                    cols: [
                        {
                            view: "button",
                            value: view.LABELS.salePointAdd,
                            width: 160,
                            css: "webix_primary",
                            hotkey: "alt+enter",
                            click: () => applyImport(view)
                        },
                        {
                            view: "button",
                            value: view.LABELS.cancelButton,
                            width: 140,
                            hotkey: "esc",
                            click: () => closeImportWindow(view)
                        }
                    ]
                }
            ]
        }
    }).show();
}

function applyImport(view) {
    const table = $$(view.NAMES.salePointsTable);
    const text = String($$(view.NAMES.salePointImport)?.getValue() || "");
    if (!text.trim()) {
        webix.message({ text: view.LABELS.salePointImportEmpty, type: "error" });
        return;
    }

    const taken = [];
    table?.serialize().forEach((row) => {
        taken.push(row.address);
        (row.aliases || []).forEach((alias) => taken.push(alias.address));
    });
    const loaded = salePointsFromAddresses(text, taken);
    const rows = loaded.points.map((row) => ({ id: newUuid(), ...row }));
    if (!table) {
        closeImportWindow(view);
        return;
    }

    if (rows.length) {
        // add, а не parse(rows, true): второй аргумент Webix трактует как формат, а не «дописать».
        rows.forEach((row) => table.add(row));
    }

    closeImportWindow(view);
    if (loaded.error)
        webix.message({ text: loaded.error, type: "error" });
    else if (!rows.length)
        webix.message(view.LABELS.salePointImportDuplicate);
}

function applySalePoint(view, rowId) {
    // Alt+Enter мог сработать прямо в редакторе синонима: без остановки правка не попадёт в serialize.
    $$(view.NAMES.salePointAliases)?.editStop?.();

    const aliases = ($$(view.NAMES.salePointAliases)?.serialize() || [])
        .map((alias) => ({ uid: String(alias.uid || "").trim(), address: String(alias.address || "").trim() }))
        .filter((alias) => alias.address.length > 0);
    const draft = {
        name: String($$(view.NAMES.salePointName)?.getValue() || ""),
        address: String($$(view.NAMES.salePointAddress)?.getValue() || ""),
        fiasId: String($$(view.NAMES.salePointFias)?.getValue() || ""),
        aliases
    };
    const error = salePointFieldError(draft);
    if (error) {
        webix.message({ text: error, type: "error" });
        return;
    }

    const table = $$(view.NAMES.salePointsTable);
    if (!table)
        return;

    const updated = {
        id: rowId || newUuid(),
        uid: rowId ? String(table.getItem(rowId)?.uid || "") : "",
        name: draft.name.trim(),
        address: draft.address.trim(),
        fiasId: draft.fiasId.trim(),
        closed: !!$$(view.NAMES.salePointClosed)?.getValue(),
        aliases
    };

    if (rowId)
        table.updateItem(rowId, updated);
    else
        table.add(updated);

    closeSalePointWindow(view);
}

function salePointsTable(view) {
    return {
        view: "datatable",
        id: view.NAMES.salePointsTable,
        gravity: 1,
        // Колонки с fillspace должны ужиматься под ширину вкладки, а не давать горизонтальную прокрутку.
        scrollX: false,
        select: "row",
        columns: [
            {
                id: "name",
                header: view.LABELS.salePointName,
                fillspace: 1,
                template: (row) => escapeHtml(salePointLabel(row))
            },
            {
                id: "address",
                header: view.LABELS.salePointAddress,
                fillspace: 1.4,
                template: (row) => escapeHtml(row.address || "")
            },
            {
                id: "remove",
                header: "",
                width: 40,
                template: "<span class='webix_icon wxi-trash' title='Удалить'></span>"
            }
        ],
        on: {
            onItemClick(cell) {
                if (cell?.column === "remove")
                    confirmRemove(view, this, cell.row);
            },
            onItemDblClick(cell) {
                if (cell && cell.column !== "remove")
                    openSalePointWindow(view, cell.row);
            }
        }
    };
}

async function confirmRemove(view, table, rowId) {
    const row = table.getItem(rowId);
    if (!row)
        return;

    const uid = String(row.uid || "").trim();
    if (!uid) {
        table.remove(rowId);
        return;
    }

    let used = false;
    try {
        const usage = await organizationService.salePointUsage(view.elementId, uid);
        used = !!usage?.used;
    } catch (error) {
        webix.message({ text: errorText(error.message), type: "error" });
        return;
    }

    if (!used) {
        table.remove(rowId);
        return;
    }

    view._setOrganizationHotkeys(false);
    webix.confirm({
        title: "Точка используется",
        text: `Точку «${escapeHtml(row.name || "")}» используют документы. Закрыть точку?`,
        ok: "Закрыть",
        cancel: "Отмена"
    }).then(() => {
        row.closed = true;
        table.updateItem(rowId, row);
    }).catch(() => {
        // «Отмена» отклоняет промис webix.confirm.
    }).finally(() => {
        view._setOrganizationHotkeys(true);
    });
}
