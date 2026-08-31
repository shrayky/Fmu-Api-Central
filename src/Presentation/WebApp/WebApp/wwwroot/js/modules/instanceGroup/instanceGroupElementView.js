import { Text, CheckBox } from '../../utils/ui.js';
import instanceGroupService from '../../services/instanceGroupService.js';
import settingsSchemaService from '../../services/settingsSchemaService.js';

class InstanceGroupElementView {
    constructor() {
        this.elementId = "";

        this.LABELS = {
            name: "Имя группы",
            invalidNameMessage: "заполните поле",
            formTitle: "Группа инстансов",
            createButton: "Сохранить",
            cancelButton: "Отмена",
            autoUpdateAllowed: "Автообновление разрешено",
            settingsSchema: "Схема настроек",
            selectSchema: "Выберите схему"
        };

        this.NAMES = {
            formId: "instanceGroupElement",
            name: "instanceGroupName",
            autoUpdateAllowed: "instanceGroupAutoUpdateAllowed",
            settingsSchema: "instanceGroupSettingsSchema"
        };
    }

    async showDialog(editedData = {}, onSuccess, onClose) {
        this.elementId = editedData.id || crypto.randomUUID();
        const schemaOptions = await this._loadSchemaOptions();
        const currentSchemaId = editedData.settingsSchema?.id || "";

        webix.ui({
            view: "window",
            id: this.NAMES.formId,
            modal: true,
            width: 480,
            position: "center",
            head: this.LABELS.formTitle,
            body: {
                view: "form",
                id: this.NAMES.formId,
                elements: [
                    Text(
                        this.LABELS.name,
                        this.NAMES.name,
                        editedData.name,
                        { required: true, invalidMessage: this.LABELS.invalidNameMessage }
                    ),
                    CheckBox(this.LABELS.autoUpdateAllowed, this.NAMES.autoUpdateAllowed, {
                        value: !!editedData.autoUpdateAllowed
                    }),
                    {
                        view: "richselect",
                        label: this.LABELS.settingsSchema,
                        labelPosition: "top",
                        id: this.NAMES.settingsSchema,
                        name: this.NAMES.settingsSchema,
                        value: currentSchemaId,
                        options: schemaOptions
                    },
                    this._createButtons(onSuccess, onClose)
                ]
            }
        }).show();

        setTimeout(() => {
            const nameField = $$(this.NAMES.name);
            if (nameField) {
                nameField.focus();
            }
        }, 100);
    }

    _createButtons(onSuccess, onClose) {
        return {
            cols: [
                {},
                {
                    view: "button",
                    value: this.LABELS.createButton,
                    click: () => this._send(onSuccess),
                    hotkey: "alt+enter"
                },
                {
                    view: "button",
                    value: this.LABELS.cancelButton,
                    click: () => {
                        if (onClose) {
                            onClose();
                        }

                        $$(this.NAMES.formId).close();
                    },
                    hotkey: "esc"
                }
            ]
        };
    }

    async _send(onSuccess) {
        const form = $$(this.NAMES.formId);
        if (!this._validate()) {
            return;
        }

        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        const schemaId = $$(this.NAMES.settingsSchema).getValue() || "";
        const schemaList = $$(this.NAMES.settingsSchema).getList();
        const schemaItem = schemaId && schemaList ? schemaList.getItem(schemaId) : null;

        const data = {
            id: this.elementId,
            name: $$(this.NAMES.name).getValue(),
            autoUpdateAllowed: !!$$(this.NAMES.autoUpdateAllowed).getValue(),
            settingsSchema: {
                id: schemaId,
                name: schemaId ? (schemaItem?.value || "") : ""
            }
        };

        try {
            await instanceGroupService.create(data);

            if (onSuccess) {
                onSuccess(data);
            }

            $$(this.NAMES.formId).close();
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            form.enable();
            form.hideProgress();
        }
    }

    _validate() {
        const name = $$(this.NAMES.name).getValue();
        if (!name || name === "") {
            webix.message({ text: this.LABELS.invalidNameMessage, type: "error" });
            return false;
        }

        return true;
    }

    async _loadSchemaOptions() {
        try {
            const schemas = await settingsSchemaService.allLinks();
            return [
                { id: "", value: this.LABELS.selectSchema },
                ...schemas.map((schema) => ({ id: schema.id, value: schema.name }))
            ];
        } catch (error) {
            webix.message({ text: `Ошибка загрузки схем: ${error.message}`, type: "error" });
            return [{ id: "", value: this.LABELS.selectSchema }];
        }
    }
}

export default new InstanceGroupElementView();
