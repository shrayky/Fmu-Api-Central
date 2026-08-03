import { loadConfiguration, saveConfigurationSection } from '../../services/ConfigurationService.js';
import { Text, Number, CheckBox, TableToolbar, padding } from '../../utils/ui.js';
import { httpAddressListValidation } from '../../utils/validators.js';

const TIME_FORMAT_24H = "%H:%i";

class SoftwareUpdateSettingsView {
    constructor() {
        this.formId = "softwareUpdateSettingsForm";
        this.SCHEDULER_TABLE_ID = "SchedulerUpdateDownload";
        this.SCHEDULER_FORM_NAME = "SchedulerUpdateDownloadForm";
        this.LABELS = {
            addresses: "Адреса серверов обмена",
            addressTip: "⚠️ можно указать несколько адресов через точку с запятой",
            exchangeInterval: "Интервал обмена (минут)",
            restrictOutsideSchedule: "Не отдавать обновления вне периодов",
            schedulerTitle: "Расписание загрузки обновлений",
            schedulerTip: "если список пуст, обновление доступно в любое время",
            newInterval: "Новый интервал",
            editInterval: "Интервал",
            beginTime: "Начало",
            endTime: "Окончание",
            add: "Сохранить",
            close: "Закрыть",
            save: "Сохранить настройки",
            timeRequired: "Укажите время начала и окончания интервала",
            invalidInterval: "Время начала должно быть меньше времени окончания",
            saveSuccess: "Настройки распространения обновлений сохранены",
            saveError: "Ошибка сохранения настроек",
            loadError: "Ошибка загрузки настроек",
        };

        this.exchangeServerAddresses = "";
        this.exchangeRequestInterval = 60;
        this.restrictUpdatesOutsideSchedule = true;
        this.schedulerUpdateDownload = [];
    }

    /// Загружает настройки из конфигурации.
    async loadData() {
        const requestResult = await loadConfiguration();

        if (!requestResult.result) {
            webix.message({ type: "error", text: requestResult.error || this.LABELS.loadError });
            return this;
        }

        const settings = requestResult.value.Content?.softwareUpdateSettings || {};

        this.exchangeServerAddresses = settings.exchangeServerAddresses || "";
        this.exchangeRequestInterval = settings.exchangeRequestInterval > 0
            ? settings.exchangeRequestInterval
            : 60;
        this.restrictUpdatesOutsideSchedule = settings.restrictUpdatesOutsideSchedule !== false;
        this.schedulerUpdateDownload = Array.isArray(settings.schedulerUpdateDownload)
            ? settings.schedulerUpdateDownload
            : [];
                
        return this;
    }

