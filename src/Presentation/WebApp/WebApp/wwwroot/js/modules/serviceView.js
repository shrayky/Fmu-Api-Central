// js/modules/serviceView.js

import { loadConfiguration, exportPortableSettings, importPortableSettings } from '../services/ConfigurationService.js';
import databaseDumpService from '../services/databaseDumpService.js';

class ServiceView {
    constructor(id) {
        this.id = id;
        this.formId = "serviceViewForm";
        this.dataButtonsId = "serviceDataButtons";
        this.labels = {
            title: "Fmu-Api-Central: Сервис",
            data: "Данные",
            settings: "Настройки",
            export: "Экспорт",
            import: "Импорт",
            dataHint: "Узлы fmu-api и статистика проверок. Пользователи и файлы обновлений не входят в архив.",
            settingsHint: "Логи, оповещения и обновления ПО. Настройки базы данных и сервера не меняются.",
            dataImportConfirm: "Импорт обновит документы узлов и статистики с совпадающими id и добавит новые. Пользователи и файлы обновлений не импортируются. Продолжить?",
            settingsImportConfirm: "Импорт заменит настройки логов, оповещений и обновлений ПО. Параметры базы данных и сервера останутся без изменений. Продолжить?",
            exportDone: "Экспорт завершён",
            importDone: "Импорт завершён",
        };
    }

    async loadData() {
        const requestResult = await loadConfiguration();

        if (!requestResult.result) {
            webix.message({ type: "error", text: requestResult.error });
            this.dbEnabled = false;
            return this;
        }

        this.dbEnabled = !!requestResult.value?.Content?.databaseConnection?.enable;
        return this;
    }

    renderView() {
        $$("toolbarLabel").setValue(this.labels.title);

        return {
            id: this.id,
            rows: [
                {
                    view: "form",
                    id: this.formId,
                    elements: [
                        this._dataFieldset(),
                        this._settingsFieldset(),
                        {}
                    ]
                }
            ]
        };
    }

    /**
     * Рамка экспорта и импорта данных CouchDB.
     */
    _dataFieldset() {
        return {
            view: "fieldset",
            label: this.labels.data,
            body: {
                id: this.dataButtonsId,
                disabled: !this.dbEnabled,
                rows: [
                    {
                        view: "label",
                        label: this.labels.dataHint
                    },
                    this._actionButtons(() => this._exportData(), () => this._importData())
                ]
            }
        };
    }

    /**
     * Рамка экспорта и импорта переносимых настроек приложения.
     */
    _settingsFieldset() {
        return {
            view: "fieldset",
            label: this.labels.settings,
            body: {
                rows: [
                    {
                        view: "label",
                        label: this.labels.settingsHint
                    },
                    this._actionButtons(() => this._exportSettings(), () => this._importSettings())
                ]
            }
        };
    }

    /**
     * Кнопки экспорта и импорта для рамки.
     */
    _actionButtons(onExport, onImport) {
        return {
            cols: [
                {
                    view: "button",
                    value: this.labels.export,
                    width: 120,
                    click: onExport
                },
                {
                    view: "button",
                    value: this.labels.import,
                    width: 120,
                    click: onImport
                },
                {}
            ]
        };
    }

    /**
     * Выгружает zip с узлами и статистикой проверок.
     */
    async _exportData() {
        const form = $$(this.formId);
        this._showFormProgress(form);

        try {
            const result = await databaseDumpService.export();
            if (!result.result) {
                webix.message({ type: "error", text: result.error });
                return;
            }

            this._downloadBlob(result.value.blob, result.value.fileName);
            webix.message({ type: "success", text: this.labels.exportDone });
        }
        catch (error) {
            webix.message({ type: "error", text: error.message });
        }
        finally {
            this._hideFormProgress(form);
        }
    }

    /**
     * Импортирует zip с узлами и статистикой проверок.
     */
    async _importData() {
        const confirmed = await this._confirm(this.labels.import, this.labels.dataImportConfirm);
        if (!confirmed)
            return;

        const file = await this._pickFile(".zip,application/zip,application/x-zip-compressed");
        if (!file)
            return;

        const form = $$(this.formId);
        this._showFormProgress(form);

        try {
            const result = await databaseDumpService.import(file);
            if (!result.result) {
                webix.message({ type: "error", text: result.error });
                return;
            }

            const summary = result.value;
            webix.message({
                type: "success",
                text: `${this.labels.importDone}: баз ${summary.databases}, пакетов ${summary.packages}, документов ${summary.documents}`
            });
        }
        catch (error) {
            webix.message({ type: "error", text: error.message });
        }
        finally {
            this._hideFormProgress(form);
        }
    }

    /**
     * Выгружает JSON переносимых настроек.
     */
    async _exportSettings() {
        const form = $$(this.formId);
        this._showFormProgress(form);

        try {
            const result = await exportPortableSettings();
            if (!result.result) {
                webix.message({ type: "error", text: result.error });
                return;
            }

            this._downloadBlob(result.value.blob, result.value.fileName);
            webix.message({ type: "success", text: this.labels.exportDone });
        }
        catch (error) {
            webix.message({ type: "error", text: error.message });
        }
        finally {
            this._hideFormProgress(form);
        }
    }

    /**
     * Импортирует JSON переносимых настроек.
     */
    async _importSettings() {
        const confirmed = await this._confirm(this.labels.import, this.labels.settingsImportConfirm);
        if (!confirmed)
            return;

        const file = await this._pickFile(".json,application/json");
        if (!file)
            return;

        const form = $$(this.formId);
        this._showFormProgress(form);

        try {
            const result = await importPortableSettings(file);
            if (!result.result) {
                webix.message({ type: "error", text: result.error });
                return;
            }

            webix.message({
                type: "success",
                text: `${this.labels.importDone}. Если изменились логи, перезапустите службу.`
            });
        }
        catch (error) {
            webix.message({ type: "error", text: error.message });
        }
        finally {
            this._hideFormProgress(form);
        }
    }

    _confirm(title, text) {
        return new Promise((resolve) => {
            webix.confirm({
                title,
                text,
                ok: "Продолжить",
                cancel: "Отмена",
                callback: resolve
            });
        });
    }

    _pickFile(accept) {
        return new Promise((resolve) => {
            const input = document.createElement("input");
            input.type = "file";
            input.accept = accept;
            input.onchange = () => resolve(input.files && input.files[0] ? input.files[0] : null);
            input.click();
        });
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

    _showFormProgress(form) {
        if (!form.showProgress)
            webix.extend(form, webix.ProgressBar);

        form.disable();
        form.showProgress({ type: "icon" });
    }

    _hideFormProgress(form) {
        if (form.hideProgress)
            form.hideProgress();

        form.enable();
        if (!this.dbEnabled && $$(this.dataButtonsId))
            $$(this.dataButtonsId).disable();
    }
}

export default async function createServiceView(id) {
    const view = new ServiceView(id);
    await view.loadData();
    return view.renderView();
}
