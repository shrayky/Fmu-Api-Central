import markCheckStatisticsService from '../../services/markCheckStatisticsService.js';
import markCheckStatisticsFilterView from './markCheckStatisticsFilterView.js';
import {
    detectPreset,
    getDefaultFilters,
    getPresetDates,
    PERIOD_PRESETS,
    PRESET_LABELS,
} from './markCheckStatisticsPeriodPresets.js';

class MarkCheckStatisticsListView {
    constructor(id) {
        this.formName = "MarkCheckStatisticsView";
        this.id = id;

        const formSettings = this._loadFormSettings();

        this.pageSize = formSettings.pageSize;
        this.pageNumber = 1;
        this.filters = this._loadFilters();
        this.activePreset = this.filters.periodPreset || PERIOD_PRESETS.today;

        this.LABELS = {
            formTitle: "Fmu-Api-Central: Статистика проверок марок",
            refresh: "Обновить",
            page: "Страница",
            prevButton: "←",
            nextButton: "→",
            instanceName: "Имя инстанса",
            total: "Total",
            onlineChecks: "Online",
            offlineChecks: "Offline",
            successRate: "% success",
            offlineRate: "% offline",
            errorLoad: "Ошибка при загрузке данных",
            printList: "Печать списка",
            filter: "Фильтр",
            filterActive: "Фильтр *",
            filterSummaryPrefix: "Установлены фильтры:",
            filterName: "имя инстанса",
            filterSuccessRateMin: "мин. % success",
            filterOfflineRateMin: "мин. % offline",
            filterDateFrom: "дата с",
            filterDateTo: "дата по",
            filterPeriod: "период",
        };

        this.NAMES = {
            toolbarLabel: "toolbarLabel",
            refreshBtn: "markCheckStatisticsRefreshBtn",
            filterBtn: "markCheckStatisticsFilterBtn",
            prevButton: "markCheckStatisticsPrevButton",
            nextButton: "markCheckStatisticsNextButton",
            paginationInfo: "markCheckStatisticsPaginationInfo",
            dataTable: "markCheckStatisticsDataTable",
            filterSummaryLabel: "markCheckStatisticsFilterSummaryLabel",
            instanceName: "instanceName",
            total: "total",
            onlineChecks: "successfulOnlineChecks",
            offlineChecks: "successfulOfflineChecks",
            successRate: "successRatePercentage",
            offlineRate: "offlineRatePercentage",
            formId: "markCheckStatisticsListViewForm",
            periodToolbar: "markCheckStatisticsPeriodToolbar",
        };

        this.hotkeys = [
            { key: "f5", buttonId: this.NAMES.refreshBtn },
            { key: "ctrl+left", buttonId: this.NAMES.prevButton },
            { key: "ctrl+right", buttonId: this.NAMES.nextButton }
        ];
    }

    _loadFormSettings() {
        return {
            pageSize: parseInt(localStorage.getItem('markCheckStatistics_pageSize')) || 50,
        };
    }

    _loadFilters() {
        try {
            const stored = JSON.parse(localStorage.getItem('markCheckStatistics_filters') || 'null');
            if (stored && typeof stored === 'object') {
                if (!stored.periodPreset) {
                    stored.periodPreset = detectPreset(stored);
                }
                return stored;
            }
        } catch {
            // ignore
        }

        return getDefaultFilters();
    }

    _saveFilters() {
        localStorage.setItem('markCheckStatistics_filters', JSON.stringify(this.filters));
    }

    _saveSettings() {
        localStorage.setItem('markCheckStatistics_pageSize', this.pageSize.toString());
    }

    delayedDataLoading() {
        setTimeout(() => {
            this._loadData();
        }, 10);

        return this;
    }

    render() {
        $$("toolbarLabel").setValue(this.LABELS.formTitle);

        const form = {
            view: "form",
            id: this.NAMES.formId,
            elements: [
                this._toolbar(),
                this._periodToolbar(),
                this._dataTable(),
                this._filterSummary(),
                this._footer(),
            ]
        };

        return {
            id: this.id,
            disabled: true,
            rows: [{ rows: [form] }]
        };
    }

