import instanceMonitoringService from '../../services/instanceMonitoringService.js';
import { TextBox } from '../../utils/ui.js';

class InstanceElementView {
    constructor() {
        this.LABELS = {
            instanceName: "Имя инстанса",
            instanceToken: "Токен инстанса",
            formTitle: "Создание инстанса fmu-api",
            invalidNameMessage: "укажите имя",
            invalidTokenMessage: "укажите токен",
        }

        this.NAMES = {
            formId: "instanceForm",
            instanceName: "instanceName",
            instanceToken: "instanceToken",
            copyToken: "copyToken",
        }
    }

    showDialog(onSuccess) {
        webix.ui({
            view: "window",
            id: this.NAMES.formId,
            modal: true,
            width: 500,
            position: "center",
            head: this.LABELS.formTitle,
            body: {
                view: "form",
                id: this.NAMES.formId,
                elements: [
                    TextBox("text", this.LABELS.instanceName, this.NAMES.instanceName, { required: true, invalidMessage: this.LABELS.invalidNameMessage }),
                    //TextBox("text", this.LABELS.instanceToken, this.NAMES.instanceToken, { required: true, invalidMessage: this.LABELS.invalidTokenMessage }),
                    this._createTokenField(),
                    this._createButtons(onSuccess),
                ]
            }
        }).show();

        this._generateToken();
    }

    _createButtons(onSuccess) {
        return {
            cols: [
                { view: "button", value: "Создать", click: () => this._sendNewInstance(onSuccess) },
                { view: "button", value: "Отмена", click: () => $$(this.NAMES.formId).close() }
            ]
        };
    }

    _createTokenField() {
        return {
            view: "forminput",
            label: this.LABELS.instanceToken,
            name: this.NAMES.instanceToken,
            required: true,
            invalidMessage: this.LABELS.invalidTokenMessage,
            body: {
                cols: [
                    {
                        view: "text",
                        id: this.NAMES.instanceToken,
                        placeholder: "Токен будет сгенерирован автоматически"
                    },
                    {
                        view: "button",
                        type: "icon",
                        icon: "wxi-sync",
                        value: this.LABELS.generateToken,
                        width: 40,
                        click: () => this._generateToken()
                    },
                    {
                        id: this.NAMES.copyToken,
                        view: "button",
                        type: "icon",
                        icon: "wxi-checkbox-blank",
                        value: this.LABELS.copyToken,
                        width: 40,
                        click: () => this._copyTokenToClipboard()
                    }
                ]
            }
        }
    }

    async _sendNewInstance(onSuccess) {
        const form = $$("instanceForm", this.NAMES.formId);
        if (!this._validateForm()) return;

        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        const instanceName = $$(this.NAMES.instanceName).getValue();
        const instanceToken = $$(this.NAMES.instanceToken).getValue();

        const instanceData = {
            Name: instanceName,
            Token: instanceToken,
            Version: "-",
            LastUpdated: new Date()
        };

        try {
            const createdInstance = await instanceMonitoringService.create(instanceData);

            if (onSuccess)
                onSuccess(createdInstance);
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            form.enable();
            form.hideProgress();
        }

        form.enable();
        form.hideProgress();
    }

    _validateForm() {
        const instanceName = $$(this.NAMES.instanceName).getValue();

        if (!instanceName || instanceName === "") {
            webix.message({ text: this.LABELS.invalidNameMessage, type: "error" });
            return false;
        }

        return true;

    }

    _generateToken() {
        const tokenField = $$(this.NAMES.instanceToken);
        tokenField.setValue(crypto.randomUUID());
    }

    _copyTokenToClipboard() {
        const tokenField = $$(this.NAMES.instanceToken);
        if (!tokenField)
            return

        const token = tokenField.getValue();

        if (token) {
            navigator.clipboard.writeText(token).then(() => {
                webix.message({ text: "Токен скопирован в буфер обмена", type: "success" });

                const copyTokenBtn = $$(this.NAMES.copyToken);
                if (copyTokenBtn) {
                    copyTokenBtn.config.icon = "wxi-check";
                    copyTokenBtn.refresh();
                }

            }).catch(() => {
                webix.message({ text: "Не удалось скопировать токен", type: "error" });
            });
        } else {
            webix.message({ text: "Сначала сгенерируйте токен", type: "error" });
        }
    }

}

export default new InstanceElementView();