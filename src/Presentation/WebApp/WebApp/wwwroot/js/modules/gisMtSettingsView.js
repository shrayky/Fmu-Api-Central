import { loadConfiguration, saveConfigurationSection } from '../services/ConfigurationService.js';
import { Number, CheckBox, Text, padding } from '../utils/ui.js';
import { httpAddressValidation } from '../utils/validators.js';

const TIME_FORMAT_24H = "%H:%i";

class GisMtSettingsView {
    constructor(id) {
        this.id = id;
        this.formId = "gisMtSettingsForm";
        this.settingsId = "gisMtSettingsFields";
        this.labels = {
            title: "Fmu-Api-Central: Настройки обмена с ГИС МТ",
            enable: "Использовать",
            serviceUrl: "Адрес сервиса обмена с ГИС МТ",
            pollInterval: "Интервал опроса документов (минуты)",
            markRetentionDays: "Срок хранения невалидных марок (дни)",
            documentsSyncDays: "Период загрузки документов (дней, включая текущий)",
            stockLoadEnabled: "Загружать остатки марок ежедневно",
            stockLoadTime: "Время загрузки остатков",
        };
    }

    /// Загружает настройки ГИС МТ из конфигурации.
    async loadData() {
        const requestResult = await loadConfiguration();

        if (!requestResult.result) {
            console.error(requestResult);
            webix.message({ type: "error", text: requestResult.error });
            return this;
        }

        const settings = requestResult.value.Content?.gisMtSettings ?? {};

        this.enable = settings.enable ?? false;
        this.serviceUrl = settings.serviceUrl || "http://localhost:2577";
        this.mtDocumentsPollIntervalMinutes = settings.mtDocumentsPollIntervalMinutes ?? 10;
        this.documentsSyncDays = settings.documentsSyncDays ?? 1;
        this.markRetentionDays = settings.markRetentionDays ?? 365;
        this.stockLoadEnabled = settings.stockLoadEnabled ?? false;
        this.stockLoadTime = this._parseTimeString(settings.stockLoadTime ?? "03:00:00");

        return this;
    }

    /// Преобразует строку времени в Date для datepicker.
    _parseTimeString(timeStr) {
        if (!timeStr)
            return new Date(2000, 0, 1, 3, 0, 0);

        const parts = String(timeStr).split(":");
        const hours = parseInt(parts[0], 10) || 0;
        const minutes = parseInt(parts[1], 10) || 0;
        const seconds = parseInt(parts[2], 10) || 0;

        return new Date(2000, 0, 1, hours, minutes, seconds);
    }

    /// Форматирует значение datepicker в строку TimeOnly.
    _formatTimeForSave(value) {
        if (!value)
            return "03:00:00";

        const date = value instanceof Date ? value : new Date(value);
        const hours = date.getHours().toString().padStart(2, "0");
        const minutes = date.getMinutes().toString().padStart(2, "0");
        const seconds = date.getSeconds().toString().padStart(2, "0");

        return `${hours}:${minutes}:${seconds}`;
    }

    /// Создаёт поле выбора времени в 24-часовом формате.
    _createTimePicker(id, label, defaultValue) {
        return {
            view: "datepicker",
            type: "time",
            format: TIME_FORMAT_24H,
            editable: true,
            suggest: {
                type: "calendar",
                padding: 0,
                body: {
                    type: "time",
                    calendarTime: TIME_FORMAT_24H,
                    width: 250,
                    height: 240,
                }
            },
            label: label,
            labelPosition: "top",
            id: id,
            name: id,
            value: defaultValue,
        };
    }

    /// Включает или выключает поля настроек обмена.
    _setFieldsEnabled(enabled) {
        const fields = $$(this.settingsId);
        if (!fields)
            return;

        if (enabled)
            fields.enable();
        else
            fields.disable();
    }

    renderView() {
        $$("toolbarLabel").setValue(this.labels.title);

        const enableCheckBox = CheckBox(this.labels.enable, "enable", {
            value: this.enable,
            on: {
                onChange: (enabled) => this._setFieldsEnabled(!!enabled)
            }
        });

        const settingsFields = {
            id: this.settingsId,
            disabled: !this.enable,
            rows: [
                Text(this.labels.serviceUrl, "serviceUrl", this.serviceUrl, httpAddressValidation),
                Number(
                    this.labels.pollInterval,
                    "mtDocumentsPollIntervalMinutes",
                    this.mtDocumentsPollIntervalMinutes,
                    "111"
                ),
                Number(
                    this.labels.documentsSyncDays,
                    "documentsSyncDays",
                    this.documentsSyncDays,
                    "111"
                ),
                Number(
                    this.labels.markRetentionDays,
                    "markRetentionDays",
                    this.markRetentionDays,
                    "111"
                ),
                CheckBox(this.labels.stockLoadEnabled, "stockLoadEnabled", {
                    value: this.stockLoadEnabled
                }),
                this._createTimePicker(
                    "stockLoadTime",
                    this.labels.stockLoadTime,
                    this.stockLoadTime
                ),
            ]
        };

        return {
            id: this.id,
            rows: [
                {
                    view: "form",
                    id: this.formId,
                    padding: padding,
                    elements: [
                        enableCheckBox,
                        settingsFields,
                        {
                            cols: [
                                {
                                    view: "button",
                                    value: "Сохранить",
                                    css: "webix_primary",
                                    width: 120,
                                    click: () => this._save()
                                },
                                {}
                            ]
                        },
                        {}
                    ]
                }
            ]
        };
    }

    /// Сохраняет секцию gisMtSettings в конфигурацию.
    async _save() {
        const form = $$(this.formId);

        if (!form.validate())
            return;

        const values = form.getValues();

        const saveResult = await saveConfigurationSection("gisMtSettings", _prev => ({
            enable: !!values.enable,
            serviceUrl: values.serviceUrl,
            mtDocumentsPollIntervalMinutes: parseInt(values.mtDocumentsPollIntervalMinutes, 10) || 10,
            documentsSyncDays: parseInt(values.documentsSyncDays, 10) || 1,
            markRetentionDays: parseInt(values.markRetentionDays, 10) || 365,
            stockLoadEnabled: !!values.stockLoadEnabled,
            stockLoadTime: this._formatTimeForSave(values.stockLoadTime),
        }));

        if (!saveResult.result) {
            webix.message({ type: "error", text: saveResult.error });
            return;
        }

        webix.message({
            type: "success",
            text: "Настройки сохранены"
        });
    }
}

export default async function createGisMtSettingsView(id) {
    const view = new GisMtSettingsView(id);
    await view.loadData();
    return view.renderView();
}
