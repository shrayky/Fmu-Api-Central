/**
 * Webix собирает form.getValues() из DOM и может не отдать поля вкладки,
 * которую ещё не открывали. Поэтому значения читаются по id.
 */
export function readFieldValues(names) {
    const values = {};

    for (const name of names) {
        const field = $$(name);
        if (field)
            values[name] = field.getValue();
    }

    return values;
}
