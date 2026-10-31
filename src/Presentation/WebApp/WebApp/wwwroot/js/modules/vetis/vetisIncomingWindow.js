import vetisService from '../../services/vetisService.js';
import { copyText } from '../../utils/copyText.js';
import { canQuenchRows, editedActualVolume, formatIsoMoment, incomingStatusLabel, incomingWindowClosable, quenchStageText, quenchSummary, sameIncomingDocument, vsdStatusLabel, vsdXmlFileName } from '../../utils/vetisFormat.js';
import { openVetisWindow } from './vetisWindow.js';

const WINDOW_ID = "vetisIncomingWindow";
const TABLE_ID = "vetisIncomingVsds";
const INFO_ID = "vetisIncomingInfo";

let openedId = null;
let busyId = null;

export async function openIncomingWindow(row, { onQuenched } = {}) {
    let incoming;
    try {
        incoming = await vetisService.incomingDocument(row.id);
    } catch (error) {
        webix.message({ text: error.message, type: "error" });
        return;
    }

    openedId = incoming.id;

    const date = formatIsoMoment(incoming.date);
    const title = incoming.loose
        ? `ВСД без входящего документа от ${date}`
        : `Документ № ${incoming.number} от ${date}`;

    openVetisWindow({
        id: WINDOW_ID,
        title,
        fill: true,
        canClose: () => incomingWindowClosable(openedId, busyId),
        body: {
            rows: [
                { view: "template", id: INFO_ID, borderless: true, height: 110, template: infoHtml(incoming) },
                {
                    view: "toolbar",
                    elements: [
                        {
                            view: "button",
                            value: "Погасить",
                            width: 140,
                            disabled: !canQuenchRows(incoming.vsds),
                            click: () => quench(incoming.id, onQuenched),
                        },
                        {},
                    ],
                },
                {
                    view: "datatable",
                    id: TABLE_ID,
                    editable: true,
                    editaction: "click",
                    columns: [
                        { id: "issueNumber", header: "Номер ВСД", width: 150 },
                        { id: "issueDate", header: "Дата оформления", width: 140, template: (vsd) => formatIsoMoment(vsd.issueDate) },
                        { id: "productName", header: "Продукция", fillspace: true },
                        { id: "volume", header: "Объём", width: 100 },
                        { id: "actualVolume", header: "Факт", width: 110, editor: "text" },
                        { id: "transport", header: "Транспорт", width: 260 },
                        { id: "status", header: "Статус", width: 120, template: (vsd) => vsdStatusLabel(vsd.status) },
                        { id: "receivedAt", header: "Получен", width: 140, template: (vsd) => formatIsoMoment(vsd.receivedAt) },
                        {
                            id: "copy",
                            header: "",
                            width: 40,
                            template: (vsd) => vsd.uuid
                                ? "<span class='webix_icon vetis-copy' title='Скопировать идентификатор'></span>"
                                : "",
                        },
                        {
                            id: "xml",
                            header: "",
                            width: 40,
                            template: (vsd) => vsd.rawXml
                                ? "<span class='webix_icon wxi-download' title='Выгрузить XML'></span>"
                                : "",
                        },
                    ],
                    select: "row",
                    data: incoming.vsds.map(vsdRow),
                    on: {
                        onAfterEditStop(state, editor) {
                            if (editor.column !== "actualVolume")
                                return;

                            const item = this.getItem(editor.row);
                            const edited = editedActualVolume(item.volume, state.value);
                            if (!edited.ok) {
                                item.actualVolume = state.old;
                                this.updateItem(item.id, item);
                                webix.message({ type: "error", text: "Фактическое количество должно быть от 0 до объёма поступления." });
                                return;
                            }

                            item.actualVolume = edited.value;
                            this.updateItem(item.id, item);
                        },
                        onItemClick(cell) {
                            const row = this.getItem(cell?.row);
                            if (!row)
                                return;

                            if (cell.column === "copy")
                                copyUuid(row.uuid);
                            if (cell.column === "xml")
                                downloadVsdXml(row);
                        },
                    },
                },
            ],
        },
    });
}

