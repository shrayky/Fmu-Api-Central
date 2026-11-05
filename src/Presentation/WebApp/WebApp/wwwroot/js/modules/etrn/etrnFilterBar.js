import { etrnStatusLabel } from '../../utils/etrnFormat.js';

const ALL = "__all__";
const STATUSES = ["Waiting", "Sending", "Accepted", "Rejected", "Closed"];

export class EtrnFilterBar {
    constructor({ prefix, organizations, withStatus, onChange }) {
        this.organizations = organizations || [];
        this.withStatus = withStatus;
        this.onChange = onChange;
        this.ids = {
            organization: `${prefix}OrganizationFilter`,
            connection: `${prefix}ConnectionFilter`,
            status: `${prefix}StatusFilter`,
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
            {
                view: "richselect",
                id: this.ids.connection,
                width: 320,
                value: ALL,
                disabled: true,
                options: [{ id: ALL, value: "Все подключения" }],
                on: { onChange: () => this.onChange() },
            },
        ];

        if (this.withStatus) {
            elements.push({
                view: "richselect",
                id: this.ids.status,
                width: 200,
                value: ALL,
                options: [{ id: ALL, value: "Все статусы" }, ...STATUSES.map((status) => ({ id: status, value: etrnStatusLabel({ status }) }))],
                on: { onChange: () => this.onChange() },
            });
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

        if (this.withStatus) {
            values.status = pick(this.ids.status);
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
        organization.blockEvent();
        organization.define("options", this._organizationOptions());
        organization.refresh();
        const next = this.organizations.some((item) => item.id === selected) ? selected : ALL;
        organization.setValue(next);
        organization.unblockEvent();
        this._showConnections(next, connection);
    }

    _organizationOptions() {
        return [
            { id: ALL, value: "Все организации" },
            ...this.organizations.map((item) => ({ id: item.id, value: item.name })),
        ];
    }

    _organizationChanged(organizationId) {
        this._showConnections(organizationId, ALL);
        this.onChange();
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
