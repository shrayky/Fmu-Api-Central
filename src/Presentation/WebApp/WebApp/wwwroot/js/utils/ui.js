import { OnDeleteAllRows, OnDeleteRow, OnAddRow } from "./tables.js";

export const padding = {
    top: 5,
    bottom: 5,
    left: 20,
    right: 20
};

/**
 * Браузер не должен сам заполнять поля приложения сохранёнными логинами и паролями.
 *
 * Webix рисует поля обычными <input>, и если в форме есть текстовое поле и поле пароля,
 * Chrome, Edge и Яндекс считают её формой входа: в «ИНН организации» прилетает логин
 * из сохранённой пары, в «Пароль от ЭЦП» и «Пароль API» — пароль.
 *
 * autocomplete=off Chrome для полей входа игнорирует, а new-password понимает:
 * это поле смены пароля, подставлять в него сохранённый пароль нельзя.
 * data-* — подсказки менеджерам паролей (LastPass, 1Password, Bitwarden).
 */
const AUTOFILL_OFF = {
    autocomplete: "off",
    "data-lpignore": "true",
    "data-1p-ignore": "true",
    "data-bwignore": "true"
};

const AUTOFILL_SECRET = {
    ...AUTOFILL_OFF,
    autocomplete: "new-password"
};

export function TableToolbar(tableName, prms = {}) {
    let newElement = {
        cols: [
            {
                view: "button",
                value: "Добавить",
                id: `add_${tableName}`,
                tableId: tableName,
                autowidth: "false",
                width: 400,
                click: OnAddRow,
            },
            {
                view: "button",
                value: "Удалить",
                id: `delete_${tableName}`,
                tableId: tableName,
                autowidth: "false",
                width: 400,
                disabled: true,
                click: OnDeleteRow
            },
            {
                view: "button",
                value: "Удалить все",
                id: `deleteAll_${tableName}`,
                tableId: tableName,
                autowidth: "false",
                width: 400,
                disabled: false,
                click: OnDeleteAllRows
            },
            {}
        ]

    }
    return applyParameters(newElement, prms);
}

export function Label(id, label, prms = {}) {
    let newElement = {
        view: "label",
        id: id,
        label: label
    };

    return applyParameters(newElement, prms);
}

export function LabelItalic(id, label) {
    let newElement = {
        view: "label",
        id: id,
        label: label,
        css: { "font-style": "italic", "font-size": 8 },
    }

    return applyParameters(newElement, prms);
}

export function TextBox(type, label, name, prms = {}) {
    //let id = `tb_${name}`;
    let id = name;

    let newElement = {
        view: "text",
        type: type,
        label: label,
        labelPosition: "top",
        id: id,
        name: name,
        attributes: { ...AUTOFILL_OFF },
    }

    return applyParameters(newElement, prms);

}

export function Text(label, name, value, prms = {}) {
    let id = name;

    let newElement = {
        view: "text",
        type: "text",
        value: value,
        label: label,
        labelPosition: "top",
        id: id,
        name: name,
        attributes: { ...AUTOFILL_OFF },
    }

    return applyParameters(newElement, prms);
}

export function Number(label, name, value, format, prms = {}) {
    let id = name;

    let newElement = {
        view: "text",
        type: "number",
        id: id,
        name: name,
        value: value,
        label: label,
        labelPosition: "top",
        "format": format,
        attributes: { ...AUTOFILL_OFF },
    }

    return applyParameters(newElement, prms);
}

export function PasswordBox(label, name, prms = {}) {
    //let id = `pb_${name}`;
    let id = name;

    let newElement  = {
        view: "text",
        type: "password",
        label: label,
        labelPosition: "top",
        id: id,
        name: name,
        attributes: { ...AUTOFILL_SECRET },
    }

    return applyParameters(newElement, prms);
}

export function TextBoxFormated(type, label, name, format, prms = {}) {
    //let id = `tbf_${name}`;
    let id = name;

    let newElement = {
        view: "text",
        type: type,
        format: format,
        label: label,
        labelPosition: "top",
        id: id,
        name: name,
        attributes: { ...AUTOFILL_OFF },
    }

    return applyParameters(newElement, prms);
}

export function CheckBox(label, name, prms = {}) {
    //let id = `cb_${name}`;
    let id = name;

    let newElement = {
        view: "checkbox",
        label: label,
        labelWidth: "auto",
        labelPosition: "left",
        id: id,
        name: name,
    }

    return applyParameters(newElement, prms);
}

function applyParameters(newElement, prms) {
    for (let prm in prms) {
        // attributes копятся: значения по умолчанию из фабрики не должны теряться.
        if (prm === "attributes" && newElement.attributes) {
            newElement.attributes = { ...newElement.attributes, ...prms.attributes };
            continue;
        }

        newElement[prm] = prms[prm];
    }

    return newElement;
}