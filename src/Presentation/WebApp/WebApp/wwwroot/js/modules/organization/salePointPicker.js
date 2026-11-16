import { salePointLabel, salePointPickerOptions } from "../../utils/salePointFormat.js";

/**
 * Строка «Торговая точка» в карточке входящего документа.
 * Шапка документа скрывает пустые строки, поэтому поле стоит отдельной строкой.
 */
export function salePointBar({ prefix, document, assign, addAlias, onChanged }) {
    const state = { document };
    const valueId = `${prefix}SalePointValue`;
    const chooseId = `${prefix}SalePointChoose`;
    const editId = `${prefix}SalePointEdit`;

    const open = () => openPicker({ prefix, state, assign, addAlias, onChanged });

    return {
        view: "toolbar",
        elements: [
            {
                view: "template",
                id: valueId,
                borderless: true,
                width: 520,
                template: () => salePointLine(state.document)
            },
            {
                view: "button",
                id: chooseId,
                value: "Выбрать",
                width: 120,
                css: "webix_secondary",
                hidden: !!state.document.salePointUid,
                click: open
            },
            {
                view: "icon",
                id: editId,
                icon: "wxi-pencil",
                tooltip: "Изменить торговую точку",
                width: 34,
                hidden: !state.document.salePointUid,
                click: open
            },
            {}
        ]
    };
}

function salePointLine(document) {
    const label = salePointLabel(document);
    return `<div><b>Торговая точка:</b> ${label ? webix.template.escape(label) : ""}</div>`;
}

function openPicker({ prefix, state, assign, addAlias, onChanged }) {
    const windowId = `${prefix}SalePointPicker`;
    const listId = `${prefix}SalePointList`;
    $$(windowId)?.destructor();

    const picker = webix.ui({
        view: "window",
        id: windowId,
        modal: true,
        move: true,
        position: "center",
        width: 460,
        height: 380,
        head: {
            view: "toolbar",
            elements: [
                { view: "label", label: "Торговая точка" },
                { view: "icon", icon: "wxi-close", click: () => $$(windowId)?.close() }
            ]
        },
        body: {
            view: "list",
            id: listId,
            select: true,
            data: salePointPickerOptions(state.document.salePoints),
            on: {
                onItemClick(id) {
                    const item = this.getItem(id);
                    if (!item)
                        return;

                    const selected = String(item.uid || "").trim();
                    const wasEmpty = !state.document.salePointUid;
                    this.disable();
                    assign(selected)
                        .then((updated) => {
                            state.document = updated;
                            applySelection(prefix, updated);
                            $$(windowId)?.close();
                            if (onChanged)
                                onChanged(updated);
                            return suggestAlias({ addAlias, updated, selected, wasEmpty });
                        })
                        .catch((error) => {
                            webix.message({
                                text: error?.message || "Не удалось сохранить торговую точку",
                                type: "error"
                            });
                            this.enable();
                        });
                }
            }
        }
    });

    picker.show();
}

/**
 * Пустое поле было заполнено вручную: предлагаем записать написание адреса синонимом точки,
 * чтобы следующий документ с этим адресом подобрался сам.
 */
async function suggestAlias({ addAlias, updated, selected, wasEmpty }) {
    if (!addAlias || !selected || !wasEmpty || !updated?.salePointUid)
        return;

    const name = webix.template.escape(updated.salePointName || "");
    const confirmed = await webix.confirm({
        title: "Синоним адреса",
        text: `Добавить адрес документа в синонимы точки «${name}»?`,
        ok: "Добавить",
        cancel: "Нет"
    }).then(() => true).catch(() => false);

    if (!confirmed)
        return;

    try {
        await addAlias(updated.id);
    } catch (error) {
        webix.message({ text: error?.message || "Не удалось добавить синоним адреса", type: "error" });
    }
}

function applySelection(prefix, document) {    const value = $$(`${prefix}SalePointValue`);
    if (value) {
        value.define("template", salePointLine(document));
        value.refresh();
    }

    const choose = $$(`${prefix}SalePointChoose`);
    if (choose) {
        if (document.salePointUid)
            choose.hide();
        else
            choose.show();
    }

    const edit = $$(`${prefix}SalePointEdit`);
    if (edit) {
        if (document.salePointUid)
            edit.show();
        else
            edit.hide();
    }
}
