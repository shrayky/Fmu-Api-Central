import { CheckBox, Text } from "../../utils/ui.js";

const labels = {
    enable: "Использовать DaData",
    apiKey: "API-ключ",
    secret: "Секретный ключ",
    hint: "Пока флажок выключен, кнопка «Получить» в окне торговой точки скрыта, а загрузка не ищет точку по ФИАС.",
};

export const dadataSettingsFormId = "dadataSettingsForm";

const dadataFields = ["dadataApiKey", "dadataSecret"];

export function setDadataFieldsEnabled(enabled) {
    for (const name of dadataFields) {
        const field = $$(name);
        if (!field)
            continue;

        if (enabled)
            field.enable();
        else
            field.disable();
    }
}

export function createDadataTab(data) {
    const locked = data.enable !== true;

    return {
        header: "Прочие интеграции",
        body: {
            id: dadataSettingsFormId,
            view: "form",
            scroll: true,
            elements: [
                CheckBox(labels.enable, "dadataEnable", {
                    value: data.enable === true,
                    on: {
                        onChange: (enabled) => setDadataFieldsEnabled(!!enabled)
                    }
                }),
                Text(labels.apiKey, "dadataApiKey", data.apiKey || "", {
                    disabled: locked,
                    clear: true
                }),
                Text(labels.secret, "dadataSecret", data.secret || "", {
                    disabled: locked,
                    clear: true
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
