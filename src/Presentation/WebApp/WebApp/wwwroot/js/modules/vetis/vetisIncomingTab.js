import vetisService from '../../services/vetisService.js';
import { formatIsoMoment, incomingDocumentLabel, incomingStatusCss, incomingStatusLabel, vsdCountText } from '../../utils/vetisFormat.js';
import { VetisFilterBar } from './vetisFilterBar.js';
import { VetisPagedTable } from './vetisPagedTable.js';
import { openIncomingWindow } from './vetisIncomingWindow.js';

export class VetisIncomingTab {
    constructor(organizations) {
        this.filterBar = new VetisFilterBar({
            prefix: "vetisIncoming",
            organizations,
            withStatus: true,
            withVsd: true,
            onChange: () => this.refresh(),
        });
        this.table = new VetisPagedTable({
            id: "vetisIncomingTable",
            fetchPage: (filters, skip, limit) => vetisService.incoming(filters, skip, limit),
            onOpen: (row) => openIncomingWindow(row, { onQuenched: () => this.refresh() }),
        });
    }

    config() {
        return {
            rows: [
                this.filterBar.config(),
                this.table.config([
                    { id: "date", header: "Дата документа", width: 130, template: (row) => formatIsoMoment(row.date) },
                    { id: "number", header: "Документ", width: 180, template: (row) => incomingDocumentLabel(row) },
                    { id: "consignor", header: "Поставщик", fillspace: true },
                    { id: "organizationName", header: "Организация", width: 240 },
                    { id: "enterpriseName", header: "Предприятие", width: 240 },
                    { id: "vsd", header: "ВСД", width: 90, template: (row) => vsdCountText(row) },
                    { id: "status", header: "Статус", width: 170, template: (row) => this._statusBadge(row.status) },
                    { id: "quenchedBy", header: "Автор", width: 160 },
                    { id: "quenchedAt", header: "Погашен", width: 150, template: (row) => formatIsoMoment(row.quenchedAt) },
                ]),
            ],
        };
    }

    load() {
        return this.table.reload(this.filterBar.values());
    }

    refresh() {
        const table = this.load();
        this._loadCounts();
        return table;
    }

    applyCounts(organizations) {
        if (organizations)
            this.filterBar.apply(organizations);
    }

    _loadCounts() {
        return vetisService.notAcceptedCounts()
            .then((organizations) => this.applyCounts(organizations))
            .catch(() => {});
    }

    _statusBadge(status) {
        const label = webix.template.escape(incomingStatusLabel(status));
        return `<span class="vetis-status ${incomingStatusCss(status)}">${label}</span>`;
    }
}
