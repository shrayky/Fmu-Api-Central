import instanceMonitoringService from '../../services/instanceMonitoringService.js';
import { Text } from '../../utils/ui.js';

class InstanceElementView {
    constructor() {
        this.LABELS = {
            instanceName: "Имя инстанса",
            instanceToken: "Токен инстанса",
            secretKey: "Секретный ключ",
            secretKeyMessage: "если не указан, то пакет бует приниматься без расшифровки",
            formTitle: "Создание инстанса fmu-api",
            invalidNameMessage: "укажите имя",
            invalidTokenMessage: "укажите токен",
        }

        this.NAMES = {
            formId: "instanceForm",
            instanceName: "instanceName",
            instanceToken: "instanceToken",
            copyToken: "copyToken",
            generateToken: "generateToken",
            secretKey: "secretKey",
        }
    }

    showDialog(editedData = [], onSuccess) {
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
                    Text(this.LABELS.instanceName,
                         this.NAMES.instanceName,
                         editedData.name,
                         { required: true, invalidMessage: this.LABELS.invalidNameMessage }
                        ),

                    Text(this.LABELS.secretKey,
                         this.NAMES.secretKey,
                         editedData.secretKey,
                         { required: false, placeholder: this.LABELS.secretKeyMessage }
                        ),

                    this._createTokenField(editedData.id || ""),

                    this._createButtons(onSuccess),
                ]
            }
        }).show();

        if (!editedData.id) 
            this._generateToken();
        else {
            $$(this.NAMES.instanceToken).disable();
            $$(this.NAMES.generateToken).disable();
        }

        setTimeout(() => {
            const nameField = $$(this.NAMES.instanceName);
            if (nameField) {
                nameField.focus();
            }
        }, 100);
    }

    _createButtons(onSuccess) {
        return {
            cols: [
                { 
                    view: "button",
                    value: "Создать",
                    click: () => this._sendInstance(onSuccess),
                    hotkey: "alt+enter"
                },
                { 
                    view: "button",
                    value: "Отмена",
                    click: () => $$(this.NAMES.formId).close(),
                    hotkey: "esc"
                }
            ]
        };
    }

    _createTokenField(token = "") {
        return {
            view: "forminput",
            label: this.LABELS.instanceToken,
            labelPosition: "top",
            name: this.NAMES.instanceToken,
            required: true,
            invalidMessage: this.LABELS.invalidTokenMessage,
            body: {
                cols: [
                    {
                        view: "text",
                        id: this.NAMES.instanceToken,
                        placeholder: "Токен будет сгенерирован автоматически",
                        value: token,
                    },
                    {
                        id: this.NAMES.generateToken,
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

    async _sendInstance(onSuccess) {
        const form = $$("instanceForm", this.NAMES.formId);
        if (!this._validateForm()) return;

        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        const instanceName = $$(this.NAMES.instanceName).getValue();
        const instanceToken = $$(this.NAMES.instanceToken).getValue();
        const instanceSecretKey = $$(this.NAMES.secretKey).getValue();

        const instanceData = {
            name: instanceName,
            id: instanceToken,
            version: "-",
            lastUpdated: new Date(),
            secretKey: instanceSecretKey
        };

        try {
            await instanceMonitoringService.create(instanceData);

            if (onSuccess) {
                onSuccess(instanceData);
            }

            $$("instanceForm", this.NAMES.formId).close();

        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            form.enable();
            form.hideProgress();

            form.enable();
            form.hideProgress();
        }
    }

    _validateForm() {
        const instanceName = $$(this.NAMES.instanceName).getValue();

        if (!instanceName || instanceName === "") {
            webix.message({ text: this.LABELS.invalidNameMessage, type: "error" });
            return false;
        }

        const instanceToken = $$(this.NAMES.instanceToken).getValue();

        if (!instanceToken || instanceToken === "") {
            this._generateToken();
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