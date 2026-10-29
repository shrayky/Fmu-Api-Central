// js/modules/serverSettingsView.js

import { loadConfiguration, saveConfigurationSections } from '../services/ConfigurationService.js';
import { Label, Number, CheckBox, Text } from '../utils/ui.js';
import { httpAddressValidation } from '../utils/validators.js';
import { setVetisMenuVisible } from '../services/VetisMenuService.js';

const VETIS_FIELDS = [
    "vetisEndpoint",
    "vetisIncomingInterval",
    "vetisIncomingPageSize",
    "vetisApplicationRetention",
    "vetisFailedApplicationRetention",
];

const VETIS_DEFAULT_ENDPOINT = "https://api.vetrf.ru/platform/services/3.0/ApplicationManagementService";

class SettingsView {
    constructor(id) {
        this.id = id;
        this.serverSettingsElementsId = "serverSettingsView";
        this.loggerSettingsElementsId = "loggerSettingsView";
        this.labels = {
            title: "Fmu-Api-Central: Настройки сервера",
            serverSettings: "Настройки сервера",
            loggerSettings: "Настройки логирования",
            apiIpPort: "IP-порт API",
            publicAddress: "Публичный адрес сервера (для checker)",
            corsOrigins: "Дополнительные origin WebApp (по одному в строке). localhost:2580 уже разрешён",
            trustedProxies: "Доверенные прокси (IP Caddy, по одному в строке). 127.0.0.1 уже доверен",
            isEnabled: "Включено",
            logDepth: "Глубина логирования (дней)",
            logLevel: "Уровень логирования",
            allowLegacyAgentApi: "Разрешить старый API узлов (без handshake)",
            vetisEnable: "Использовать Ветис API",
            vetisEndpoint: "Адрес сервиса",
            vetisHint: "Пока флажок выключен, адрес недоступен, вкладка «Ветис» в организации и пункт меню «Ветис» скрыты.",
            vetisIncomingInterval: "Как часто загружать ВСД (минут)",
            vetisIncomingPageSize: "Сколько ВСД за запрос (не больше 100)",
            vetisApplicationRetention: "Хранить выполненные заявки (дней)",
            vetisFailedApplicationRetention: "Хранить заявки с ошибкой или без ответа (дней)",
            vetisPositiveNumber: "Нужно целое число больше нуля",
        }
    }

    LOG_LEVELS = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    async loadData() {
        const requestResult = await loadConfiguration();

        if (!requestResult.result) {
            console.error(requestResult);
            webix.message({ type: "error", text: requestResult.error });
            return this;
        }

        let configuration = requestResult.value.Content;

        this.serverSettings = {
            apiIpPort: configuration.serverSettings.apiIpPort,
            publicAddress: configuration.serverSettings.publicAddress || "",
            corsOrigins: (configuration.serverSettings.corsOrigins || []).join("\n"),
            trustedProxies: (configuration.serverSettings.trustedProxies || []).join("\n"),
        };
        this.logger = {
            isEnabled: configuration.loggerSettings.isEnabled,
            logLevel: configuration.loggerSettings.logLevel,
            logDepth: configuration.loggerSettings.logDepth,
        };
        this.security = {
            allowLegacyAgentApi: configuration.security?.allowLegacyAgentApi !== false,
        };
        const vetis = configuration.vetisSettings ?? {};
        this.vetis = {
            enable: !!vetis.enable,
            endpoint: String(vetis.endpoint || "").trim() || VETIS_DEFAULT_ENDPOINT,
            incomingFetchIntervalMinutes: vetis.incomingFetchIntervalMinutes ?? 5,
            incomingFetchPageSize: vetis.incomingFetchPageSize ?? 100,
            applicationRetentionDays: vetis.applicationRetentionDays ?? 1,
            failedApplicationRetentionDays: vetis.failedApplicationRetentionDays ?? 7,
        };

        return this;
    }

