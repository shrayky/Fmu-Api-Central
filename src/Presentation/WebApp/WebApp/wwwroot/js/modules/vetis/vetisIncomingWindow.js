import vetisService from '../../services/vetisService.js';
import { copyText } from '../../utils/copyText.js';
import { formatIsoMoment, incomingStatusLabel, vsdStatusLabel, vsdXmlFileName } from '../../utils/vetisFormat.js';
import { infoTemplate, openVetisWindow } from './vetisWindow.js';

export async function openIncomingWindow(row) {
    let incoming;
    try {
        incoming = await vetisService.incomingDocument(row.id);
    } catch (error) {
        webix.message({ text: error.message, type: "error" });
        return;
    }

    const date = formatIsoMoment(incoming.date);
    const title = incoming.loose
        ? `ВСД без входящего документа от ${date}`
        : `Документ № ${incoming.number} от ${date}`;

    openVetisWindow({
        id: "vetisIncomingWindow",
        title,
        fill: true,
        body: {
            rows: [
                infoTemplate([
                    ["Организация", incoming.organizationName],
                    ["Предприятие", incoming.enterpriseName],
                    ["Поставщик", incoming.consignor],
                    ["Статус", incomingStatusLabel(incoming.status)],
                ], 110),
                {
                    view: "datatable",
                    columns: [
                        { id: "issueNumber", header: "Номер ВСД", width: 150 },
                        { id: "issueDate", header: "Дата оформления", width: 140, template: (vsd) => formatIsoMoment(vsd.issueDate) },
                        { id: "productName", header: "Продукция", fillspace: true },
                        { id: "volume", header: "Объём", width: 100 },
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
                    data: incoming.vsds.map((vsd) => ({ ...vsd, id: vsd.uuid })),
                    on: {
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
