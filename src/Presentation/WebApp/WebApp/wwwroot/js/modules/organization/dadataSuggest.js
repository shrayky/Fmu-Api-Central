/**
 * Подсказки DaData в окне торговой точки. Чистые функции: окно и запросы остаются в organizationSalePoints.js.
 */

export const DADATA_MAX_ADDRESS = 250;

export const DADATA_MESSAGES = {
    emptyAddress: "Укажите адрес",
    houseNotFound: "Дом по адресу не найден",
    longAddress: "Адрес DaData длиннее 250 символов",
    unavailable: "DaData недоступна",
    selectHouse: "Выберите дом",
};

export function dadataEnabled(configuration) {
    return configuration?.dadataSettings?.enable === true;
}

/**
 * Дома из ответа API: по одной строке на дом. Строка без ФИАС отбрасывается.
 * Длинный адрес из списка убирается, короткие дома остаются.
 */
export function dadataHouses(value) {
    const houses = (Array.isArray(value) ? value : [])
        .map((row) => ({
            fiasId: String(row?.fiasId || "").trim(),
            address: String(row?.address || "").trim()
        }))
        .filter((row) => row.fiasId.length > 0);

    const fits = houses.filter((row) => row.address.length <= DADATA_MAX_ADDRESS);
    if (fits.length > 0)
        return { houses: fits, error: "" };

    return {
        houses: [],
        error: houses.length > 0 ? DADATA_MESSAGES.longAddress : DADATA_MESSAGES.houseNotFound
    };
}

/**
 * Что писать в поля окна при выборе дома. Длинный адрес поля не меняет.
 */
export function dadataHouseValues(house) {
    const fiasId = String(house?.fiasId || "").trim();
    const address = String(house?.address || "").trim();
    if (fiasId.length === 0)
        return { error: DADATA_MESSAGES.houseNotFound };

    if (address.length > DADATA_MAX_ADDRESS)
        return { error: DADATA_MESSAGES.longAddress };

    return { fiasId, address };
}

/**
 * Что делать с ответом: заполнить поля, показать список домов или сообщить об ошибке.
 * Ошибка длинного адреса и «дом не найден» поля не меняют.
 */
export function dadataSelection(value) {
    const loaded = dadataHouses(value);
    if (loaded.error)
        return { error: loaded.error, houses: [], values: null };

    if (loaded.houses.length === 1) {
        const values = dadataHouseValues(loaded.houses[0]);
        if (values.error)
            return { error: values.error, houses: [], values: null };

        return { error: "", houses: [], values };
    }

    return { error: "", houses: loaded.houses, values: null };
}

/**
 * Поле ФИАС с кнопкой «Получить». Выключенная интеграция оставляет только поле.
 */
export function salePointFiasRow({ enabled, fiasId, label, name, buttonId, spinnerId, buttonLabel, onLoad }) {
    const field = {
        view: "text",
        id: name,
        label,
        labelPosition: "top",
        attributes: { maxlength: 36 },
        clear: true,
        value: fiasId || ""
    };

    if (!enabled)
        return field;

    return {
        cols: [
            field,
            {
                // Свободная строка сверху прижимает кнопку к нижней границе поля:
                // высота кнопки равна полю ввода, а не подписи над ним.
                rows: [
                    { gravity: 1 },
                    {
                        view: "button",
                        id: buttonId,
                        value: buttonLabel,
                        width: 130,
                        click: () => onLoad?.()
                    },
                    {
                        // Пока идёт запрос, вместо кнопки крутится индикатор ожидания.
                        view: "template",
                        id: spinnerId,
                        width: 130,
                        height: 38,
                        hidden: true,
                        borderless: true,
                        template: "<span class='webix_icon wxi-sync webix_spin'></span>",
                        css: {
                            display: "flex",
                            "align-items": "center",
                            "justify-content": "center"
                        }
                    }
                ]
            },
            {}
        ]
    };
}
