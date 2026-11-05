import etrnService from '../../services/etrnService.js';
import { etrnStatusCss, etrnStatusLabel, formatIsoMoment } from '../../utils/etrnFormat.js';
import { VetisPagedTable } from '../vetis/vetisPagedTable.js';
import { EtrnFilterBar } from './etrnFilterBar.js';
import { openEtrnIncomingWindow } from './etrnIncomingWindow.js';

export class EtrnIncomingTab {
    constructor(organizations) {
        this.filterBar = new EtrnFilterBar({
            prefix: "etrnIncoming",
            organizations,
            withStatus: true,
            onChange: () => this.refresh(),
        });
        this.table = new VetisPagedTable({
            id: "etrnIncomingTable",
            fetchPage: (filters, skip, limit) => etrnService.incoming(filters, skip, limit),
            onOpen: (row) => openEtrnIncomingWindow(row, { onAccepted: () => this.refresh() }),
        });
    }

    config() {
        return {
            rows: [
                this.filterBar.config(),
                this.table.config([
                    { id: "date", header: "Дата", width: 120, template: (row) => formatIsoMoment(row.date) },
                    { id: "number", header: "Номер", width: 160, template: (row) => this._escape(row.number) },
                    { id: "consignor", header: "Грузоотправитель", fillspace: 1, template: (row) => this._escape(row.consignor) },
                    { id: "deliveryAddress", header: "Адрес выгрузки", fillspace: 1.4, template: (row) => this._escape(row.deliveryAddress) },
                    { id: "operatorName", header: "Оператор", width: 180, template: (row) => this._escape(row.operatorName) },
                    { id: "organizationName", header: "Организация", width: 220, template: (row) => this._escape(row.organizationName) },
                    { id: "status", header: "Статус", width: 160, template: (row) => this._statusBadge(row) },
                ]),
            ],
        };
    }

    load() {
        return this.table.reload(this.filterBar.values());
    }

    refresh() {
        return this.load();
    }

    _statusBadge(row) {
        const label = webix.template.escape(etrnStatusLabel(row));
        return `<span class="vetis-status ${etrnStatusCss(row.status)}">${label}</span>`;
    }

    _escape(value) {
        return webix.template.escape(String(value ?? ""));
    }
}
