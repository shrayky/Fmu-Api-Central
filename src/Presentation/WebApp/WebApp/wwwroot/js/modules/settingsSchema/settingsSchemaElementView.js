import { Text, Number as NumberField, CheckBox } from '../../utils/ui.js';
import settingsSchemaService from '../../services/settingsSchemaService.js';

class SettingsSchemaElementView {
    constructor() {
        this.elementId = "";
        this.defaults = null;

        this.LABELS = {
            formTitle: "Схема настроек",
            name: "Имя схемы",
            invalidNameMessage: "заполните поле",
            timeouts: "Таймауты",
            cdnRequestTimeout: "Загрузка списка CDN, сек",
            checkMarkRequestTimeout: "Проверка марки в ЧЗ, сек",
            checkInternetConnectionTimeout: "Проверка доступа в интернет, сек",
            syncWithTsPiot: "Синхронизировать таймауты с ТС ПИОТ",
            mappings: "Товарные группы",
            atolCode: "Код Атол",
            trueApiGroupId: "Код ЧЗ",
            mappingName: "Название",
            checkSmp: "ЕМЦ",
            addMapping: "Добавить",
            removeMapping: "Удалить",
            fillDefaults: "Заполнить по умолчанию",
            hosts: "Хосты пинга",
            host: "Хост",
            addHost: "Добавить",
            removeHost: "Удалить",
            createButton: "Сохранить",
            cancelButton: "Отмена"
        };

        this.NAMES = {
            windowId: "settingsSchemaWindow",
            formId: "settingsSchemaElement",
            name: "settingsSchemaName",
            cdnRequestTimeout: "settingsSchemaCdnTimeout",
            checkMarkRequestTimeout: "settingsSchemaCheckMarkTimeout",
            checkInternetConnectionTimeout: "settingsSchemaInternetTimeout",
            syncWithTsPiot: "settingsSchemaSyncWithTsPiot",
            mappingTable: "settingsSchemaMappingTable",
            hostsTable: "settingsSchemaHostsTable"
        };
    }