    _toolbar() {
        return {
            view: "toolbar",
            elements: [
                {
                    view: "button",
                    id: this.NAMES.refreshBtn,
                    value: this.LABELS.refresh,
                    width: 100,
                    click: () => this._loadData(),
                },
                {
                    view: "button",
                    id: this.NAMES.filterBtn,
                    value: this._hasServerFilters() ? this.LABELS.filterActive : this.LABELS.filter,
                    width: 100,
                    click: () => this._showFilterDialog(),
                },
                {
                    view: "menu",
                    id: "markCheckStatisticsReportsMenu",
                    autowidth: true,
                    data: [
                        {
                            id: "reports",
                            value: "Отчеты",
                            autowidth: true,
                            submenu: [
                                { id: "reports:print", value: this.LABELS.printList },
                            ]
                        }
                    ],
                    on: {
                        onMenuItemClick: (id) => {
                            if (id === "reports:print") {
                                this._printList();
                            }
                        }
                    }
                },
                {},
                {
                    view: "button",
                    id: this.NAMES.prevButton,
                    value: this.LABELS.prevButton,
                    width: 50,
                    disabled: true,
                    click: () => this._goToPage(this.pageNumber - 1),
                },
                {
                    view: "label",
                    id: this.NAMES.paginationInfo,
                    label: this.LABELS.page + " " + this.pageNumber,
                    width: 150,
                    align: "center"
                },
                {
                    view: "button",
                    id: this.NAMES.nextButton,
                    value: this.LABELS.nextButton,
                    width: 50,
                    disabled: true,
                    click: () => this._goToPage(this.pageNumber + 1),
                },
            ]
        };
    }

    _periodToolbar() {
        const presets = [
            PERIOD_PRESETS.today,
            PERIOD_PRESETS.yesterday,
            PERIOD_PRESETS.week,
            PERIOD_PRESETS.month,
        ];

        return {
            view: "toolbar",
            id: this.NAMES.periodToolbar,
            css: "mark-check-period-toolbar",
            elements: presets.map((preset) => ({
                view: "button",
                id: `markCheckPreset_${preset}`,
                value: PRESET_LABELS[preset],
                width: 100,
                css: this.activePreset === preset ? "webix_primary" : "webix_secondary",
                click: () => this._applyPeriodPreset(preset),
            }))
        };
    }

    _applyPeriodPreset(preset) {
        const dates = getPresetDates(preset);

        this.activePreset = preset;
        this.filters = {
            ...this.filters,
            ...dates,
            periodPreset: preset,
        };

        this.pageNumber = 1;
        this._saveFilters();
        this._updatePresetButtons();
        this._updateFilterButtonState();
        this._updateFilterSummary();
        this._loadData();
    }

    _updatePresetButtons() {
        [PERIOD_PRESETS.today, PERIOD_PRESETS.yesterday, PERIOD_PRESETS.week, PERIOD_PRESETS.month]
            .forEach((preset) => {
                this._setPresetButtonActive(`markCheckPreset_${preset}`, this.activePreset === preset);
            });
    }

    _setPresetButtonActive(buttonId, isActive) {
        const button = $$(buttonId);
        if (!button?.getNode) {
            return;
        }

        const node = button.getNode();
        node.classList.remove("webix_primary", "webix_secondary");

        if (isActive) {
            node.classList.add("webix_primary");
            return;
        }

        node.classList.add("webix_secondary");
    }

    _filterSummary() {
        return {
            view: "label",
            id: this.NAMES.filterSummaryLabel,
            label: "",
            hidden: true,
            height: 28,
            css: "instance-filter-summary",
            align: "left"
        };
    }

