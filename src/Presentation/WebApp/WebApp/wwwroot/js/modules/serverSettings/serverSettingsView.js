import { loadConfiguration, saveConfigurationSections } from "../../services/ConfigurationService.js";
import { setVetisMenuVisible } from "../../services/VetisMenuService.js";
import { createServerTab, serverSettingsFormId } from "./serverTab.js";
import { createVetisTab, vetisSettingsFormId } from "./vetisTab.js";
import { mapServerSettings } from "./loadedSettings.js";
import { buildConfigurationUpdaters, vetisEndpoint } from "./saveSettings.js";
import { readFieldValues } from "./tabFields.js";

const fieldNames = [
    "apiIpPort",
    "publicAddress",
    "corsOrigins",
    "trustedProxies",
    "allowLegacyAgentApi",
    "isEnabled",
    "logDepth",
    "logLevel",
    "vetisEnable",
    "vetisEndpoint",
    "vetisIncomingInterval",
    "vetisIncomingPageSize",
    "vetisAutoQuenchInterval",
    "vetisAutoQuenchBatch",
    "vetisRequireVehicle",
    "vetisApplicationRetention",
    "vetisFailedApplicationRetention",
];

const formIds = [serverSettingsFormId, vetisSettingsFormId];

export function createServerSettingsRows(serverTab, vetisTab, onSave) {
    return [
        {
            cols: [
                {
                    view: "button",
                    value: "Сохранить",
                    css: "webix_primary",
                    width: 120,
                    click: () => onSave()
                },
                {}
            ]
        },
        {
            view: "tabview",
            id: "serverSettingsTabView",
            cells: [serverTab, vetisTab]
        }
    ];
}

export async function persistServerSettings({ endpointField, formsValid, readValues, saveSections, showMenu }) {
    if (endpointField) {
        const endpoint = vetisEndpoint(endpointField.getValue());
        if (endpointField.getValue() !== endpoint)
            endpointField.setValue(endpoint);
    }

    if (!formsValid())
        return { saved: false };

    const values = readValues();
    const saveResult = await saveSections(buildConfigurationUpdaters(values));
    if (!saveResult.result)
        return { saved: false, error: saveResult.error };

    showMenu(!!values.vetisEnable);
    return { saved: true };
}

function formsAreValid() {
    for (const formId of formIds) {
        const form = $$(formId);
        if (form && !form.validate())
            return false;
    }

    return true;
}

class SettingsView {
    constructor(id) {
        this.id = id;
    }

    async loadData() {
        const requestResult = await loadConfiguration();

        if (!requestResult.result) {
            console.error(requestResult);
            webix.message({ type: "error", text: requestResult.error });
            return this;
        }

        const mapped = mapServerSettings(requestResult.value.Content);
        this.server = mapped.server;
        this.vetis = mapped.vetis;
        return this;
    }

    renderView() {
        $$("toolbarLabel").setValue("Fmu-Api-Central: Настройки сервера");

        return {
            id: this.id,
            rows: createServerSettingsRows(
                createServerTab(this.server),
                createVetisTab(this.vetis),
                () => this.save()
            )
        };
    }

    async save() {
        const result = await persistServerSettings({
            endpointField: $$("vetisEndpoint"),
            formsValid: formsAreValid,
            readValues: () => readFieldValues(fieldNames),
            saveSections: saveConfigurationSections,
            showMenu: setVetisMenuVisible,
        });

        if (!result.saved && result.error) {
            webix.message({ type: "error", text: result.error });
            return;
        }

        if (!result.saved)
            return;

        webix.message({
            type: "success",
            text: "Настройки сохранены. Необходимо перезапустить службу для применения изменений."
        });
    }
}

export default async function createServerSettingsView(id) {
    const view = new SettingsView(id);
    await view.loadData();
    return view.renderView();
}
