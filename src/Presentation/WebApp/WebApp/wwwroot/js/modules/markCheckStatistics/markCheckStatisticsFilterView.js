import {
    getDefaultFilters,
    getPresetDates,
    PERIOD_PRESETS,
} from './markCheckStatisticsPeriodPresets.js';

class MarkCheckStatisticsFilterView {
    constructor() {
        this.LABELS = {
            formTitle: "Фильтр статистики проверок",
            instanceName: "Имя инстанса",
            successRateMin: "Min. % success",
            offlineRateMin: "Min. % offline",
            dateFrom: "Дата с",
            dateTo: "Дата по",
            applyButton: "Применить",
            resetButton: "Сбросить",
            cancelButton: "Отмена"
        };

        this.NAMES = {
            windowId: "markCheckStatisticsFilterWindow",
            formId: "markCheckStatisticsFilterForm",
            name: "filterName",
            successRateMin: "filterSuccessRateMin",
            offlineRateMin: "filterOfflineRateMin",
            dateFrom: "filterDateFrom",
            dateTo: "filterDateTo"
        };
    }

    showDialog(currentFilters = {}, onApply, onClose) {
        if ($$(this.NAMES.windowId)) {
            $$(this.NAMES.windowId).destructor();
        }

        webix.ui({
            view: "window",
            id: this.NAMES.windowId,
            modal: true,
            width: 480,
            position: "center",
            head: this.LABELS.formTitle,
            body: {
                view: "form",
                id: this.NAMES.formId,
                elements: [
                    this._createTextInput(
                        this.LABELS.instanceName,
                        this.NAMES.name,
                        currentFilters.name
                    ),
                    this._createNumberInput(
                        this.LABELS.successRateMin,
                        this.NAMES.successRateMin,
                        currentFilters.successRateMin
                    ),
                    this._createNumberInput(
                        this.LABELS.offlineRateMin,
                        this.NAMES.offlineRateMin,
                        currentFilters.offlineRateMin
                    ),
                    this._createDateInput(
                        this.LABELS.dateFrom,
                        this.NAMES.dateFrom,
                        currentFilters.dateFrom
                    ),
                    this._createDateInput(
                        this.LABELS.dateTo,
                        this.NAMES.dateTo,
                        currentFilters.dateTo
                    ),
                    this._createButtons(onApply, onClose)
                ]
            }
        }).show();
    }

    getDefaultFilters() {
        return getDefaultFilters();
    }

    getPresetDates(preset) {
        return getPresetDates(preset);
    }

    _createTextInput(label, name, value) {
        return {
            view: "text",
            label,
            labelPosition: "top",
            name,
            id: name,
            value: value || "",
            placeholder: "введите имя"
        };
    }

    _createNumberInput(label, name, value) {
        return {
            view: "text",
            label,
            labelPosition: "top",
            name,
            id: name,
            value: value != null ? String(value) : "",
            placeholder: "0–100",
            attributes: { type: "number", min: 0, max: 100, step: 0.01 }
        };
    }

    _createDateInput(label, name, value) {
        return {
            view: "datepicker",
            label,
            labelPosition: "top",
            name,
            id: name,
            value: this._parseDate(value),
            format: "%d.%m.%Y"
        };
    }

    _createButtons(onApply, onClose) {
        return {
            cols: [
                {
                    view: "button",
                    value: this.LABELS.applyButton,
                    click: () => this._applyFilters(onApply),
                    hotkey: "alt+enter"
                },
                {
                    view: "button",
                    value: this.LABELS.resetButton,
                    click: () => this._resetFilters(onApply)
                },
                {
                    view: "button",
                    value: this.LABELS.cancelButton,
                    click: () => this._closeDialog(onClose),
                    hotkey: "esc"
                }
            ]
        };
    }

    _parseDate(value) {
        if (!value) {
            return "";
        }

        const isoMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value).trim());
        if (isoMatch) {
            return new Date(Number(isoMatch[1]), Number(isoMatch[2]) - 1, Number(isoMatch[3]));
        }

        const date = new Date(value);
        return isNaN(date.getTime()) ? "" : date;
    }

    _formatDateParam(value) {
        if (!value) {
            return "";
        }

        const date = value instanceof Date ? value : new Date(value);
        if (isNaN(date.getTime())) {
            return String(value).trim();
        }

        const year = date.getFullYear();
        const month = (date.getMonth() + 1).toString().padStart(2, "0");
        const day = date.getDate().toString().padStart(2, "0");

        return `${year}-${month}-${day}`;
    }

    _getFormValues() {
        const form = $$(this.NAMES.formId);
        const values = form.getValues();

        return {
            name: (values[this.NAMES.name] || "").trim(),
            successRateMin: (values[this.NAMES.successRateMin] || "").trim(),
            offlineRateMin: (values[this.NAMES.offlineRateMin] || "").trim(),
            dateFrom: this._formatDateParam(values[this.NAMES.dateFrom]),
            dateTo: this._formatDateParam(values[this.NAMES.dateTo]),
            periodPreset: PERIOD_PRESETS.custom,
        };
    }

    _applyFilters(onApply) {
        const filters = this._getFormValues();
        this._closeWindow();

        if (onApply) {
            onApply(filters);
        }
    }

    _resetFilters(onApply) {
        this._closeWindow();

        if (onApply) {
            onApply(this.getDefaultFilters());
        }
    }

    _closeDialog(onClose) {
        this._closeWindow();

        if (onClose) {
            onClose();
        }
    }

    _closeWindow() {
        const window = $$(this.NAMES.windowId);
        if (window) {
            window.close();
        }
    }
}

export default new MarkCheckStatisticsFilterView();
