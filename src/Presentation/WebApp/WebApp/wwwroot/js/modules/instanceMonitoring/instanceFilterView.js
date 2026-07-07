class InstanceFilterView {
    constructor() {
        this.LABELS = {
            formTitle: "Фильтр инстансов",
            instanceName: "Имя инстанса",
            localModuleVersion: "Версия ЛМ ЧЗ",
            tsPiotVersion: "Версия ТС ПИоТ",
            tsPiotLicense: "Дата окончания лицензии",
            applyButton: "Применить",
            resetButton: "Сбросить",
            cancelButton: "Отмена",
            allOption: "— все —"
        };

        this.NAMES = {
            windowId: "instanceFilterWindow",
            formId: "instanceFilterForm",
            name: "filterName",
            localModuleVersion: "filterLocalModuleVersion",
            tsPiotVersion: "filterTsPiotVersion",
            tsPiotLicense: "filterTsPiotLicense",
            allValue: "__all__"
        };
    }

    showDialog(filterOptions = {}, currentFilters = {}, onApply, onClose) {
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
                    this._createSelect(
                        this.LABELS.localModuleVersion,
                        this.NAMES.localModuleVersion,
                        filterOptions.localModuleVersions,
                        currentFilters.localModuleVersion
                    ),
                    this._createSelect(
                        this.LABELS.tsPiotVersion,
                        this.NAMES.tsPiotVersion,
                        filterOptions.tsPiotVersions,
                        currentFilters.tsPiotVersion
                    ),
                    this._createDateInput(
                        this.LABELS.tsPiotLicense,
                        this.NAMES.tsPiotLicense,
                        currentFilters.tsPiotLicense
                    ),
                    this._createButtons(onApply, onClose)
                ]
            }
        }).show();
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

    _createDateInput(label, name, value) {
        return {
            view: "datepicker",
            label,
            labelPosition: "top",
            name,
            id: name,
            value: this._parseLicenseDate(value),
            format: "%d.%m.%Y"
        };
    }

    _createSelect(label, name, values, selectedValue) {
        return {
            view: "richselect",
            label,
            labelPosition: "top",
            name,
            id: name,
            value: this._toSelectValue(selectedValue),
            options: this._buildSelectOptions(values)
        };
    }

    _toSelectValue(value) {
        return value ? String(value) : this.NAMES.allValue;
    }

    _fromSelectValue(value) {
        return value === this.NAMES.allValue ? "" : (value || "");
    }

    _buildSelectOptions(values) {
        const items = [{ id: this.NAMES.allValue, value: this.LABELS.allOption }];

        (values || []).forEach((value) => {
            const id = String(value);
            items.push({ id, value: id });
        });

        return items;
    }

    _parseLicenseDate(value) {
        if (!value) {
            return "";
        }

        const date = new Date(value);
        if (isNaN(date.getTime())) {
            return "";
        }

        return date;
    }

    _formatLicenseFilterValue(value) {
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

    _getFormValues() {
        const form = $$(this.NAMES.formId);
        const values = form.getValues();

        return {
            name: (values[this.NAMES.name] || "").trim(),
            localModuleVersion: this._fromSelectValue(values[this.NAMES.localModuleVersion]),
            tsPiotVersion: this._fromSelectValue(values[this.NAMES.tsPiotVersion]),
            tsPiotLicense: this._formatLicenseFilterValue(values[this.NAMES.tsPiotLicense])
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
        const form = $$(this.NAMES.formId);
        form.setValues({
            [this.NAMES.name]: "",
            [this.NAMES.localModuleVersion]: this.NAMES.allValue,
            [this.NAMES.tsPiotVersion]: this.NAMES.allValue,
            [this.NAMES.tsPiotLicense]: ""
        });

        this._closeWindow();

        if (onApply) {
            onApply({
                name: "",
                localModuleVersion: "",
                tsPiotVersion: "",
                tsPiotLicense: ""
            });
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

export default new InstanceFilterView();
