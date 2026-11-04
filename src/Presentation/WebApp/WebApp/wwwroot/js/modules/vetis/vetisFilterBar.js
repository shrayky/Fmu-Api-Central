import { incomingStatusLabel, vetisCountLabel } from '../../utils/vetisFormat.js';

const ALL = "__all__";
const STATUSES = ["accepted", "partial", "notAccepted", "rejected"];

export class VetisFilterBar {
    constructor({ prefix, organizations, withStatus, withVsd, onChange }) {
        this.organizations = organizations;
        this.withStatus = withStatus;
        this.withVsd = withVsd;
        this.onChange = onChange;
        this.ids = {
            organization: `${prefix}OrganizationFilter`,
            enterprise: `${prefix}EnterpriseFilter`,
            status: `${prefix}StatusFilter`,
            vsd: `${prefix}VsdFilter`,
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
                value: "notAccepted",
                options: [{ id: ALL, value: "Все статусы" }, ...STATUSES.map((status) => ({ id: status, value: incomingStatusLabel(status) }))],
                on: { onChange: () => this.onChange() },
            });
        }

        if (this.withVsd) {
            elements.push({
                view: "text",
                id: this.ids.vsd,
                width: 320,
                placeholder: "GUID ВСД",
                clear: true,
                on: {
                    onTimedKeyPress: () => this.onChange(),
                    onChange: () => this.onChange(),
                },
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

        const vsdUuid = this._vsdUuid();
        if (vsdUuid)
            values.vsdUuid = vsdUuid;

        return values;
    }

    _vsdUuid() {
        if (!this.withVsd)
            return "";

        const value = ($$(this.ids.vsd)?.getValue() || "").trim();
        if (/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(value))
            return value;
        if (/^[0-9a-fA-F]{32}$/.test(value))
            return value;

        return "";
    }

    _organizationChanged(organizationId) {
        this._showEnterprises(organizationId, ALL);
        this.onChange();
    }

    apply(organizations) {
        this.organizations = organizations || [];
        const organization = $$(this.ids.organization);
        if (!organization)
            return;

        const selected = organization.getValue();
        const enterpriseId = $$(this.ids.enterprise)?.getValue();
        organization.blockEvent();
        organization.define("options", this._organizationOptions());
        organization.refresh();
        const next = this.organizations.some((item) => item.id === selected) ? selected : ALL;
        organization.setValue(next);
        organization.unblockEvent();
        this._showEnterprises(next, enterpriseId);
    }

    _organizationOptions() {
        return [
            { id: ALL, value: "Все организации" },
            ...this.organizations.map((item) => ({ id: item.id, value: vetisCountLabel(item.name, item.notAcceptedCount) })),
        ];
    }

    _showEnterprises(organizationId, keepEnterpriseId) {
        const enterprise = $$(this.ids.enterprise);
        const organization = this.organizations.find((item) => item.id === organizationId);
        const enterprises = organization?.enterprises ?? [];
        const options = [{ id: ALL, value: "Все предприятия" }, ...enterprises.map((item) => ({
            id: item.guid,
            value: vetisCountLabel(item.name, item.notAcceptedCount),
        }))];

        enterprise.blockEvent();
        enterprise.define("options", options);
        enterprise.refresh();
        const keep = options.some((item) => item.id === keepEnterpriseId) ? keepEnterpriseId : ALL;
        enterprise.setValue(keep);
        enterprise.unblockEvent();
        if (organization)
            enterprise.enable();
        else
            enterprise.disable();
    }
}
