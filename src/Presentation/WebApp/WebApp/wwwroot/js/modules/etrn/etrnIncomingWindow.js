import etrnService from '../../services/etrnService.js';
import { etrnOperatorLabel, etrnStatusLabel, formatIsoMoment } from '../../utils/etrnFormat.js';
import { incomingWindowClosable } from '../../utils/vetisFormat.js';
import { infoTemplate, openVetisWindow } from '../vetis/vetisWindow.js';

const WINDOW_ID = "etrnIncomingWindow";
const TABLE_ID = "etrnIncomingLines";
const INFO_ID = "etrnIncomingInfo";
const COMMENT_ID = "etrnIncomingComment";
const ERROR_ID = "etrnIncomingError";
const ACCEPT_ID = "etrnIncomingAccept";
const REJECT_ID = "etrnIncomingReject";

let openedId = null;
let busyId = null;

export async function openEtrnIncomingWindow(row, { onAccepted } = {}) {
    let document;
    try {
        document = await etrnService.incomingDocument(row.id);
    } catch (error) {
        webix.message({ text: error.message, type: "error" });
        return;
    }

    openedId = document.id;
    busyId = null;
    let pendingCell = null;

    const dialog = openVetisWindow({        id: WINDOW_ID,
        title: `Документ № ${document.number} от ${formatIsoMoment(document.date)}`,
        fill: true,
        canClose: () => incomingWindowClosable(openedId, busyId),
        body: {
            rows: [
                { view: "template", id: INFO_ID, borderless: true, height: 200, template: infoHtml(document) },
                {
                    view: "toolbar",
                    elements: [
                        {
                            view: "button",
                            id: ACCEPT_ID,
                            value: "Подписать приёмку",
                            width: 190,
                            css: "webix_primary",
                            disabled: !document.canDecide,
                            click: () => send("sign"),
                        },
                        {
                            view: "button",
                            id: REJECT_ID,
                            value: "Отказать",
                            width: 140,
                            disabled: !document.canDecide,
                            click: () => send("reject"),
                        },
                        {},
                    ],
                },
                { view: "template", id: ERROR_ID, borderless: true, height: 34, template: "" },
                {
                    view: "datatable",
                    id: TABLE_ID,
                    editable: document.canDecide,
                    editaction: "click",
                    gravity: 1,
                    columns: [
                        { id: "name", header: "Наименование", fillspace: true },
                        { id: "places", header: "Мест", width: 80 },
                        { id: "factPlaces", header: "Факт мест", width: 110, editor: "text" },
                        { id: "grossWeight", header: "Масса брутто", width: 130 },
                        { id: "factWeight", header: "Факт массы", width: 120, editor: "text" },
                        { id: "comment", header: "Комментарий строки", width: 300, editor: "text" },
                    ],
                    data: rows(document),
                    on: {
                        onItemClick(cell) {
                            pendingCell = cell;
                        },
                        onBeforeEditStart(id) {
                            const cell = pendingCell;
                            pendingCell = null;
                            if (!document.canDecide)
                                return false;
                            if (!cell || cell.row !== id)
                                return true;

                            const line = this.getItem(id);
                            if (cell.column === "factPlaces")
                                return !!line?.editablePlaces;
                            if (cell.column === "factWeight")
                                return !!line?.editableWeight;
                            return true;
                        },
                    },
                },
                {
                    view: "textarea",
                    id: COMMENT_ID,
                    label: "Общий комментарий",
                    labelPosition: "top",
                    height: 78,
                    readonly: !document.canDecide,
                    value: document.generalComment || "",
                },
            ],
        },
    });

    if (!document.canDecide)
        setError(document.lastError);

    async function send(intent) {
        if (busyId)
            return;

        const table = $$(TABLE_ID);
        const lines = (table?.serialize() ?? []).map((line) => ({
            index: line.index,
            factPlaces: Number.isInteger(line.factPlaces) ? line.factPlaces : parseInt(line.factPlaces, 10),
            factWeight: String(line.factWeight ?? "").trim(),
            comment: String(line.comment ?? "").trim(),
        }));

        busyId = document.id;
        setError("");
        dialog.disable();
        showStage(dialog, `Отправка в ${etrnOperatorLabel(document.operatorName, "saby")}`);

        try {
            const updated = await etrnService.accept(document.id, {
                intent,
                comment: String($$(COMMENT_ID)?.getValue() ?? ""),
                lines: lines.map((line) => ({
                    ...line,
                    factPlaces: Number.isNaN(line.factPlaces) ? null : line.factPlaces,
                })),
            });
            webix.message(`Титул отправлен: ${etrnStatusLabel(updated)}`);
            applyDocument(updated);
            if (onAccepted)
                await onAccepted();
        } catch (error) {
            setError(errorText(error.message));
        } finally {
            busyId = null;
            hideStage(dialog);
            if ($$(WINDOW_ID))
                dialog.enable();
        }
    }

    function applyDocument(updated) {
        document = updated;
        const info = $$(INFO_ID);
        if (info) {
            info.define("template", infoHtml(updated));
            info.refresh();
        }

        const table = $$(TABLE_ID);
        if (table) {
            table.define("editable", !!updated.canDecide);
            table.clearAll();
            table.parse(rows(updated));
            table.refresh();
        }

        const comment = $$(COMMENT_ID);
        if (comment) {
            comment.setValue(updated.generalComment || "");
            comment.define("readonly", !updated.canDecide);
            comment.refresh();
        }

        if (!updated.canDecide) {
            $$(ACCEPT_ID)?.disable();
            $$(REJECT_ID)?.disable();
        }

        setError(updated.lastError);
    }

    function setError(text) {
        const view = $$(ERROR_ID);
        if (!view)
            return;

        const value = String(text ?? "").trim();
        view.define("template", value
            ? `<div style="color:#9b2335;white-space:normal;line-height:1.4;">${webix.template.escape(value)}</div>`
            : "");
        view.refresh();
    }
}

