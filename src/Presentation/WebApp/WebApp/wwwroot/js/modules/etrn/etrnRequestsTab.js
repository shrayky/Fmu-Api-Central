import etrnService from '../../services/etrnService.js';
import { etrnStatusCss, etrnStatusLabel, etrnResponseText, formatUnixMoment } from '../../utils/etrnFormat.js';
import { VetisPagedTable } from '../vetis/vetisPagedTable.js';
import { EtrnFilterBar } from './etrnFilterBar.js';
import { openEtrnRequestWindow } from './etrnRequestWindow.js';

export class EtrnRequestsTab {
    constructor(organizations) {
        this.filterBar = new EtrnFilterBar({
            prefix: "etrnRequests",
            organizations,
            withStatus: false,
            onChange: () => this.load(),
        });
        this.table = new VetisPagedTable({
            id: "etrnRequestsTable",
            fetchPage: (filters, skip, limit) => etrnService.requests(filters, skip, limit),
            onOpen: (row) => openEtrnRequestWindow(row),
        });
    }

    config() {
        return {
            rows: [
                this.filterBar.config(),
                this.table.config([
                    { id: "createdAt", header: "Время", width: 140, template: (row) => formatUnixMoment(row.createdAt) },
                    { id: "method", header: "Запрос", width: 210 },
                    { id: "state", header: "Ответ", width: 110, template: (row) => this._state(row) },
                    { id: "parameters", header: "Параметры", fillspace: true, template: (row) => webix.template.escape(row.parameters) },
                    { id: "organizationName", header: "Организация", width: 240, template: (row) => webix.template.escape(row.organizationName) },
                    { id: "operatorName", header: "Оператор", width: 180, template: (row) => webix.template.escape(row.operatorName) },
                ]),
            ],
        };
    }

    load() {
        return this.table.reload(this.filterBar.values());
    }

    _state(row) {
        const text = etrnResponseText(row);
        if (!row.error) {
            return text;
        }

        return `<span class="vetis-request-error" title="${webix.template.escape(row.error)}">${text}</span>`;
    }
}
