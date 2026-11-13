import { CheckBox, Label, Number } from "../../utils/ui.js";
import { positiveSetting } from "../../utils/vetisFormat.js";

const labels = {
    enable: "Использовать ЭТрН",
    incoming: "Приёмка входящих ЭТрН",
    interval: "Как часто загружать ЭТрН (минут)",
    requests: "Запросы",
    requestRetention: "Хранить выполненные запросы (дней)",
    failedRequestRetention: "Хранить запросы с ошибкой или без ответа (дней)",
    positiveNumber: "Нужно целое число больше нуля",
    hint: "Пока флажок выключен, пункт меню «ЭТрН» и вкладка «ЭТрН» в организации скрыты.",
};

export const etrnSettingsFormId = "etrnSettingsForm";

const etrnFields = [
    "etrnIncomingInterval",
    "etrnRequestRetention",
    "etrnFailedRequestRetention",
];

function etrnNumber(label, name, value, locked) {
    return Number(label, name, value, undefined, {
        disabled: locked,
        validate: (input) => positiveSetting(input),
        invalidMessage: labels.positiveNumber
    });
}

export function setEtrnFieldsEnabled(enabled) {
    for (const name of etrnFields) {
        const field = $$(name);
        if (!field)
            continue;

        if (enabled)
            field.enable();
        else
            field.disable();
    }
}

export function createEtrnTab(data) {
    const locked = data.enable !== true;

    return {
        header: "ЭТрН",
        body: {
            id: etrnSettingsFormId,
            view: "form",
            scroll: true,
            elements: [
                CheckBox(labels.enable, "etrnEnable", {
                    value: data.enable === true,
                    on: {
                        onChange: (enabled) => setEtrnFieldsEnabled(!!enabled)
                    }
                }),
                Label("etrnSettingsTitle", labels.incoming),
                etrnNumber(labels.interval, "etrnIncomingInterval", data.incomingFetchIntervalMinutes ?? 5, locked),
                Label("etrnRequestsTitle", labels.requests),
                etrnNumber(labels.requestRetention, "etrnRequestRetention", data.requestRetentionDays ?? 1, locked),
                etrnNumber(labels.failedRequestRetention, "etrnFailedRequestRetention", data.failedRequestRetentionDays ?? 7, locked),
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