    _footer() {
        return {
            view: "toolbar",
            name: "footer",
            borderless: true,
            elements: [
                {
                    view: "label",
                    label: "Элементов на странице:",
                    width: 210
                },
                {
                    view: "select",
                    id: "markCheckStatisticsPageSizeSelect",
                    value: this.pageSize,
                    width: 80,
                    options: [
                        { id: 25, value: "25" },
                        { id: 50, value: "50" },
                        { id: 100, value: "100" },
                        { id: 200, value: "200" },
                        { id: 500, value: "500" }
                    ],
                    on: {
                        onChange: (newValue) => this._changePageSize(newValue)
                    }
                },
            ]
        };
    }

    _dataTable() {
        return {
            view: "datatable",
            id: this.NAMES.dataTable,
            columns: [
                {
                    id: this.NAMES.instanceName,
                    header: [this.LABELS.instanceName],
                    fillspace: true,
                    sort: "string",
                },
                {
                    id: this.NAMES.total,
                    header: [this.LABELS.total],
                    width: 90,
                    sort: "int",
                },
                {
                    id: this.NAMES.onlineChecks,
                    header: [this.LABELS.onlineChecks],
                    width: 90,
                    sort: "int",
                },
                {
                    id: this.NAMES.offlineChecks,
                    header: [this.LABELS.offlineChecks],
                    width: 90,
                    sort: "int",
                },
                {
                    id: this.NAMES.successRate,
                    header: [this.LABELS.successRate],
                    width: 110,
                    template: (obj) => this._formatPercentage(obj.successRatePercentage),
                    sort: "int",
                },
                {
                    id: this.NAMES.offlineRate,
                    header: [this.LABELS.offlineRate],
                    width: 110,
                    template: (obj) => this._formatPercentage(obj.offlineRatePercentage),
                    sort: "int",
                },
            ],
            select: "row",
            multiselect: false,
            on: {
                onAfterLoad: () => {
                    webix.UIManager.setFocus(this.NAMES.dataTable);
                }
            }
        };
    }

    async _loadData() {
        this._disableHotkeys();

        try {
            const data = await markCheckStatisticsService.list(
                this.pageNumber,
                this.pageSize,
                this.filters
            );

            if (!data.content) {
                console.warn("no data");
                $$(this.id).enable();
                return;
            }

            if (!data.listEnabled) {
                webix.message({
                    text: data.description,
                    type: "error"
                });
                return;
            }

            const table = $$(this.NAMES.dataTable);
            const selectedId = table.getSelectedId();

            table.clearAll();
            table.parse(data.content);

            $$(this.id).enable();

            if (data.content.length > 0) {
                const rowToSelect = selectedId && table.exists(selectedId)
                    ? selectedId
                    : data.content[0].id;
                table.select(rowToSelect);
            }

            this.activePreset = this.filters.periodPreset || detectPreset(this.filters);
            this._updatePresetButtons();
            this._updatePagination(data);
            this.pageNumber = data.currentPage || this.pageNumber;
            this._updateFilterButtonState();
            this._updateFilterSummary();
        } catch (error) {
            console.error(this.LABELS.errorLoad, error);
            webix.message({
                text: this.LABELS.errorLoad,
                type: "error"
            });
        }

        this._enableHotkeys();
    }

    _updatePagination(data) {
        if (!data) {
            return;
        }

        const prevButton = $$(this.NAMES.prevButton);
        const nextButton = $$(this.NAMES.nextButton);
        const paginationInfo = $$(this.NAMES.paginationInfo);

        if (prevButton) {
            prevButton.enable();
            if (data.currentPage <= 1) {
                prevButton.disable();
            }
        }

        if (nextButton) {
            nextButton.enable();
            if (data.currentPage >= data.totalPages) {
                nextButton.disable();
            }
        }

        data.totalPages = data.totalPages == 0 ? 1 : data.totalPages;

        if (paginationInfo) {
            paginationInfo.setValue(
                `${data.currentPage} из ${data.totalPages}`
            );
        }
    }