function infoHtml(incoming) {
    return infoLines(incoming)
        .filter(([, value]) => value)
        .map(([name, value]) => `<div><b>${webix.template.escape(name)}:</b> ${webix.template.escape(value)}</div>`)
        .join("");
}

function infoLines(incoming) {
    return [
        ["Организация", incoming.organizationName],
        ["Предприятие", incoming.enterpriseName],
        ["Поставщик", incoming.consignor],
        ["Статус", incomingStatusLabel(incoming.status)],
    ];
}

function vsdRow(vsd) {
    return {
        ...vsd,
        id: vsd.uuid,
        actualVolume: vsd.actualVolume != null && String(vsd.actualVolume) !== "" ? vsd.actualVolume : vsd.volume,
    };
}

async function quench(documentId, onQuenched) {
    const dialog = $$(WINDOW_ID);
    const table = $$(TABLE_ID);
    if (!dialog || !table)
        return;

    const requestId = documentId;
    const facts = table.serialize().map((row) => ({ uuid: row.uuid, actualVolume: row.actualVolume }));
    busyId = requestId;
    dialog.disable();
    showQuenchStage(dialog, quenchStageText({}));
    const watch = watchQuench(requestId);
    let report = null;
    try {
        report = await vetisService.quench(requestId, facts);
        const text = quenchSummary(report);
        webix.message(report.errors?.length ? { type: "error", text, expire: 8000 } : text);
        applyQuenchReport(report, requestId);
    } catch (error) {
        webix.message({ type: "error", text: error.message });
        if (onQuenched)
            await onQuenched();
    } finally {
        watch.stop();
        if (busyId === requestId)
            busyId = null;

        if (sameIncomingDocument(openedId, requestId)) {
            const opened = $$(WINDOW_ID);
            hideQuenchStage(opened);
            opened?.enable();
        }
    }

    if (report && onQuenched)
        await onQuenched();
}

function applyQuenchReport(report, requestId) {
    if (!report?.document || !sameIncomingDocument(openedId, requestId))
        return;

    const table = $$(TABLE_ID);
    if (table) {
        table.clearAll();
        table.parse((report.document.vsds || []).map(vsdRow));
    }

    const info = $$(INFO_ID);
    if (info) {
        info.define("template", infoHtml(report.document));
        info.refresh();
    }
}

const QUENCH_POLL_MS = 400;

function watchQuench(documentId) {
    let stopped = false;
    const pull = () => {
        if (stopped)
            return;

        vetisService.quenchProgress(documentId)
            .then((progress) => {
                // Ответ «не идёт» приходит после очистки: поздний опрос не затирает последнюю стадию.
                if (stopped || !progress?.running || !sameIncomingDocument(openedId, documentId))
                    return;

                const opened = $$(WINDOW_ID);
                if (opened)
                    showQuenchStage(opened, quenchStageText(progress));
            })
            .catch(() => {});
    };

    pull();
    const timer = window.setInterval(pull, QUENCH_POLL_MS);
    return {
        stop() {
            stopped = true;
            window.clearInterval(timer);
        }
    };
}

function showQuenchStage(dialog, text) {
    const host = dialog.getNode();
    let stage = host.querySelector(".vetis-quench-stage");
    if (!stage) {
        stage = document.createElement("div");
        stage.className = "vetis-quench-stage";
        stage.innerHTML = "<div class='vetis-quench-card'><span class='vetis-quench-spin'></span><div class='vetis-quench-text'></div></div>";
        host.appendChild(stage);
    }

    stage.querySelector(".vetis-quench-text").textContent = text;
}

function hideQuenchStage(dialog) {
    dialog?.getNode()?.querySelector(".vetis-quench-stage")?.remove();
}

function downloadVsdXml(vsd) {
    if (!vsd?.rawXml)
        return;

    const blob = new Blob([vsd.rawXml], { type: "application/xml" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = vsdXmlFileName(vsd);
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(link.href);
}

async function copyUuid(uuid) {
    if (!uuid)
        return;

    try {
        await copyText(uuid);
        webix.message("Идентификатор скопирован");
    } catch (error) {
        webix.message({ text: error.message || "Не удалось скопировать идентификатор", type: "error" });
    }
}
