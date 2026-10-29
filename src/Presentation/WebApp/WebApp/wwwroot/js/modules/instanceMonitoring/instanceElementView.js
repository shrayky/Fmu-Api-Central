import instanceMonitoringService from '../../services/instanceMonitoringService.js';
import { instanceCardCancelTarget } from './instanceCardCancel.js';
import instanceGroupService from '../../services/instanceGroupService.js';
import { loadConfiguration } from '../../services/ConfigurationService.js';
import { Text, CheckBox } from '../../utils/ui.js';
import { newUuid } from '../../utils/uuid.js';
import { copyText } from '../../utils/copyText.js';

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
            download: "Скачать",
            downloadFile: "Скачать файл",
            downloadLink: "Получить ссылку",
            installCommandTitle: "Команда установки",
            copyCommand: "Скопировать",
            commandCopied: "Команда скопирована в буфер обмена",
            errorCopyCommand: "Не удалось скопировать команду",
            needToken: "Укажите токен инстанса",
            errorDownload: "Не удалось скачать дистрибутив",
            errorCommand: "Не удалось получить команду установки",
            group: "Группа",
            selectGroup: "Выберите группу",
            settingsModified: "Выгрузить настройки"
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
            settingsModified: "instanceSettingsModified",
            downloadBtn: "instanceDownloadBtn",
            downloadMenu: "instanceDownloadMenu",
            installCommandWindow: "instanceInstallCommandWindow",
            installCommandText: "instanceInstallCommandText"
        }
    }

    async showDialog(editedData = [], onSuccess, onClose) {
        this.editedData = editedData && !Array.isArray(editedData) ? editedData : {};
        this._onSuccess = onSuccess;
        this._pendingListNotify = false;
        const groupOptions = await this._loadGroupOptions();
        const canDownload = await this._hasPublicAddress();
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

                    CheckBox(this.LABELS.settingsModified, this.NAMES.settingsModified, {
                        value: !!editedData.settingsModified
                    }),

                    this._createTokenField(editedData.id || ""),

                    this._createButtons(onSuccess, onClose, canDownload),
                ]
            }
        }).show();

        if (canDownload)
            this._keepDownloadMenuToggle();

        if (!this.editedData.id)
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

    _createButtons(onSuccess, onClose, canDownload) {
        const buttons = [];

        if (canDownload) {
            buttons.push({
                view: "button",
                id: this.NAMES.downloadBtn,
                value: this.LABELS.download,
                icon: "wxi-download",
                click: () => this._toggleDownloadMenu()
            });
        }

        buttons.push(
            {
                view: "button",
                value: this.LABELS.createButton,
                click: () => this._sendInstance(onSuccess),
                hotkey: "alt+enter"
            },
            {
                view: "button",
                value: this.LABELS.cancelButton,
                click: () => this._cancelCard(onClose),
                hotkey: "esc"
            }
        );

        return { cols: buttons };
    }

    async _hasPublicAddress() {
        try {
            const result = await loadConfiguration();
            if (!result.result)
                return false;

            const address = result.value?.Content?.serverSettings?.publicAddress;
            return !!(address && String(address).trim());
        } catch {
            return false;
        }
    }

    /** mousedown всплывает до click и закрывает popup как внешний клик. */
    _keepDownloadMenuToggle() {
        const button = $$(this.NAMES.downloadBtn);
        if (!button)
            return;

        webix.event(button.getNode(), "mousedown", (event) => {
            event.stopPropagation();
        });
    }

    _toggleDownloadMenu() {
        const existing = $$(this.NAMES.downloadMenu);
        const button = $$(this.NAMES.downloadBtn);
        if (!button)
            return;

        if (existing) {
            if (existing.isVisible())
                existing.hide();
            else
                existing.show(button.getNode(), { pos: "top" });
            return;
        }

        webix.ui({
            view: "popup",
            id: this.NAMES.downloadMenu,
            width: 200,
            body: {
                view: "list",
                borderless: true,
                autoheight: true,
                yCount: 2,
                data: [
                    { id: "file", value: this.LABELS.downloadFile },
                    { id: "link", value: this.LABELS.downloadLink }
                ],
                on: {
                    onItemClick: (id) => {
                        const menu = $$(this.NAMES.downloadMenu);
                        if (menu)
                            menu.hide();

                        if (id === "file")
                            this._downloadDistribution();
                        else if (id === "link")
                            this._showInstallCommand();
                    }
                }
            }
        }).show(button.getNode(), { pos: "top" });
    }

    async _showInstallCommand() {
        if (!this._isPersisted() && !this._validateForm())
            return;

        const form = $$(this.NAMES.formId);
        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        try {
            if (!await this._ensureSaved())
                return;

            const token = $$(this.NAMES.instanceToken)?.getValue()?.trim();
            if (!token) {
                webix.message({ text: this.LABELS.needToken, type: "error" });
                return;
            }

            const data = await instanceMonitoringService.checkerInstallCommand(token);
            const command = data.value?.command;
            if (!data.result || !command) {
                webix.message({ text: data.error || this.LABELS.errorCommand, type: "error" });
                return;
            }

            this._openInstallCommandWindow(command);
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
        } finally {
            form.enable();
            form.hideProgress();
        }
    }

    _openInstallCommandWindow(command) {
        const existing = $$(this.NAMES.installCommandWindow);
        if (existing)
            existing.destructor();

        webix.ui({
            view: "window",
            id: this.NAMES.installCommandWindow,
            modal: true,
            width: 640,
            position: "center",
            head: this.LABELS.installCommandTitle,
            body: {
                padding: 12,
                rows: [
                    {
                        view: "textarea",
                        id: this.NAMES.installCommandText,
                        readonly: true,
                        value: command,
                        height: 140
                    },
                    {
                        cols: [
                            {
                                view: "checkbox",
                                labelRight: this.LABELS.copyCommand,
                                labelWidth: 0,
                                width: 160,
                                on: {
                                    onChange: (checked) => {
                                        if (checked)
                                            this._copyInstallCommand();
                                    }
                                }
                            },
                            {},
                            {
                                view: "button",
                                value: this.LABELS.cancelButton,
                                width: 120,
                                click: () => $$(this.NAMES.installCommandWindow).close()
                            }
                        ]
                    }
                ]
            }
        }).show();

        setTimeout(() => {
            const input = $$(this.NAMES.installCommandText)?.getInputNode();
            if (!input)
                return;

            input.focus();
            input.select();
        }, 0);
    }

    _copyInstallCommand() {
        const command = $$(this.NAMES.installCommandText)?.getValue();
        if (!command) {
            webix.message({ text: this.LABELS.errorCommand, type: "error" });
            return;
        }

        copyText(command).then(() => {
            webix.message({ text: this.LABELS.commandCopied, type: "success" });
        }).catch(() => {
            webix.message({ text: this.LABELS.errorCopyCommand, type: "error" });
        });
    }

    _cancelCard(onClose) {
        const commandWindow = $$(this.NAMES.installCommandWindow);
        if (instanceCardCancelTarget(!!commandWindow) === "command") {
            commandWindow.close();
            return;
        }

        // Инстанс уже создан ради скачивания: Отмена оставляет его в списке.
        if (this._pendingListNotify && this._onSuccess) {
            this._onSuccess(this.editedData);
            this._pendingListNotify = false;
        }

        if (onClose)
            onClose();

        this._closeDialog();
    }

    _closeDialog() {
        const menu = $$(this.NAMES.downloadMenu);
        if (menu)
            menu.destructor();

        const commandWindow = $$(this.NAMES.installCommandWindow);
        if (commandWindow)
            commandWindow.destructor();

        const dialog = $$(this.NAMES.formId);
        if (dialog)
            dialog.close();
    }

    async _downloadDistribution() {
        if (!this._isPersisted() && !this._validateForm())
            return;

        const form = $$(this.NAMES.formId);
        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        try {
            if (!await this._ensureSaved())
                return;

            const token = $$(this.NAMES.instanceToken)?.getValue()?.trim();
            if (!token) {
                webix.message({ text: this.LABELS.needToken, type: "error" });
                return;
            }

            const data = await instanceMonitoringService.downloadCheckerDistribution(token);
            if (!data.result) {
                webix.message({ text: data.error || this.LABELS.errorDownload, type: "error" });
                return;
            }

            this._downloadBlob(data.value, "fmu-api.zip");
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
        } finally {
            form.enable();
            form.hideProgress();
        }
    }

    _downloadBlob(blob, fileName) {
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
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

    _isPersisted() {
        return !!this.editedData?.id;
    }

    async _ensureSaved() {
        if (this._isPersisted())
            return true;

        await this._writeInstance();
        this._pendingListNotify = true;
        return true;
    }

    async _writeInstance() {
        const instanceData = this._collectInstanceData();
        await instanceMonitoringService.create(instanceData);
        this.editedData = instanceData;
        this._lockToken();
        return instanceData;
    }

    _collectInstanceData() {
        const groupId = $$(this.NAMES.group).getValue() || "";
        const groupList = $$(this.NAMES.group).getList();
        const groupItem = groupId && groupList ? groupList.getItem(groupId) : null;
        const current = this.editedData || {};

        return {
            name: $$(this.NAMES.instanceName).getValue(),
            id: $$(this.NAMES.instanceToken).getValue(),
            version: current.version || "-",
            lastUpdated: current.lastUpdated || new Date("2000-01-01T00:00:00.000Z"),
            secretKey: $$(this.NAMES.secretKey).getValue(),
            address: $$(this.NAMES.instanceAddress).getValue(),
            localModules: current.localModules,
            TsPiots: current.TsPiots,
            forcedUpdateId: current.forcedUpdateId,
            group: {
                id: groupId,
                name: groupId ? (groupItem?.value || "") : ""
            },
            settingsModified: !!$$(this.NAMES.settingsModified).getValue()
        };
    }

    _lockToken() {
        const tokenField = $$(this.NAMES.instanceToken);
        const generateButton = $$(this.NAMES.generateToken);
        if (tokenField)
            tokenField.disable();
        if (generateButton)
            generateButton.disable();
    }

    async _sendInstance(onSuccess) {
        if (!this._validateForm()) return;

        const form = $$(this.NAMES.formId);
        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        try {
            const instanceData = await this._writeInstance();
            this._pendingListNotify = false;

            if (onSuccess)
                onSuccess(instanceData);

            this._closeDialog();
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
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
        tokenField.setValue(newUuid());
    }

    _copyTokenToClipboard() {
        const tokenField = $$(this.NAMES.instanceToken);
        if (!tokenField)
            return

        const token = tokenField.getValue();

        if (token) {
            copyText(token).then(() => {
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