    async showDialog(editedData = {}, onSuccess, onClose) {
        this.elementId = editedData.id || crypto.randomUUID();
        this.defaults = await this._loadDefaults();

        const timeouts = editedData.httpRequestTimeouts || this.defaults.httpRequestTimeouts || {};
        const mappings = (editedData.gisMtProductMappings && editedData.gisMtProductMappings.length > 0)
            ? editedData.gisMtProductMappings
            : (this.defaults.gisMtProductMappings || []);
        const hosts = editedData.hostsToPing || [];

        if ($$(this.NAMES.windowId)) {
            $$(this.NAMES.windowId).destructor();
        }

        webix.ui({
            view: "window",
            id: this.NAMES.windowId,
            modal: true,
            width: 820,
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
                    {
                        view: "tabview",
                        height: 380,
                        cells: [
                            {
                                header: this.LABELS.timeouts,
                                body: {
                                    rows: [
                                        NumberField(this.LABELS.cdnRequestTimeout, this.NAMES.cdnRequestTimeout, timeouts.cdnRequestTimeout ?? 15, "11"),
                                        NumberField(this.LABELS.checkMarkRequestTimeout, this.NAMES.checkMarkRequestTimeout, timeouts.checkMarkRequestTimeout ?? 2, "11"),
                                        NumberField(this.LABELS.checkInternetConnectionTimeout, this.NAMES.checkInternetConnectionTimeout, timeouts.checkInternetConnectionTimeout ?? 15, "1111"),
                                        CheckBox(this.LABELS.syncWithTsPiot, this.NAMES.syncWithTsPiot, {
                                            value: timeouts.syncWithTsPiot !== false
                                        }),
                                        {}
                                    ]
                                }
                            },
                            {
                                header: this.LABELS.mappings,
                                body: {
                                    rows: [
                                        this._mappingToolbar(),
                                        this._mappingTable(mappings)
                                    ]
                                }
                            },
                            {
                                header: this.LABELS.hosts,
                                body: {
                                    rows: [
                                        this._hostsToolbar(),
                                        this._hostsTable(hosts)
                                    ]
                                }
                            }
                        ]
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

    _mappingToolbar() {
        return {
            view: "toolbar",
            borderless: true,
            elements: [
                {
                    view: "button",
                    value: this.LABELS.addMapping,
                    width: 120,
                    click: () => this._addMapping()
                },
                {
                    view: "button",
                    value: this.LABELS.removeMapping,
                    width: 120,
                    click: () => this._removeMapping()
                },
                {
                    view: "button",
                    value: this.LABELS.fillDefaults,
                    width: 200,
                    click: () => this._fillDefaults()
                },
                {}
            ]
        };
    }

    _mappingTable(mappings) {
        return {
            view: "datatable",
            id: this.NAMES.mappingTable,
            editable: true,
            select: "row",
            height: 280,
            columns: [
                { id: "atolCode", header: this.LABELS.atolCode, width: 110, editor: "text", sort: "int" },
                { id: "trueApiGroupId", header: this.LABELS.trueApiGroupId, width: 110, editor: "text", sort: "int" },
                { id: "name", header: this.LABELS.mappingName, fillspace: true, editor: "text" },
                {
                    id: "checkSmp",
                    header: this.LABELS.checkSmp,
                    width: 70,
                    template: "{common.checkbox()}",
                    checkValue: true,
                    uncheckValue: false
                }
            ],
            checkboxRefresh: true,
            data: mappings.map((row) => this._toRow(row)),
            on: {
                onAfterEditStop: (state, editor) => this._onMappingEdited(state, editor)
            }
        };
    }

    _toRow(row) {
        const atolCode = this._toInt(row.atolCode);
        return {
            id: atolCode || webix.uid(),
            atolCode,
            trueApiGroupId: this._toInt(row.trueApiGroupId),
            name: row.name || "",
            checkSmp: !!row.checkSmp
        };
    }

    _toInt(value) {
        const parsed = parseInt(value, 10);
        return Number.isNaN(parsed) ? 0 : parsed;
    }

    _addMapping() {
        const table = $$(this.NAMES.mappingTable);
        const id = webix.uid();
        table.add({
            id,
            atolCode: 0,
            trueApiGroupId: 0,
            name: "",
            checkSmp: false
        });
        table.select(id);
        table.showItem(id);
    }

    _removeMapping() {
        const table = $$(this.NAMES.mappingTable);
        const selected = table.getSelectedId();
        if (selected) {
            table.remove(selected);
        }
    }

    _hostsToolbar() {
        return {
            view: "toolbar",
            borderless: true,
            elements: [
                {
                    view: "button",
                    value: this.LABELS.addHost,
                    width: 120,
                    click: () => this._addHost()
                },
                {
                    view: "button",
                    value: this.LABELS.removeHost,
                    width: 120,
                    click: () => this._removeHost()
                },
                {}
            ]
        };
    }

    _hostsTable(hosts) {
        return {
            view: "datatable",
            id: this.NAMES.hostsTable,
            editable: true,
            select: "row",
            height: 280,
            columns: [
                { id: "value", header: this.LABELS.host, fillspace: true, editor: "text" }
            ],
            data: hosts.map((row, index) => this._toHostRow(row, index)),
        };
    }

    _toHostRow(row, index) {
        const value = row.value || row.Value || "";
        const id = row.id || row.Id || index + 1;
        return { id, value };
    }

    _addHost() {
        const table = $$(this.NAMES.hostsTable);
        const lastId = table.getLastId();
        const id = lastId ? this._toInt(lastId) + 1 : 1;
        table.add({ id, value: "" });
        table.select(id);
        table.showItem(id);
        table.editCell(id, "value");
    }

    _removeHost() {
        const table = $$(this.NAMES.hostsTable);
        const selected = table.getSelectedId();
        if (selected) {
            table.remove(selected);
        }
    }

    _fillDefaults() {
        const mappings = this.defaults?.gisMtProductMappings || [];
        const table = $$(this.NAMES.mappingTable);
        table.clearAll();
        table.parse(mappings.map((row) => this._toRow(row)));
    }

    _onMappingEdited(state, editor) {
        if (editor.column !== "atolCode" || state.value === state.old) {
            return;
        }

        const table = $$(this.NAMES.mappingTable);
        const item = table.getItem(editor.row);
        const atolCode = this._toInt(state.value);
        const preset = (this.defaults?.gisMtProductMappings || [])
            .find((row) => this._toInt(row.atolCode) === atolCode);

        if (!item || !preset) {
            return;
        }

        table.updateItem(editor.row, {
            ...item,
            atolCode,
            trueApiGroupId: preset.trueApiGroupId,
            name: preset.name,
            checkSmp: !!preset.checkSmp
        });
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

    async _loadDefaults() {
        try {
            return await settingsSchemaService.defaults();
        } catch {
            return {
                httpRequestTimeouts: {
                    cdnRequestTimeout: 15,
                    checkMarkRequestTimeout: 2,
                    checkInternetConnectionTimeout: 15,
                    syncWithTsPiot: true
                },
                gisMtProductMappings: [],
                hostsToPing: []
            };
        }
    }

    _collectHosts() {
        const table = $$(this.NAMES.hostsTable);
        const rows = [];
        let index = 1;

        table.data.each((item) => {
            const value = (item.value || "").trim();
            if (!value) {
                return;
            }

            rows.push({
                Id: index,
                Value: value
            });
            index += 1;
        });

        return rows;
    }

    _collectMappings() {
        const table = $$(this.NAMES.mappingTable);
        const rows = [];

        table.data.each((item) => {
            const atolCode = this._toInt(item.atolCode);
            if (!atolCode) {
                return;
            }

            rows.push({
                atolCode,
                trueApiGroupId: this._toInt(item.trueApiGroupId),
                name: item.name || "",
                checkSmp: !!item.checkSmp
            });
        });

        return rows;
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
            httpRequestTimeouts: {
                cdnRequestTimeout: this._toInt($$(this.NAMES.cdnRequestTimeout).getValue()) || 15,
                checkMarkRequestTimeout: this._toInt($$(this.NAMES.checkMarkRequestTimeout).getValue()) || 2,
                checkInternetConnectionTimeout: this._toInt($$(this.NAMES.checkInternetConnectionTimeout).getValue()) || 15,
                syncWithTsPiot: !!$$(this.NAMES.syncWithTsPiot).getValue()
            },
            gisMtProductMappings: this._collectMappings(),
            hostsToPing: this._collectHosts()
        };

        try {
            await settingsSchemaService.create(data);

            if (onSuccess) {
                onSuccess(data);
            }

            $$(this.NAMES.windowId).close();
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

export default new SettingsSchemaElementView();
