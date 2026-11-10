import { Label, Number, PasswordBox, Text } from "../../utils/ui.js";
import { positiveSetting } from "../../utils/vetisFormat.js";

const labels = {
    interval: "Как часто загружать ЭТрН (минут)",
    hint: "Интервал применяется без перезапуска службы. client_id и секрет — учётка интегратора Платформы ОФД. Пустой секрет уже сохранённый не стирает.",
    positiveNumber: "Нужно целое число больше нуля",
    ofd: "Платформа ОФД",
    clientId: "client_id",
    clientSecret: "client_secret",
    keycloak: "Адрес Keycloak",
    realm: "Область",
    api: "Адрес API",
};

export const etrnSettingsFormId = "etrnSettingsForm";

export function createEtrnTab(data) {
    return {
        header: "ЭТрН",
        body: {
            id: etrnSettingsFormId,
            view: "form",
            scroll: true,
            elements: [
                Label("etrnSettingsTitle", "Приёмка входящих ЭТрН"),
                Number(labels.interval, "etrnIncomingInterval", data.incomingFetchIntervalMinutes ?? 5, undefined, {
                    validate: (input) => positiveSetting(input),
                    invalidMessage: labels.positiveNumber
                }),
                Label("etrnOfdTitle", labels.ofd),
                Text(labels.clientId, "ofdClientId", data.ofdClientId || ""),
                PasswordBox(labels.clientSecret, "ofdClientSecret", { value: data.ofdClientSecret || "" }),
                Text(labels.keycloak, "ofdKeycloakUrl", data.ofdKeycloakUrl || ""),
                Text(labels.realm, "ofdRealm", data.ofdRealm || ""),
                Text(labels.api, "ofdApiUrl", data.ofdApiUrl || ""),
                {
                    view: "template",
                    autoheight: true,
                    borderless: true,
                    template: labels.hint
                },
                {}
            ]
        }
    };
}
