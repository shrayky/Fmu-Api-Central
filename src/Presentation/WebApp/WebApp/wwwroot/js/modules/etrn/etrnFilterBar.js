import { etrnStatusLabel } from '../../utils/etrnFormat.js';
import { salePointFilterOptions } from '../../utils/salePointFormat.js';

const ALL = "__all__";
const STATUSES = ["Waiting", "Sending", "Accepted", "Rejected", "Closed", "Foreign"];

export class EtrnFilterBar {
    constructor({ prefix, organizations, withStatus, withJournalSearch, withSalePoint, onChange }) {
        this.organizations = organizations || [];
        this.withStatus = withStatus;
        this.withJournalSearch = withJournalSearch;
        this.withSalePoint = !!withSalePoint;
        this.onChange = onChange;
        this.ids = {
            organization: `${prefix}OrganizationFilter`,
            connection: `${prefix}ConnectionFilter`,
            salePoint: `${prefix}SalePointFilter`,
            status: `${prefix}StatusFilter`,
            number: `${prefix}NumberFilter`,
            consignor: `${prefix}ConsignorFilter`,
        };
    }

    config() {
        const elements = [
            { view: "button", value: "Обновить", width: 110, click: () => this.onChange() },
            {
                view: "richselect",
                id: this.ids.organization,
                width: 320,
                value: ALL,
                options: this._organizationOptions(),
                on: { onChange: (organizationId) => this._organizationChanged(organizationId) },
            },
        ];

        if (this.withSalePoint) {
            elements.push({
                view: "richselect",
                id: this.ids.salePoint,
                width: 240,
                value: ALL,
                disabled: true,
                options: [{ id: ALL, value: "Все точки" }],
                on: { onChange: () => this.onChange() },
            });
        }

        elements.push({
            view: "richselect",
            id: this.ids.connection,
            width: 320,
            value: ALL,
            disabled: true,
            options: [{ id: ALL, value: "Все подключения" }],
            on: { onChange: () => this.onChange() },
        });

        if (this.withStatus) {
            elements.push({
                view: "richselect",
                id: this.ids.status,
                width: 200,
                value: "Waiting",
                options: [{ id: ALL, value: "Все статусы" }, ...STATUSES.map((status) => ({ id: status, value: etrnStatusLabel({ status }) }))],
                on: { onChange: () => this.onChange() },
            });
        }

        if (this.withJournalSearch) {
            elements.push(
                {
                    view: "text",
                    id: this.ids.number,
                    width: 160,
                    placeholder: "Номер",
                    clear: true,
                    on: {
                        onTimedKeyPress: () => this.onChange(),
                        onChange: () => this.onChange(),
                    },
                },
                {
                    view: "text",
                    id: this.ids.consignor,
                    width: 220,
                    placeholder: "Поставщик",
                    clear: true,
                    on: {
                        onTimedKeyPress: () => this.onChange(),
                        onChange: () => this.onChange(),
                    },
                },
            );
        }

        elements.push({});
        return { view: "toolbar", elements };
    }

    values() {
        const pick = (id) => {
            const value = $$(id)?.getValue();
            return !value || value === ALL ? "" : value;
        };

        const values = {
            organizationId: pick(this.ids.organization),
            connectionId: pick(this.ids.connection),
        };

        if (this.withSalePoint) {
            const salePoint = pick(this.ids.salePoint);
            if (salePoint)
                values.salePoint = salePoint;
        }

        if (this.withStatus) {
            values.status = pick(this.ids.status);
        }

        if (this.withJournalSearch) {
            const number = ($$(this.ids.number)?.getValue() || "").trim();
            const consignor = ($$(this.ids.consignor)?.getValue() || "").trim();
            if (number)
                values.number = number;
            if (consignor)
                values.consignor = consignor;
        }

        return values;
    }

    apply(organizations) {
        this.organizations = organizations || [];
        const organization = $$(this.ids.organization);
        if (!organization) {
            return;
        }

        const selected = organization.getValue();
        const connection = $$(this.ids.connection)?.getValue();
        const salePoint = $$(this.ids.salePoint)?.getValue();
        organization.blockEvent();
        organization.define("options", this._organizationOptions());
        organization.refresh();
        const next = this.organizations.some((item) => item.id === selected) ? selected : ALL;
        organization.setValue(next);
        organization.unblockEvent();
        this._showConnections(next, connection);
        this._showSalePoints(next, salePoint);
    }

    _organizationOptions() {
        return [
            { id: ALL, value: "Все организации" },
            ...this.organizations.map((item) => ({ id: item.id, value: item.name })),
        ];
    }

    _organizationChanged(organizationId) {
        this._showConnections(organizationId, ALL);
        this._showSalePoints(organizationId, ALL);
        this.onChange();
    }

    _showSalePoints(organizationId, keepSalePointId) {
        const control = $$(this.ids.salePoint);
        if (!control) {
            return;
        }

        const organization = this.organizations.find((item) => item.id === organizationId);
        const options = salePointFilterOptions(organization?.salePoints)
            .map((item) => ({ id: item.id || ALL, value: item.value }));

        control.blockEvent();
        control.define("options", options);
        control.refresh();
        const keep = options.some((item) => item.id === keepSalePointId) ? keepSalePointId : ALL;
        control.setValue(keep);
        control.unblockEvent();
        if (organization)
            control.enable();
        else
            control.disable();
    }

    _showConnections(organizationId, keepConnectionId) {
        const control = $$(this.ids.connection);
        if (!control) {
            return;
        }

        const organization = this.organizations.find((item) => item.id === organizationId);
        const connections = organization?.connections ?? [];
        const options = [
            { id: ALL, value: "Все подключения" },
            ...connections.map((item) => ({ id: item.id, value: item.name })),
        ];

        control.blockEvent();
        control.define("options", options);
        control.refresh();
        const keep = options.some((item) => item.id === keepConnectionId) ? keepConnectionId : ALL;
        control.setValue(keep);
        control.unblockEvent();
        if (organization)
            control.enable();
        else
            control.disable();
    }
}
