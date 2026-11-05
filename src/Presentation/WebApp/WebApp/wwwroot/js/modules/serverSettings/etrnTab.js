import { Label, Number } from "../../utils/ui.js";
import { positiveSetting } from "../../utils/vetisFormat.js";

const labels = {
    interval: "Как часто загружать ЭТрН (минут)",
    hint: "Новое значение применяется без перезапуска службы.",
    positiveNumber: "Нужно целое число больше нуля",
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
