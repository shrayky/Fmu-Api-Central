import { incomingStatusLabel } from '../../utils/vetisFormat.js';

const ALL = "__all__";
const STATUSES = ["accepted", "partial", "notAccepted", "rejected"];

export class VetisFilterBar {
    constructor({ prefix, organizations, withStatus, onChange }) {
        this.organizations = organizations;
        this.withStatus = withStatus;
        this.onChange = onChange;
        this.ids = {
            organization: `${prefix}OrganizationFilter`,
            enterprise: `${prefix}EnterpriseFilter`,
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
                options: [{ id: ALL, value: "Все организации" }, ...this.organizations.map((item) => ({ id: item.id, value: item.name }))],
                on: { onChange: (organizationId) => this._organizationChanged(organizationId) },
            },
            {
                view: "richselect",
                id: this.ids.enterprise,
                width: 320,
                value: ALL,
                disabled: true,
                options: [{ id: ALL, value: "Все предприятия" }],
                on: { onChange: () => this.onChange() },
            },
        ];

        if (this.withStatus) {
            elements.push({
                view: "richselect",
                id: this.ids.status,
                width: 200,
                value: ALL,
                options: [{ id: ALL, value: "Все статусы" }, ...STATUSES.map((status) => ({ id: status, value: incomingStatusLabel(status) }))],
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
            enterpriseGuid: pick(this.ids.enterprise),
        };

        if (this.withStatus) {
            values.status = pick(this.ids.status);
        }

        return values;
    }

    _organizationChanged(organizationId) {
        const enterprise = $$(this.ids.enterprise);
        const organization = this.organizations.find((item) => item.id === organizationId);
        const enterprises = organization?.enterprises ?? [];

        enterprise.blockEvent();
        enterprise.define("options", [{ id: ALL, value: "Все предприятия" }, ...enterprises.map((item) => ({ id: item.guid, value: item.name }))]);
        enterprise.refresh();
        enterprise.setValue(ALL);
        enterprise.unblockEvent();
        if (organization) {
            enterprise.enable();
        } else {
            enterprise.disable();
        }

        this.onChange();
    }
}