function rows(document) {
    return (document.lines || []).map((line) => {
        const decided = document.status !== "Waiting";
        return {
            id: line.index,
            index: line.index,
            name: line.name,
            places: line.places ?? "",
            factPlaces: line.factPlaces ?? (decided ? "" : line.places ?? ""),
            grossWeight: line.grossWeight ?? "",
            factWeight: line.factWeight || (decided ? "" : line.grossWeight ?? ""),
            comment: line.comment || "",
            editablePlaces: !!line.editablePlaces,
            editableWeight: !!line.editableWeight,
        };
    });
}

function infoHtml(document) {
    return infoLines(document)
        .filter(([, value]) => value)
        .map(([name, value]) => `<div><b>${webix.template.escape(name)}:</b> ${webix.template.escape(value)}</div>`)
        .join("");
}

function infoLines(document) {
    return [
        ["Номер", document.number],
        ["Дата", formatIsoMoment(document.date)],
        ["Организация", document.organizationName],
        ["Оператор", document.operatorName],
        ["Грузоотправитель", document.consignor],
        ["Адрес выгрузки", document.deliveryAddress],
        ["УИД ГИС ЭПД", document.uid],
        ["Статус", etrnStatusLabel(document)],
        ["Ошибка", document.lastError],
    ];
}

function showStage(dialog, text) {
    const host = dialog.getNode();
    let stage = host.querySelector(".vetis-quench-stage");
    if (!stage) {
        stage = window.document.createElement("div");
        stage.className = "vetis-quench-stage";
        stage.innerHTML = "<div class='vetis-quench-card'><span class='vetis-quench-spin'></span><div class='vetis-quench-text'></div></div>";
        host.appendChild(stage);
    }

    stage.querySelector(".vetis-quench-text").textContent = text;
}

function hideStage(dialog) {
    dialog?.getNode()?.querySelector(".vetis-quench-stage")?.remove();
}

function errorText(raw) {
    const text = String(raw ?? "").trim();
    if (text.startsWith("\"") && text.endsWith("\"")) {
        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    }

    return text;
}
