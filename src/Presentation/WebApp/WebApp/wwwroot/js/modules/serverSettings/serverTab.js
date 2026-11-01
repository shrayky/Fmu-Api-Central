import { Label, Number, CheckBox, Text } from "../../utils/ui.js";

const LOG_LEVELS = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

const labels = {
    port: "Настройка API сервера",
    publication: "Дистрибутив клиента",
    security: "Безопасность",
    logging: "Логирование",
    apiIpPort: "IP-порт API",
    publicAddress: "Адрес этого сервера для клиента",
    corsOrigins: "Дополнительные origin WebApp (по одному в строке). localhost:2580 уже разрешён",
    trustedProxies: "Доверенные прокси (IP Caddy, по одному в строке). 127.0.0.1 уже доверен",
    allowLegacyAgentApi: "Разрешить старый API узлов (без handshake)",
    isEnabled: "Включено",
    logDepth: "Глубина логирования (дней)",
    logLevel: "Уровень логирования",
};

export const serverSettingsFormId = "serverSettingsForm";
const logFieldsId = "loggerSettingsFields";

export function setLogFieldsEnabled(enabled) {
    const fields = $$(logFieldsId);
    if (!fields)
        return;

    if (enabled)
        fields.enable();
    else
        fields.disable();
}

export function createServerTab(data) {
    return {
        header: "Настройки сервера",
        body: {
            id: serverSettingsFormId,
            view: "form",
            scroll: true,
            elements: [
                Label("serverPortTitle", labels.port),
                Number(labels.apiIpPort, "apiIpPort", data.apiIpPort),
                Label("serverPublicationTitle", labels.publication),
                Text(labels.publicAddress, "publicAddress", data.publicAddress, {
                    placeholder: "https://central.example:2579"
                }),
                Label("serverSecurityTitle", labels.security),
                {
                    view: "textarea",
                    label: labels.corsOrigins,
                    labelPosition: "top",
                    id: "corsOrigins",
                    name: "corsOrigins",
                    height: 90,
                    value: data.corsOrigins
                },
                {
                    view: "textarea",
                    label: labels.trustedProxies,
                    labelPosition: "top",
                    id: "trustedProxies",
                    name: "trustedProxies",
                    height: 70,
                    value: data.trustedProxies
                },
                CheckBox(labels.allowLegacyAgentApi, "allowLegacyAgentApi", {
                    value: data.allowLegacyAgentApi
                }),
                Label("serverLoggerTitle", labels.logging),
                CheckBox(labels.isEnabled, "isEnabled", {
                    value: data.isEnabled,
                    on: {
                        onChange: (enabled) => setLogFieldsEnabled(enabled)
                    }
                }),
                {
                    id: logFieldsId,
                    disabled: !data.isEnabled,
                    rows: [
                        Number(labels.logDepth, "logDepth", data.logDepth),
                        {
                            view: "select",
                            label: labels.logLevel,
                            labelPosition: "top",
                            id: "logLevel",
                            name: "logLevel",
                            options: LOG_LEVELS,
                            value: data.logLevel
                        },
                    ]
                },
                {}
            ]
        }
    };
}
