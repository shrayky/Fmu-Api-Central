import { Text, CheckBox } from '../../utils/ui.js';
import instanceGroupService from '../../services/instanceGroupService.js';

class InstanceGroupElementView {
    constructor() {
        this.elementId = "";

        this.LABELS = {
            name: "Имя группы",
            invalidNameMessage: "заполните поле",
            formTitle: "Группа инстансов",
            createButton: "Сохранить",
            cancelButton: "Отмена",
            autoUpdateAllowed: "Автообновление разрешено"
        };

        this.NAMES = {
            formId: "instanceGroupElement",
            name: "instanceGroupName",
            autoUpdateAllowed: "instanceGroupAutoUpdateAllowed"
        };
    }

    showDialog(editedData = {}, onSuccess, onClose) {
        this.elementId = editedData.id || crypto.randomUUID();

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

        const data = {
            id: this.elementId,
            name: $$(this.NAMES.name).getValue(),
            autoUpdateAllowed: !!$$(this.NAMES.autoUpdateAllowed).getValue()
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
}

export default new InstanceGroupElementView();
