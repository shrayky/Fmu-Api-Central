import { CheckBox, Label, Text, Number } from "../../utils/ui.js";
import { httpAddressValidation } from "../../utils/validators.js";
import { positiveSetting } from "../../utils/vetisFormat.js";

const labels = {
    enable: "Использовать Ветис API",
    endpoint: "Адрес сервиса",
    incoming: "Получение ВСД",
    incomingInterval: "Как часто загружать ВСД (минут)",
    incomingPageSize: "Сколько ВСД за запрос (не больше 100)",
    autoQuench: "Автогашение",
    autoQuenchInterval: "Как часто гасить ВСД (минут)",
    autoQuenchBatch: "Сколько входящих документов гасить за запуск",
    quench: "Гашение",
    requireVehicle: "Запретить гашение без номера автомобиля",
    applications: "Заявки",
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
    "vetisAutoQuenchInterval",
    "vetisAutoQuenchBatch",
    "vetisRequireVehicle",
    "vetisApplicationRetention",
    "vetisFailedApplicationRetention",
];

function vetisNumber(label, name, value, locked) {
    return Number(label, name, value, undefined, {
        disabled: locked,
        validate: (input) => positiveSetting(input),
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
                Label("vetisIncomingTitle", labels.incoming),
                vetisNumber(labels.incomingInterval, "vetisIncomingInterval", data.incomingFetchIntervalMinutes, locked),
                vetisNumber(labels.incomingPageSize, "vetisIncomingPageSize", data.incomingFetchPageSize, locked),
                Label("vetisAutoQuenchTitle", labels.autoQuench),
                vetisNumber(labels.autoQuenchInterval, "vetisAutoQuenchInterval", data.autoQuenchIntervalMinutes ?? 15, locked),
                vetisNumber(labels.autoQuenchBatch, "vetisAutoQuenchBatch", data.autoQuenchBatchSize ?? 20, locked),
                Label("vetisQuenchTitle", labels.quench),
                CheckBox(labels.requireVehicle, "vetisRequireVehicle", {
                    value: !!data.requireVehicleForQuench,
                    disabled: locked
                }),
                Label("vetisApplicationsTitle", labels.applications),
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
