import { CheckBox, Text, Number } from "../../utils/ui.js";
import { httpAddressValidation } from "../../utils/validators.js";

const labels = {
    enable: "Использовать Ветис API",
    endpoint: "Адрес сервиса",
    incomingInterval: "Как часто загружать ВСД (минут)",
    incomingPageSize: "Сколько ВСД за запрос (не больше 100)",
    applicationRetention: "Хранить выполненные заявки (дней)",
    failedApplicationRetention: "Хранить заявки с ошибкой или без ответа (дней)",
    hint: "Пока флажок выключен, адрес недоступен, вкладка «Ветис» в организации и пункт меню «Ветис» скрыты.",
    positiveNumber: "Нужно целое число больше нуля",
};

export const vetisSettingsFormId = "vetisSettingsForm";

const vetisFields = [
    "vetisEndpoint",
    "vetisIncomingInterval",
    "vetisIncomingPageSize",
    "vetisApplicationRetention",
    "vetisFailedApplicationRetention",
];

function vetisNumber(label, name, value, locked) {
    return Number(label, name, value, undefined, {
        disabled: locked,
        validate: (input) => parseInt(input) > 0,
        invalidMessage: labels.positiveNumber
    });
}

export function setVetisFieldsEnabled(enabled) {
    for (const name of vetisFields) {
        const field = $$(name);
        if (!field)
            continue;

        if (enabled)
            field.enable();
        else
            field.disable();
    }
}

export function createVetisTab(data) {
    const locked = !data.enable;

    return {
        header: "Ветис",
        body: {
            id: vetisSettingsFormId,
            view: "form",
            scroll: true,
            elements: [
                CheckBox(labels.enable, "vetisEnable", {
                    value: data.enable,
                    on: {
                        onChange: (enabled) => setVetisFieldsEnabled(!!enabled)
                    }
                }),
                Text(labels.endpoint, "vetisEndpoint", data.endpoint, {
                    ...httpAddressValidation,
                    disabled: locked
                }),
                vetisNumber(labels.incomingInterval, "vetisIncomingInterval", data.incomingFetchIntervalMinutes, locked),
                vetisNumber(labels.incomingPageSize, "vetisIncomingPageSize", data.incomingFetchPageSize, locked),
                vetisNumber(labels.applicationRetention, "vetisApplicationRetention", data.applicationRetentionDays, locked),
                vetisNumber(labels.failedApplicationRetention, "vetisFailedApplicationRetention", data.failedApplicationRetentionDays, locked),
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