    renderView() {
        $$("toolbarLabel").setValue(this.labels.title);

        const serverSettings = {
            id: this.serverSettingsElementsId,
            rows:[],
        }

        serverSettings.rows.push(
            Label("serverSettingsTitle", this.labels.serverSettings),
            Number(this.labels.apiIpPort, "apiIpPort", this.serverSettings.apiIpPort),
            Text(this.labels.publicAddress, "publicAddress", this.serverSettings.publicAddress, {
                placeholder: "https://central.example:2579"
            }),
            {
                view: "textarea",
                label: this.labels.corsOrigins,
                labelPosition: "top",
                name: "corsOrigins",
                height: 90,
                value: this.serverSettings.corsOrigins
            },
            {
                view: "textarea",
                label: this.labels.trustedProxies,
                labelPosition: "top",
                name: "trustedProxies",
                height: 70,
                value: this.serverSettings.trustedProxies
            },
            CheckBox(this.labels.allowLegacyAgentApi, "allowLegacyAgentApi", {
                value: this.security.allowLegacyAgentApi
            }),
            CheckBox(this.labels.vetisEnable, "vetisEnable", {
                value: this.vetis.enable,
                on: {
                    onChange: (enabled) => this._setVetisEndpointEnabled(!!enabled)
                }
            }),
            Text(this.labels.vetisEndpoint, "vetisEndpoint", this.vetis.endpoint, {
                ...httpAddressValidation,
                disabled: !this.vetis.enable
            }),
            this._vetisNumber(this.labels.vetisIncomingInterval, "vetisIncomingInterval", this.vetis.incomingFetchIntervalMinutes),
            this._vetisNumber(this.labels.vetisIncomingPageSize, "vetisIncomingPageSize", this.vetis.incomingFetchPageSize),
            this._vetisNumber(this.labels.vetisApplicationRetention, "vetisApplicationRetention", this.vetis.applicationRetentionDays),
            this._vetisNumber(this.labels.vetisFailedApplicationRetention, "vetisFailedApplicationRetention", this.vetis.failedApplicationRetentionDays),
            {
                view: "template",
                autoheight: true,
                borderless: true,
                template: this.labels.vetisHint
            },
        );

        const loggerSettings = {
            rows: [
                Label("loggerSettingsTitle", this.labels.loggerSettings),

                CheckBox(this.labels.isEnabled, "isEnabled", {
                    value: this.logger.isEnabled,
                    on: {
                        onChange: (enabled) => {
                            if (enabled) {
                                $$(this.loggerSettingsElementsId).enable();
                            }
                            else {
                                $$(this.loggerSettingsElementsId).disable();
                            }
                        }
                    }
                }),
            ],
        }

        loggerSettings.rows.push(
            {
                id: this.loggerSettingsElementsId,
                rows: [
                    Number(this.labels.logDepth, "logDepth", this.logger.logDepth),

                    {
                        view: "select",
                        label: this.labels.logLevel,
                        labelPosition: "top",
                        id: "LogLevel",
                        name: "logLevel",
                        options: this.LOG_LEVELS,
                        value: this.logger.logLevel
                    },
                ]
            }
        );


        let elements = [
            serverSettings,
            loggerSettings,
            {
                cols: [
                    this._saveButton,
                    {}
                ]
            },
            {}
        ];

        const couchDbSettingsForm = {
            view: "form",
            elements: elements
        };

        return {
            id: this.id,
            rows: [
                couchDbSettingsForm,
            ],
        };
    }

    _saveButton = {
        view: "button",
        value: "Сохранить",
        css: "webix_primary",
        width: 120,
        click: async function () {
            const form = this.getFormView();
            SettingsView._vetisEndpoint($$("vetisEndpoint")?.getValue());

            if (!form.validate()) return;
    
            const values = form.getValues();
    
            const saveResult = await saveConfigurationSections({
                serverSettings: _ => ({
                  apiIpPort: parseInt(values.apiIpPort),
                  publicAddress: String(values.publicAddress || "").trim(),
                  corsOrigins: String(values.corsOrigins || "")
                    .split(/\r?\n/)
                    .map(item => item.trim())
                    .filter(item => item.length > 0),
                  trustedProxies: String(values.trustedProxies || "")
                    .split(/\r?\n/)
                    .map(item => item.trim())
                    .filter(item => item.length > 0)
                }),
                loggerSettings: _ => ({
                  isEnabled: !!values.isEnabled,
                  logDepth: parseInt(values.logDepth),
                  logLevel: values.logLevel
                }),
                security: prev => ({
                  ...prev,
                  allowLegacyAgentApi: !!values.allowLegacyAgentApi
                }),
                vetisSettings: prev => ({
                  ...prev,
                  enable: !!values.vetisEnable,
                  endpoint: SettingsView._vetisEndpoint(values.vetisEndpoint),
                  incomingFetchIntervalMinutes: parseInt(values.vetisIncomingInterval),
                  incomingFetchPageSize: parseInt(values.vetisIncomingPageSize),
                  applicationRetentionDays: parseInt(values.vetisApplicationRetention),
                  failedApplicationRetentionDays: parseInt(values.vetisFailedApplicationRetention)
                })
              });
    
            if (!saveResult.result) {
                webix.message({ type: "error", text: saveResult.error });
                return;
            }

            setVetisMenuVisible(!!values.vetisEnable);
    
            webix.message({
                type: "success",
                text: "Настройки сохранены. Необходимо перезапустить службу для применения изменений."
            });
        }
    };

    static _vetisEndpoint(value) {
        const endpoint = String(value || "").trim() || VETIS_DEFAULT_ENDPOINT;
        const field = $$("vetisEndpoint");
        if (field && field.getValue() !== endpoint)
            field.setValue(endpoint);
        return endpoint;
    }

    _vetisNumber(label, name, value) {
        return Number(label, name, value, undefined, {
            disabled: !this.vetis.enable,
            validate: input => parseInt(input) > 0,
            invalidMessage: this.labels.vetisPositiveNumber
        });
    }

    /// Пока Ветис выключен, адрес сервиса и параметры фоновых задач нельзя менять.
    _setVetisEndpointEnabled(enabled) {
        for (const name of VETIS_FIELDS) {
            const field = $$(name);
            if (!field)
                continue;

            if (enabled)
                field.enable();
            else
                field.disable();
        }
    }

}

export default async function createServerSettingsView(id) {
    const view = new SettingsView(id);
    await view.loadData();
    return view.renderView();
}