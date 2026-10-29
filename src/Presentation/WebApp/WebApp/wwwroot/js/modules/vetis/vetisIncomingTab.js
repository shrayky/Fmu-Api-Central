import vetisService from '../../services/vetisService.js';
import { formatIsoMoment, incomingDocumentLabel, incomingReceivedLabel, incomingStatusCss, incomingStatusLabel, vsdCountText } from '../../utils/vetisFormat.js';
import { VetisFilterBar } from './vetisFilterBar.js';
import { VetisPagedTable } from './vetisPagedTable.js';
import { openIncomingWindow } from './vetisIncomingWindow.js';

export class VetisIncomingTab {
    constructor(organizations) {
        this.filterBar = new VetisFilterBar({
            prefix: "vetisIncoming",
            organizations,
            withStatus: true,
            onChange: () => this.load(),
        });
        this.table = new VetisPagedTable({
            id: "vetisIncomingTable",
            fetchPage: (filters, skip, limit) => vetisService.incoming(filters, skip, limit),
            onOpen: (row) => openIncomingWindow(row),
        });
    }

    config() {
        return {
            rows: [
                this.filterBar.config(),
                this.table.config([
                    { id: "receivedAt", header: "Получен", width: 140, template: (row) => incomingReceivedLabel(row) },
                    { id: "date", header: "Дата документа", width: 130, template: (row) => formatIsoMoment(row.date) },
                    { id: "number", header: "Документ", width: 180, template: (row) => incomingDocumentLabel(row) },
                    { id: "consignor", header: "Поставщик", fillspace: true },
                    { id: "organizationName", header: "Организация", width: 240 },
                    { id: "enterpriseName", header: "Предприятие", width: 240 },
                    { id: "vsd", header: "ВСД", width: 90, template: (row) => vsdCountText(row) },
                    { id: "status", header: "Статус", width: 170, template: (row) => this._statusBadge(row.status) },
                ]),
            ],
        };
    }

    load() {
        return this.table.reload(this.filterBar.values());
    }

    _statusBadge(status) {
        const label = webix.template.escape(incomingStatusLabel(status));
        return `<span class="vetis-status ${incomingStatusCss(status)}">${label}</span>`;
    }
}
