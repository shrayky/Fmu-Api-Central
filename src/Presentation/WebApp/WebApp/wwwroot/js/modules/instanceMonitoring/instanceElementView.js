import instanceMonitoringService from '../../services/instanceMonitoringService.js';
import instanceGroupService from '../../services/instanceGroupService.js';
import { Text } from '../../utils/ui.js';

class InstanceElementView {
    constructor() {
        this.editedData = {};

        this.LABELS = {
            instanceName: "Имя инстанса",
            instanceNetAddress: "Адрес инстанса",
            instanceToken: "Токен инстанса",
            secretKey: "Секретный ключ",
            secretKeyMessage: "если не указан, то пакет бует приниматься без расшифровки",
            formTitle: "Создание инстанса fmu-api",
            invalidNameMessage: "укажите имя",
            invalidTokenMessage: "укажите токен",
            createButton: "Сохранить",
            cancelButton: "Отмена",
            group: "Группа",
            selectGroup: "Выберите группу"
        }

        this.NAMES = {
            formId: "instanceForm",
            instanceName: "instanceName",
            instanceAddress: "instanceAddress",
            instanceToken: "instanceToken",
            copyToken: "copyToken",
            generateToken: "generateToken",
            secretKey: "secretKey",
            group: "instanceGroup",
        }
    }

    async showDialog(editedData = [], onSuccess, onClose) {
        this.editedData = editedData || {};
        const groupOptions = await this._loadGroupOptions();
        const currentGroupId = this.editedData.group?.id || "";

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

                    Text(
                        this.LABELS.instanceNetAddress,
                        this.NAMES.instanceAddress,
                        editedData.address,
                        {
                            placeholder: "http://fmu-api-server:2578"
                        }
                    ),

                    Text(this.LABELS.secretKey,
                         this.NAMES.secretKey,
                         editedData.secretKey,
                         { required: false, placeholder: this.LABELS.secretKeyMessage }
                        ),

                    {
                        view: "richselect",
                        label: this.LABELS.group,
                        labelPosition: "top",
                        id: this.NAMES.group,
                        name: this.NAMES.group,
                        value: currentGroupId,
                        options: groupOptions
                    },

                    this._createTokenField(editedData.id || ""),

                    this._createButtons(onSuccess, onClose),
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

    _createButtons(onSuccess, onClose) {
        return {
            cols: [
                { 
                    view: "button",
                    value: this.LABELS.createButton,
                    click: () => this._sendInstance(onSuccess),
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
        const instanceAddress = $$(this.NAMES.instanceAddress).getValue();
        const groupId = $$(this.NAMES.group).getValue() || "";
        const groupList = $$(this.NAMES.group).getList();
        const groupItem = groupId && groupList ? groupList.getItem(groupId) : null;
        const current = this.editedData || {};

        const instanceData = {
            name: instanceName,
            id: instanceToken,
            version: current.version || "-",
            lastUpdated: current.lastUpdated || new Date("2000-01-01T00:00:00.000Z"),
            secretKey: instanceSecretKey,
            address: instanceAddress,
            localModules: current.localModules,
            TsPiots: current.TsPiots,
            forcedUpdateId: current.forcedUpdateId,
            group: {
                id: groupId,
                name: groupId ? (groupItem?.value || "") : ""
            }
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

    async _loadGroupOptions() {
        try {
            const groups = await instanceGroupService.allLinks();
            return [
                { id: "", value: this.LABELS.selectGroup },
                ...groups.map((group) => ({ id: group.id, value: group.name }))
            ];
        } catch (error) {
            webix.message({ text: `Ошибка загрузки групп: ${error.message}`, type: "error" });
            return [{ id: "", value: this.LABELS.selectGroup }];
        }
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