    _showFilterDialog() {
        this._disableHotkeys();

        markCheckStatisticsFilterView.showDialog(
            this.filters,
            (filters) => {
                this.filters = filters;
                this.activePreset = filters.periodPreset || detectPreset(filters);
                this._saveFilters();
                this.pageNumber = 1;
                this._updatePresetButtons();
                this._updateFilterButtonState();
                this._updateFilterSummary();
                this._loadData();
                this._enableHotkeys();
            },
            () => this._enableHotkeys()
        );
    }

    _hasServerFilters() {
        return !!(
            this.filters?.name ||
            this.filters?.successRateMin ||
            this.filters?.offlineRateMin ||
            this.activePreset === PERIOD_PRESETS.custom
        );
    }

    _updateFilterButtonState() {
        const button = $$(this.NAMES.filterBtn);
        if (!button) {
            return;
        }

        button.setValue(this._hasServerFilters() ? this.LABELS.filterActive : this.LABELS.filter);
    }

    _buildFilterSummaryText() {
        const parts = [];

        if (this.activePreset && this.activePreset !== PERIOD_PRESETS.custom) {
            parts.push(`${this.LABELS.filterPeriod} = ${PRESET_LABELS[this.activePreset]}`);
        } else {
            if (this.filters?.dateFrom) {
                parts.push(`${this.LABELS.filterDateFrom} = ${this._formatFilterDate(this.filters.dateFrom)}`);
            }

            if (this.filters?.dateTo) {
                parts.push(`${this.LABELS.filterDateTo} = ${this._formatFilterDate(this.filters.dateTo)}`);
            }
        }

        if (this.filters?.name) {
            parts.push(`${this.LABELS.filterName} = ${this.filters.name}`);
        }

        if (this.filters?.successRateMin) {
            parts.push(`${this.LABELS.filterSuccessRateMin} = ${this.filters.successRateMin}`);
        }

        if (this.filters?.offlineRateMin) {
            parts.push(`${this.LABELS.filterOfflineRateMin} = ${this.filters.offlineRateMin}`);
        }

        if (parts.length === 0) {
            return "";
        }

        return `${this.LABELS.filterSummaryPrefix} ${parts.join(", ")}`;
    }

    _formatFilterDate(value) {
        const isoMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value).trim());
        if (isoMatch) {
            return `${isoMatch[3]}.${isoMatch[2]}.${isoMatch[1]}`;
        }

        const date = new Date(value);
        if (isNaN(date.getTime())) {
            return String(value);
        }

        const day = date.getDate().toString().padStart(2, "0");
        const month = (date.getMonth() + 1).toString().padStart(2, "0");
        const year = date.getFullYear().toString();

        return `${day}.${month}.${year}`;
    }

    _updateFilterSummary() {
        const label = $$(this.NAMES.filterSummaryLabel);
        if (!label) {
            return;
        }

        const summaryText = this._buildFilterSummaryText();
        if (!summaryText) {
            label.hide();
            return;
        }

        label.setValue(summaryText);
        label.show();
    }

    _goToPage(page) {
        if (page >= 1) {
            this.pageNumber = page;
            this._loadData();
        }
    }

    _changePageSize(newSize) {
        this.pageSize = newSize;
        this.pageNumber = 1;
        this._loadData();
        this._saveSettings();
    }

    _formatPercentage(value) {
        if (value == null || isNaN(value)) {
            return "";
        }

        return `${Number(value).toFixed(2)}%`;
    }

    _printList() {
        const table = $$(this.NAMES.dataTable);
        webix.toPDF(table, { autowidth: true, filterHTML: true });
    }

    _disableHotkeys() {
        this.hotkeys.forEach(({ key }) => {
            webix.UIManager.removeHotKey(key, null);
        });
    }

    _enableHotkeys() {
        this.hotkeys.forEach(({ key, buttonId }) => {
            const button = $$(buttonId);
            if (button) {
                button.define({ hotkey: key });
            }
        });
    }
}

export default async function createMarkCheckStatisticsListView(id) {
    const view = new MarkCheckStatisticsListView(id)
        .delayedDataLoading()
        .render();

    return view;
}