    /// Преобразует строку времени в объект Date для datepicker.
    _parseTimeString(timeStr) {
        if (!timeStr)
            return new Date(2000, 0, 1, 0, 0, 0);

        const parts = String(timeStr).split(":");
        const hours = parseInt(parts[0], 10) || 0;
        const minutes = parseInt(parts[1], 10) || 0;
        const seconds = parseInt(parts[2], 10) || 0;

        return new Date(2000, 0, 1, hours, minutes, seconds);
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

    /// Форматирует время для сохранения в формате TimeOnly.
    _formatTimeForSave(value) {
        if (!value)
            return "00:00:00";

        const date = value instanceof Date ? value : new Date(value);
        const hours = date.getHours().toString().padStart(2, "0");
        const minutes = date.getMinutes().toString().padStart(2, "0");
        const seconds = date.getSeconds().toString().padStart(2, "0");

        return `${hours}:${minutes}:${seconds}`;
    }

    /// Заголовок (стандартный label Webix) и подсказка курсивом в скобках в одной строке.
    _labelWithTip(id, title, tip) {
        const safeTip = webix.template.escape(tip);

        return {
            cols: [
                {
                    view: "label",
                    id: id,
                    label: title,
                    width: 0,
                    autowidth: true,
                },
                {
                    view: "template",
                    id: `${id}Tip`,
                    borderless: true,
                    css: "webix_el_label",
                    template: `<div style="display:flex;align-items:center;height:100%"><span style="font-style:italic;font-size:smaller">(${safeTip})</span></div>`
                }
            ]
        };
    }

    render() {
        return {
            view: "form",
            id: this.formId,
            padding: padding,
            elements: [
                this._labelWithTip("lExchangeAddresses", this.LABELS.addresses, this.LABELS.addressTip),
                Text("",
                    "exchangeServerAddresses",
                    this.exchangeServerAddresses,
                    {
                        ...httpAddressListValidation,
                        label: false,
                    }),

                Number(this.LABELS.exchangeInterval,
                    "exchangeRequestInterval",
                    this.exchangeRequestInterval),

                CheckBox(this.LABELS.restrictOutsideSchedule, "restrictUpdatesOutsideSchedule", {
                    value: this.restrictUpdatesOutsideSchedule,
                }),

                this._labelWithTip("lSchedulerUpdateDownload", this.LABELS.schedulerTitle, this.LABELS.schedulerTip),

                TableToolbar(this.SCHEDULER_TABLE_ID),

                this._createSchedulerTable(),
                {
                    cols: [
                        {
                            view: "button",
                            value: this.LABELS.save,
                            css: "webix_primary",
                            width: 220,
                            click: () => this._save(),
                        },
                        {}
                    ]
                },
                {}
            ]
        };
    }

    _createSchedulerTable() {
        return {
            view: "formtable",
            id: this.SCHEDULER_TABLE_ID,
            name: "schedulerUpdateDownload",
            data: this.schedulerUpdateDownload,
            resizeColumn: true,
            resizeRow: true,
            select: true,
            minHeight: 200,
            columns: [
                { id: "id", header: "Код", hidden: true },
                { id: "beginTime", header: this.LABELS.beginTime, fillspace: true },
                { id: "endTime", header: this.LABELS.endTime, fillspace: true },
            ],
            on: {
                onAfterSelect: () => {
                    $$(`delete_${this.SCHEDULER_TABLE_ID}`).enable();
                },
                onAfterDelete: () => {
                    $$(`delete_${this.SCHEDULER_TABLE_ID}`).disable();
                    if ($$(this.SCHEDULER_TABLE_ID).count() == 0) {
                        $$(`deleteAll_${this.SCHEDULER_TABLE_ID}`).disable();
                    }
                },
                onBeforeAdd: (id, obj) => {
                    if (obj.beginTime == undefined) {
                        this._showSchedulerForm(this.LABELS.newInterval);
                        return false;
                    }
                },
                onItemDblClick: (id) => {
                    this._showSchedulerForm(this.LABELS.editInterval, id);
                }
            }
        };
    }

    _showSchedulerForm(label, id) {
        const windowInnerWidth = window.innerWidth;

        webix.ui({
            view: "window",
            id: this.SCHEDULER_FORM_NAME,
            position: "center",
            modal: true,
            move: false,
            resize: false,
            width: windowInnerWidth * 0.5,
            head: this._createSchedulerFormHeader(label),
            body: this._createSchedulerFormBody(id)
        }).show();

        this._initSchedulerFormValues(id);
        $$("schedulerBeginTime").focus();
    }

    _createSchedulerFormHeader(label) {
        return {
            view: "toolbar",
            elements: [
                {
                    view: "label",
                    label: label,
                },
                {
                    view: "icon",
                    icon: "wxi-close",
                    click: () => $$(this.SCHEDULER_FORM_NAME).close()
                }
            ]
        };
    }

    _createSchedulerFormBody(rowId) {
        return {
            rows: [
                {
                    cols: [
                        this._createTimePicker(
                            "schedulerBeginTime",
                            this.LABELS.beginTime,
                            new Date(2000, 0, 1, 0, 0, 0)
                        ),
                        this._createTimePicker(
                            "schedulerEndTime",
                            this.LABELS.endTime,
                            new Date(2000, 0, 1, 23, 59, 0)
                        ),
                    ]
                },
                {
                    view: "text",
                    type: "number",
                    id: "schedulerRowId",
                    name: "schedulerRowId",
                    hidden: true,
                    value: rowId ?? ""
                },
                {
                    cols: [
                        {
                            view: "button",
                            value: this.LABELS.add,
                            id: "schedulerAddButton",
                            autowidth: "false",
                            width: 400,
                            click: () => this._handleSchedulerAddButton(rowId)
                        },
                        {
                            view: "button",
                            value: this.LABELS.close,
                            id: "schedulerCloseBtn",
                            autowidth: "false",
                            width: 400,
                            click: () => $$(this.SCHEDULER_FORM_NAME).close()
                        },
                        {}
                    ]
                }
            ]
        };
    }

    _handleSchedulerAddButton(rowId) {
        const beginTimeValue = $$("schedulerBeginTime").getValue();
        const endTimeValue = $$("schedulerEndTime").getValue();

        if (!beginTimeValue || !endTimeValue) {
            webix.message({
                text: this.LABELS.timeRequired,
                type: "error"
            });
            return;
        }

        const beginTime = this._formatTimeForSave(beginTimeValue);
        const endTime = this._formatTimeForSave(endTimeValue);

        if (beginTime >= endTime) {
            webix.message({
                text: this.LABELS.invalidInterval,
                type: "error"
            });
            return;
        }

        const table = $$(this.SCHEDULER_TABLE_ID);
        if (!table)
            return;

        if (rowId == undefined) {
            const lastId = table.getLastId();
            const newId = lastId == undefined ? 1 : lastId + 1;
            table.add({ id: newId, beginTime, endTime });
        }
        else {
            table.updateItem(rowId, { id: rowId, beginTime, endTime });
        }

        if (table.count() > 0)
            $$(`deleteAll_${this.SCHEDULER_TABLE_ID}`).enable();

        $$(this.SCHEDULER_FORM_NAME).close();
    }

    _initSchedulerFormValues(rowId) {
        if (rowId == undefined)
            return;

        const table = $$(this.SCHEDULER_TABLE_ID);
        const item = table.getItem(rowId);

        $$("schedulerBeginTime").setValue(this._parseTimeString(item.beginTime));
        $$("schedulerEndTime").setValue(this._parseTimeString(item.endTime));
        $$("schedulerRowId").setValue(item.id);
    }

    /// Сохраняет настройки распространения обновлений.
    async _save() {
        const form = $$(this.formId);

        if (!form.validate())
            return;

        const values = form.getValues();
        const table = $$(this.SCHEDULER_TABLE_ID);
        const schedulerRows = table ? table.serialize() : [];

        const saveResult = await saveConfigurationSection("softwareUpdateSettings", () => ({
            exchangeServerAddresses: values.exchangeServerAddresses || "",
            exchangeRequestInterval: parseInt(values.exchangeRequestInterval, 10) || 60,
            restrictUpdatesOutsideSchedule: !!values.restrictUpdatesOutsideSchedule,
            schedulerUpdateDownload: schedulerRows.map((row) => ({
                id: row.id,
                beginTime: row.beginTime,
                endTime: row.endTime,
            })),
        }));

        if (!saveResult.result) {
            webix.message({ type: "error", text: saveResult.error || this.LABELS.saveError });
            return;
        }

        webix.message({ type: "success", text: this.LABELS.saveSuccess });
    }
}

export default async function createSoftwareUpdateSettingsView() {
    const view = new SoftwareUpdateSettingsView();
    await view.loadData();
    return view;
}
