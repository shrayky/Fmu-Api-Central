import vetisService from '../../services/vetisService.js';
import { formatUnixMoment, operationLabel, requestStateText } from '../../utils/vetisFormat.js';
import { VetisFilterBar } from './vetisFilterBar.js';
import { VetisPagedTable } from './vetisPagedTable.js';
import { openApplicationWindow } from './vetisApplicationWindow.js';

export class VetisApplicationsTab {
    constructor(organizations) {
        this.filterBar = new VetisFilterBar({
            prefix: "vetisApplications",
            organizations,
            withStatus: false,
            onChange: () => this.load(),
        });
        this.table = new VetisPagedTable({
            id: "vetisApplicationsTable",
            fetchPage: (filters, skip, limit) => vetisService.applications(filters, skip, limit),
            onOpen: (row) => openApplicationWindow(row),
        });
    }

    config() {
        return {
            rows: [
                this.filterBar.config(),
                this.table.config([
                    { id: "createdAt", header: "Время", width: 140, template: (row) => formatUnixMoment(row.createdAt) },
                    { id: "operation", header: "Запрос", width: 190, template: (row) => operationLabel(row.operation) },
                    { id: "state", header: "Ответ", width: 120, template: (row) => this._state(row) },
                    { id: "parameters", header: "Параметры", fillspace: true },
                    { id: "organizationName", header: "Организация", width: 240 },
                    { id: "enterpriseName", header: "Предприятие", width: 240 },
                ]),
            ],
        };
    }

    load() {
        return this.table.reload(this.filterBar.values());
    }

    _state(row) {
        const text = requestStateText(row);
        if (!row.error) {
            return text;
        }

        return `<span class="vetis-request-error" title="${webix.template.escape(row.error)}">${text}</span>`;
    }
}
