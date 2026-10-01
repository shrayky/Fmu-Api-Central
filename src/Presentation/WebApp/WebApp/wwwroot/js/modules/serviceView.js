// js/modules/serviceView.js

import { loadConfiguration, exportPortableSettings, importPortableSettings } from '../services/ConfigurationService.js';
import databaseDumpService from '../services/databaseDumpService.js';
import cryptoProLicenseService from '../services/cryptoProLicenseService.js';
import organizationService from '../services/organizationService.js';

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
            dataHint: "Узлы, группы, схемы настроек, организации и шаблоны оповещений. Пользователи, файлы обновлений, статистика и данные ГИС МТ не входят в архив.",
            settingsHint: "Логи, оповещения и обновления ПО. Настройки базы данных и сервера не меняются.",
            dataImportConfirm: "Импорт обновит узлы, группы, схемы, организации и шаблоны оповещений с совпадающими id и добавит новые. Пользователи, файлы обновлений, статистика и данные ГИС МТ не импортируются. Продолжить?",
            settingsImportConfirm: "Импорт заменит настройки логов, оповещений и обновлений ПО. Параметры базы данных и сервера останутся без изменений. Продолжить?",
            exportDone: "Экспорт завершён",
            importDone: "Импорт завершён",
            cryptoPro: "КриптоПро",
            cryptoProHint: "Лицензия одна на машину, где запущена служба. Сертификат — zip-контейнер с файлами *.key, например 2560.zip.",
            cryptoProTool: "csptest ищется в Program Files\\Crypto Pro\\CSP, в Program Files (x86)\\Crypto Pro\\CSP и в /opt/cprocsp/bin/amd64/csptest. Другие каталоги не проверяются.",
            setLicense: "Установить лицензию",
            uploadCertificate: "Загрузить сертификат",
            licenseSet: "Лицензия установлена",
            certificateUploaded: "Сертификат загружен",
            serial: "Серийный номер",
        };
    }

    async loadData() {
        const requestResult = await loadConfiguration();

        if (!requestResult.result) {
            webix.message({ type: "error", text: requestResult.error });
            this.dbEnabled = false;
            this.licenseText = await this._readLicense();
            return this;
        }

        this.dbEnabled = !!requestResult.value?.Content?.databaseConnection?.enable;
        this.licenseText = await this._readLicense();
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
                        this._cryptoProFieldset(),
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
     * Лицензия и контейнер ставятся в КриптоПро пользователя службы, не в настройки организации.
     */
    _cryptoProFieldset() {
        return {
            view: "fieldset",
            label: this.labels.cryptoPro,
            body: {
                rows: [
                    {
                        view: "label",
                        label: this.labels.cryptoProHint
                    },
                    {
                        view: "label",
                        label: this.labels.cryptoProTool
                    },
                    {
                        view: "template",
                        id: "cryptoProLicenseStatus",
                        autoheight: true,
                        borderless: true,
                        template: this._escape(this.licenseText),
                        css: { "white-space": "pre-wrap" }
                    },
                    {
                        view: "text",
                        id: "cryptoProLicenseSerial",
                        label: this.labels.serial,
                        labelPosition: "top",
                        placeholder: "XXXXX-XXXXX-XXXXX-XXXXX-XXXXX"
                    },
                    {
                        cols: [
                            {
                                view: "button",
                                value: this.labels.setLicense,
                                width: 220,
                                click: () => this._setLicense()
                            },
                            {
                                view: "button",
                                value: this.labels.uploadCertificate,
                                width: 220,
                                click: () => this._uploadCertificate()
                            },
                            {}
                        ]
                    }
                ]
            }
        };
    }

    async _readLicense() {
        try {
            return await cryptoProLicenseService.view();
        } catch (error) {
            return error.message || "Не удалось прочитать лицензию";
        }
    }

    _escape(text) {
        return String(text || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");
    }

    async _setLicense() {
        const serial = String($$("cryptoProLicenseSerial").getValue() || "");
        const form = $$(this.formId);
        this._showFormProgress(form);

        try {
            const text = await cryptoProLicenseService.set(serial);
            this._showLicense(text);
            webix.message({ type: "success", text: this.labels.licenseSet });
        } catch (error) {
            webix.message({ type: "error", text: error.message || "Не удалось установить лицензию" });
        } finally {
            this._hideFormProgress(form);
        }
    }

    async _uploadCertificate() {
        const file = await this._pickFile(".zip,application/zip,application/x-zip-compressed");
        if (!file)
            return;

        const form = $$(this.formId);
        this._showFormProgress(form);

        try {
            await organizationService.uploadCertificate(file);
            webix.message({ type: "success", text: this.labels.certificateUploaded });
        } catch (error) {
            webix.message({ type: "error", text: error.message || "Не удалось загрузить сертификат" });
        } finally {
            this._hideFormProgress(form);
        }
    }

    _showLicense(text) {
        this.licenseText = text;
        const status = $$("cryptoProLicenseStatus");
        if (!status)
            return;

        status.define("template", this._escape(text));
        status.refresh();
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
     * Zip справочников без статистики и данных ГИС МТ — на новом сервере они подгрузятся сами.
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
     * Импортирует zip справочников; пакеты статистики и ГИС МТ пропускаются.
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
