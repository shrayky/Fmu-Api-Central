import { Text, CheckBox, Number } from '../../utils/ui.js';
import { newUuid } from '../../utils/uuid.js';
import instanceGroupService from '../../services/instanceGroupService.js';
import organizationService from '../../services/organizationService.js';
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
            selectSchema: "Выберите схему",
            mainTab: "Основная",
            organizationsTab: "Организации",
            organizationName: "Наименование",
            organizationInn: "ИНН",
            channelTab: "Канал оповещения",
            channelEnabled: "Использовать",
            provider: "Протокол",
            chatId: "ID чата",
            botToken: "Токен бота",
            test: "Тест",
            botNotConnected: "Бот не подключен",
            chatIdZero: "ID чата не может быть 0",
            testSuccess: "Тест выполнен",
            checkerSettingsTitle: "Шаблон настроек FMU-API",
            settingsMissing: "Шаблон настроек не загружен",
            settingsLoaded: "Шаблон настроек загружен",
            replaceSettings: "Заменить шаблон настроек",
            removeSettings: "Удалить шаблон настроек",
            uploadError: "Не удалось загрузить шаблон настроек",
            removeError: "Не удалось удалить шаблон настроек",
            confirmRemoveTitle: "Удаление шаблона настроек",
            confirmRemoveText: "Удалить загруженный шаблон настроек?",
            confirmYes: "Да",
            confirmNo: "Нет"
        };

        this.NAMES = {
            windowId: "instanceGroupWindow",
            formId: "instanceGroupElement",
            channelFormId: "instanceGroupChannelForm",
            name: "instanceGroupName",
            autoUpdateAllowed: "instanceGroupAutoUpdateAllowed",
            settingsSchema: "instanceGroupSettingsSchema",
            organizationsTable: "instanceGroupOrganizationsTable",
            channelEnabled: "instanceGroupChannelEnabled",
            channelFields: "instanceGroupChannelFields",
            provider: "instanceGroupProvider",
            chatId: "instanceGroupChatId",
            botToken: "instanceGroupBotToken",
            checkerSettings: "instanceGroupCheckerSettings",
            checkerSettingsStatus: "instanceGroupCheckerSettingsStatus"
        };

        webix.html.addStyle(
            ".instance-group-checker-settings legend{color:#d7dce5;font-weight:600;}" +
            ".instance-group-checker-settings .webix_template{padding:6px 0 2px 0;}" +
            ".instance-group-checker-settings__upload{color:#ff6b6b;text-decoration:underline;cursor:pointer;}" +
            ".instance-group-checker-settings__upload:hover{color:#ff9a9a;}" +
            ".instance-group-checker-settings__loaded{color:#4dabf7;text-decoration:underline;cursor:pointer;}" +
            ".instance-group-checker-settings__loaded:hover{color:#8cc8ff;}" +
            ".instance-group-checker-settings__remove{cursor:pointer;margin-left:10px;color:#8a93a3;}" +
            ".instance-group-checker-settings__remove:hover{color:#ff6b6b;}",
            "instance-group-checker-settings-styles"
        );
    }

    async showDialog(editedData = {}, onSuccess, onClose) {
        const checkerSettingsRequest = this._nextCheckerSettingsRequest();
        this._saved = !!editedData.id;
        this._hasCheckerSettings = !!editedData.hasCheckerSettings;
        this._checkerSettingsTouched = false;
        this.elementId = editedData.id || newUuid();
        const schemaOptions = await this._loadSchemaOptions();
        const currentSchemaId = editedData.settingsSchema?.id || "";
        const organizationRows = await this._loadOrganizationRows(editedData.organizationIds || []);
        const channel = editedData.alertChannel || {};
        const isEnabled = !!channel.isEnabled;

        if ($$(this.NAMES.windowId)) {
            $$(this.NAMES.windowId).destructor();
        }

        webix.ui({
            view: "window",
            id: this.NAMES.windowId,
            modal: true,
            width: 720,
            position: "center",
            head: this.LABELS.formTitle,
            on: {
                onDestruct: () => {
                    if (this._checkerSettingsRequest === checkerSettingsRequest)
                        this._nextCheckerSettingsRequest();
                }
            },
            body: {
                rows: [
                    {
                        view: "tabview",
                        cells: [
                            {
                                header: this.LABELS.mainTab,
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
                                        ...(this._saved ? [this._checkerSettingsBlock()] : [])
                                    ]
                                }
                            },
                            {
                                header: this.LABELS.organizationsTab,
                                body: {
                                    padding: 10,
                                    rows: [
                                        this._organizationsTable(organizationRows)
                                    ]
                                }
                            },
                            {
                                header: this.LABELS.channelTab,
                                body: {
                                    view: "form",
                                    id: this.NAMES.channelFormId,
                                    elements: [
                                        CheckBox(this.LABELS.channelEnabled, this.NAMES.channelEnabled, {
                                            value: isEnabled,
                                            on: {
                                                onChange: (enabled) => this._setChannelFieldsEnabled(enabled)
                                            }
                                        }),
                                        {
                                            id: this.NAMES.channelFields,
                                            disabled: !isEnabled,
                                            rows: [
                                                {
                                                    view: "richselect",
                                                    label: this.LABELS.provider,
                                                    labelPosition: "top",
                                                    id: this.NAMES.provider,
                                                    name: this.NAMES.provider,
                                                    value: channel.provider || "telegram",
                                                    options: [
                                                        { id: "telegram", value: "telegram" },
                                                        { id: "max", value: "max" },
                                                        { id: "ntfy", value: "ntfy" }
                                                    ]
                                                },
                                                Number(this.LABELS.chatId, this.NAMES.chatId, channel.chatId || 0),
                                                Text(this.LABELS.botToken, this.NAMES.botToken, channel.botToken || "")
                                            ]
                                        },
                                        {
                                            cols: [
                                                {
                                                    view: "button",
                                                    value: this.LABELS.test,
                                                    width: 120,
                                                    click: () => this._testChannel()
                                                },
                                                {}
                                            ]
                                        }
                                    ]
                                }
                            }
                        ]
                    },
                    this._createButtons(onSuccess, onClose)
                ]
            }
        }).show();

        if (this._saved) {
            this._bindCheckerSettingsEvents();
            void this._refreshCheckerSettingsState(checkerSettingsRequest);
        }

        setTimeout(() => {
            const nameField = $$(this.NAMES.name);
            if (nameField) {
                nameField.focus();
            }
        }, 100);
    }

    _checkerSettingsBlock() {
        return {
            view: "fieldset",
            id: this.NAMES.checkerSettings,
            label: this.LABELS.checkerSettingsTitle,
            css: "instance-group-checker-settings",
            body: {
                rows: [
                    {
                        view: "template",
                        id: this.NAMES.checkerSettingsStatus,
                        borderless: true,
                        autoheight: true,
                        template: () => this._checkerSettingsMarkup(),
                        on: {
                            onAfterRender: () => this._bindCheckerSettingsEvents()
                        }
                    }
                ]
            }
        };
    }

    _checkerSettingsMarkup() {
        if (this._hasCheckerSettings) {
            return `<a href="javascript:void(0)" class="instance-group-checker-settings__loaded"` +
                ` title="${this.LABELS.replaceSettings}">${this.LABELS.settingsLoaded}</a>` +
                `<span class="webix_icon wxi-trash instance-group-checker-settings__remove"` +
                ` title="${this.LABELS.removeSettings}"></span>`;
        }

        return `<a href="javascript:void(0)" class="instance-group-checker-settings__upload">` +
            `${this.LABELS.settingsMissing}</a>`;
    }

    _bindCheckerSettingsEvents() {
        const status = $$(this.NAMES.checkerSettingsStatus);
        const node = status?.getNode();
        if (!node) {
            return;
        }

        // Корневой узел шаблона переживает refresh(), поэтому обработчик не теряется.
        node.onclick = (event) => {
            if (event.target.closest(".instance-group-checker-settings__upload") ||
                event.target.closest(".instance-group-checker-settings__loaded")) {
                this._pickCheckerSettings();
                return;
            }

            if (event.target.closest(".instance-group-checker-settings__remove")) {
                this._removeCheckerSettings();
            }
        };
    }

    _applyCheckerSettingsState(hasFile) {
        this._hasCheckerSettings = !!hasFile;
        $$(this.NAMES.checkerSettingsStatus)?.refresh();
        this._bindCheckerSettingsEvents();
    }

    _nextCheckerSettingsRequest() {
        this._checkerSettingsRequest = (this._checkerSettingsRequest || 0) + 1;
        return this._checkerSettingsRequest;
    }

    async _refreshCheckerSettingsState(request) {
        const elementId = this.elementId;
        try {
            const hasFile = await instanceGroupService.checkerSettingsStatus(elementId);
            if (request !== this._checkerSettingsRequest ||
                this.elementId !== elementId ||
                this._checkerSettingsTouched) {
                return;
            }

            this._applyCheckerSettingsState(hasFile);
        } catch (error) {
            // Список групп уже отдал статус; если он устарел, карточка просто покажет его.
            console.warn("Не удалось получить состояние шаблона настроек", error);
        }
    }

    _pickCheckerSettings() {
        const input = document.createElement("input");
        input.type = "file";
        input.accept = ".json,application/json";
        input.style.display = "none";
        document.body.appendChild(input);
        input.addEventListener("cancel", () => input.remove(), { once: true });
        input.onchange = async () => {
            const file = input.files && input.files[0];
            if (!file) {
                input.remove();
                return;
            }

            try {
                await instanceGroupService.uploadCheckerSettings(this.elementId, file);
                this._checkerSettingsTouched = true;
                this._applyCheckerSettingsState(true);
            } catch (error) {
                webix.message({
                    text: error.message || this.LABELS.uploadError,
                    type: "error"
                });
            } finally {
                input.remove();
            }
        };
        input.click();
    }

    async _removeCheckerSettings() {
        try {
            await webix.confirm({
                title: this.LABELS.confirmRemoveTitle,
                text: this.LABELS.confirmRemoveText,
                ok: this.LABELS.confirmYes,
                cancel: this.LABELS.confirmNo
            });
        } catch {
            return;
        }

        try {
            await instanceGroupService.deleteCheckerSettings(this.elementId);
            this._checkerSettingsTouched = true;
            this._applyCheckerSettingsState(false);
        } catch (error) {
            webix.message({
                text: error.message || this.LABELS.removeError,
                type: "error"
            });
        }
    }

    _setChannelFieldsEnabled(enabled) {
        const view = $$(this.NAMES.channelFields);
        if (!view) {
            return;
        }

        if (enabled) {
            view.enable();
        } else {
            view.disable();
        }
    }

    _readChannel() {
        return {
            isEnabled: !!$$(this.NAMES.channelEnabled).getValue(),
            provider: $$(this.NAMES.provider).getValue() || "telegram",
            chatId: parseInt($$(this.NAMES.chatId).getValue()) || 0,
            botToken: $$(this.NAMES.botToken).getValue() || ""
        };
    }

    async _testChannel() {
        const channel = this._readChannel();

        if (!channel.isEnabled) {
            webix.message({ text: this.LABELS.botNotConnected, type: "error" });
            return;
        }

        if (channel.chatId == 0) {
            webix.message({ text: this.LABELS.chatIdZero, type: "error" });
            return;
        }

        try {
            await instanceGroupService.testChannel(channel);
            webix.message({ text: this.LABELS.testSuccess, type: "success" });
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
        }
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

                        $$(this.NAMES.windowId).close();
                    },
                    hotkey: "esc"
                }
            ]
        };
    }

    async _send(onSuccess) {
        const win = $$(this.NAMES.windowId);
        if (!this._validate()) {
            return;
        }

        webix.extend(win, webix.ProgressBar);
        win.showProgress({ type: "icon" });
        win.disable();

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
            },
            organizationIds: this._readOrganizationIds(),
            alertChannel: this._readChannel()
        };

        try {
            await instanceGroupService.create(data);

            if (onSuccess) {
                onSuccess(data);
            }

            $$(this.NAMES.windowId).close();
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            win.enable();
            win.hideProgress();
        }
    }

    _validate() {
        const name = $$(this.NAMES.name).getValue();
        if (!name || name === "") {
            webix.message({ text: this.LABELS.invalidNameMessage, type: "error" });
            return false;
        }

        const channel = this._readChannel();
        if (channel.isEnabled && channel.chatId == 0) {
            webix.message({ text: this.LABELS.chatIdZero, type: "error" });
            return false;
        }

        return true;
    }

    _organizationsTable(rows) {
        return {
            view: "datatable",
            id: this.NAMES.organizationsTable,
            select: "row",
            height: 280,
            checkboxRefresh: true,
            columns: [
                {
                    id: "selected",
                    header: { content: "masterCheckbox", css: "webix_ss_center" },
                    template: "{common.checkbox()}",
                    checkValue: true,
                    uncheckValue: false,
                    width: 50,
                    css: { "text-align": "center" }
                },
                { id: "name", header: this.LABELS.organizationName, fillspace: true, sort: "string" },
                { id: "inn", header: this.LABELS.organizationInn, width: 160, sort: "string" }
            ],
            data: rows
        };
    }

    _readOrganizationIds() {
        const table = $$(this.NAMES.organizationsTable);
        if (!table) {
            return [];
        }

        return table.serialize()
            .filter((row) => row.selected)
            .map((row) => row.id);
    }

    async _loadOrganizationRows(selectedIds) {
        const selected = new Set((selectedIds || []).map((id) => String(id)));

        try {
            const page = await organizationService.list(1, 500);
            return (page.content || []).map((item) => ({
                id: item.id,
                name: item.name || "",
                inn: item.inn || "",
                selected: selected.has(String(item.id))
            }));
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            return [];
        }
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
