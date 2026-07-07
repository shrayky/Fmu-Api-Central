import instanceMonitoringService from '../../services/instanceMonitoringService.js';
import instanceElementView from './instanceElementView.js';
import instanceFilterView from './instanceFilterView.js';
import { pollingManager } from '../../services/PollingManager.js';

const style = document.createElement('style');

style.textContent = `
    .multiline_datatable .webix_cell {
        white-space: normal !important;
        vertical-align: top !important;
        line-height: 1.35 !important;
        padding: 8px 10px !important;
        overflow: visible !important;
        box-sizing: border-box !important;
    }

    .multiline_datatable .instance-module-item {
        margin-bottom: 8px;
        padding-bottom: 8px;
        border-bottom: 1px solid rgba(128, 128, 128, 0.35);
    }

    .multiline_datatable .instance-module-item:last-child {
        margin-bottom: 0;
        padding-bottom: 0;
        border-bottom: none;
    }

    .multiline_datatable .instance-module-empty {
        color: #888;
    }
`;

document.head.appendChild(style);

class InstanceListView {
    constructor(id) {
        this.formName = "SoftwareUpdatesView";
        this.id = id;

        const formSettings = this._loadFormSettings();

        this.pageSize = formSettings.pageSize;
        this.autoRefreshEnabled = formSettings.autoRefreshEnabled;
        this.refreshInterval = formSettings.refreshInterval;
        this.pageNumber = 1;
        this.filters = this._loadFilters();
        this.lastTotalCount = 0;
        this.filterOptions = this._createEmptyFilterOptions();

        this.LABELS = {
            formTitle: "Fmu-Api-Central: Мониторинг инстансов",
            refresh: "Обновить",
            add: "Добавить",
            edit: "Редактировать",
            delete: "Удалить",
            page: "Страница",
            prevButton: "←",
            nextButton: "→",
            instanceName: "Имя инстанса",
            instanceToken: "Токен инстанса",
            instanceCreatedAt: "Дата создания",
            instanceUpdatedAt: "Обновлен",
            instanceVersion: "Версия",
            errorLoad: "Ошибка при загрузке данных",
            autoRefresh: "Автообновление",
            refreshInterval: "Интервал (сек)",
            hostAddress: "Web-адрес Fmu-Api",
            printInstancesList: "Печать списка инстансов",
            exportToCsv: "Экспорт списка в csv",
            filter: "Фильтр",
            filterActive: "Фильтр *",
            filterSummaryPrefix: "Установлены фильтры:",
            filterName: "имя инстанса",
            filterLocalModuleVersion: "версия ЛМ ЧЗ",
            filterTsPiotVersion: "версия ТС ПИоТ",
            filterTsPiotLicense: "лицензия ТС ПИоТ до",
            token: "Токен",
            tsPiotsModules: "Модули ТСПИоТ"
        };

        this.NAMES = {
            toolbarLabel: "toolbarLabel",
            refreshBtn: "refreshBtn",
            filterBtn: "filterBtn",
            addBtn: "addBtn",
            editBtn: "editBtn",
            deleteBtn: "deleteBtn",
            prevButton: "prevButton",
            nextButton: "nextButton",
            paginationInfo: "paginationInfo",
            dataTable: "dataTable",
            filterSummaryLabel: "filterSummaryLabel",
            instanceName: "name",
            instanceVersion: "version",
            instanceUpdatedAt: "lastUpdated",
            instanceToken: "id",
            autoRefresh: "autoRefreshCheckbox",
            refreshInterval: "refreshIntervalInput",
            formId: "instanceMonitoringListViewForm",
            hostAddress: "address",
            id: "id",
            tsPiots: "tsPiots"
        };

        this.hotkeys = [
            { key: "insert", buttonId: this.NAMES.addBtn },
            { key: "delete", buttonId: this.NAMES.deleteBtn },
            { key: "f5", buttonId: this.NAMES.refreshBtn },
            { key: "ctrl+left", buttonId: this.NAMES.prevButton },
            { key: "ctrl+right", buttonId: this.NAMES.nextButton }
        ]
    }

    _loadFormSettings() {
        return {
            refreshInterval: parseInt(localStorage.getItem('instanceMonitoring_refreshInterval')) || 60,
            autoRefreshEnabled: JSON.parse(localStorage.getItem('instanceMonitoring_autoRefresh') || true),
            pageSize: parseInt(localStorage.getItem('instanceMonitoring_pageSize')) || 50,
        };
    }

    _loadFilters() {
        try {
            return JSON.parse(localStorage.getItem('instanceMonitoring_filters') || '{}');
        } catch {
            return {};
        }
    }

    _saveFilters() {
        localStorage.setItem('instanceMonitoring_filters', JSON.stringify(this.filters));
    }

    _saveSettings() {
        localStorage.setItem('instanceMonitoring_refreshInterval', this.refreshInterval.toString());
        localStorage.setItem('instanceMonitoring_autoRefresh', JSON.stringify(this.autoRefreshEnabled));
        localStorage.setItem('instanceMonitoring_pageSize', this.pageSize.toString());
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
                this._dataTable(),
                this._filterSummary(),
                this._footer(),
            ]
        };

        const view = {
            id: this.id,
            disabled: true,
            rows: [
                {
                    rows: [
                        form,
                    ]
                }
            ]
        };

        return view;
    }

    _toolbar() {
        return {
            view: "toolbar",
            elements: [
                {
                    view: "button",
                    id: this.NAMES.addBtn,
                    value: this.LABELS.add,
                    width: 100,
                    click: () => this._showAddDialog(),
                },
                {
                    view: "button",
                    id: this.NAMES.deleteBtn,
                    value: this.LABELS.delete,
                    width: 100,
                    click: () => this._deleteInstance(),
                },
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
                    id: "reportsMenu",
                    autowidth: true,
                    data: [
                        {
                            id: "reports",
                            value: "Отчеты",
                            autowidth: true,
                            submenu: [
                                { id: "reports:print", value: this.LABELS.printInstancesList },
                                { id: "reports:csv", value: this.LABELS.exportToCsv },
                            ]
                        }
                    ],
                    on: {
                        onMenuItemClick: (id) => {
                            if (id === "reports:print") {
                                this._printInstances();
                                return;
                            }

                            if (id === "reports:csv") {
                                this._exportInstancesCsv();
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
            id: "footer",
            borderless: true,
            elements: [
                {
                    view: "label",
                    label: "Элементов на странице:",
                    width: 210
                },
                {
                    view: "select",
                    id: "pageSizeSelect",
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
                {},
                {
                    view: "label",
                    label: this.LABELS.autoRefresh,
                    width: 140
                },
                {
                    view: "checkbox",
                    id: "autoRefreshCheckbox",
                    value: this.autoRefreshEnabled,
                    width: 40,
                    click: (elementId) => {
                        this.autoRefreshEnabled = $$(elementId).getValue();
                        this._toggleAutoRefresh(this.autoRefreshEnabled)
                    }
                },
                {
                    view: "text",
                    id: "refreshIntervalInput",
                    value: this.refreshInterval,
                    width: 80,
                    placeholder: "сек",
                    on: {
                        onBlur: () => this._updateRefreshInterval()
                    }
                },
            ]
        }
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
                    id: this.NAMES.hostAddress,
                    header: this.LABELS.hostAddress,
                    fillspace: true
                },
                {
                    id: this.NAMES.id,
                    header: this.LABELS.token,
                    hidden: true,
                    fillspace: true
                },
                {
                    id: this.NAMES.instanceVersion,
                    header: [this.LABELS.instanceVersion],
                    width: 120
                },
                {
                    id: "localModules",
                    header: "Локальные модули",
                    fillspace: true,
                    template: (obj) => this._formatLocalModules(obj.localModules)
                },
                {
                    id: this.NAMES.tsPiots,
                    header: this.LABELS.tsPiotsModules,
                    fillspace: true,
                    template: (obj) => this._formatTsPiots(obj.TsPiots)
                },
                {
                    id: this.NAMES.instanceUpdatedAt,
                    header: [this.LABELS.instanceUpdatedAt],
                    width: 120,
                    template: (obj) => this._formatDate(obj.lastUpdated),
                    sort: "int",
                },
            ],
            select: "row",
            multiselect: false,
            fixedRowHeight: false,
            rowHeight: 36,
            css: "multiline_datatable",
            on: {
                onResize: () => this._scheduleRowHeightAdjust(),
                onItemDblClick: (cell) => {
                    if (cell.column === this.NAMES.hostAddress) {
                        const record = $$(this.NAMES.dataTable).getItem(cell.row);
                        const addr = record ? record[this.NAMES.hostAddress] : "";

                        if (addr && typeof addr === "string" && addr.trim() !== "") {
                            try { window.open(addr, "_blank"); } catch (_) { }
                            return;
                        }
                    }

                    this._editInstance(cell.row);
                },
                onAfterFilter: () => {
                    this._syncAutoRefreshState();
                }
            }
        };
    }

    async _loadData() {
        this._disableHotkeys();

        try {
            const data = await instanceMonitoringService.list(this.pageNumber, this.pageSize, this.filters);

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

            this.lastTotalCount = data.totalCount || data.content.length;
            const resetFilterOptions = this.pageNumber === 1 && !this._hasServerFilters();
            this._updateFilterOptionsFromPacket(data, resetFilterOptions);

            const table = $$(this.NAMES.dataTable);
            const selectedId = table.getSelectedId();
            this._assignRowHeightsToRecords(data.content);

            table.clearAll();
            table.parse(data.content);

            $$(this.id).enable();

            if (data.content.length > 0) {
                const rowToSelect = selectedId && table.exists(selectedId)
                    ? selectedId
                    : data.content[0].id;
                table.select(rowToSelect);
                webix.UIManager.setFocus(this.NAMES.dataTable);
            }

            this._updatePagination(data);
            this.pageNumber = data.currentPage || this.pageNumber;
            this._updateFilterButtonState();
            this._updateFilterSummary();
            this._syncAutoRefreshState();
        } catch (error) {
            console.error(this.LABELS.errorLoad, error);
            webix.message({
                text: this.LABELS.errorLoad,
                type: "error"
            });
        }

        this._enableHotkeys();
    }

    async _deleteInstance() {
        const recordId = $$(this.NAMES.dataTable).getSelectedId();

        if (!recordId) {
            webix.message({
                text: "Выберите запись для удаления",
                type: "error"
            });
            return;
        }

        webix.confirm({
            title: "Вы уверены?",
            text: "Вы собираетесь удалить запись?",
            ok: "Да",
            cancel: "Нет",
        }).then(async () => {
            try {
                await instanceMonitoringService.delete(recordId);
                $$(this.NAMES.dataTable).remove(recordId);
                webix.message("Запись удалена успешно");
            } catch (error) {
                console.error(this.LABELS.errorDelete, error);
                webix.message({
                    text: this.LABELS.errorDelete,
                    type: "error"
                });
            }
        });
    }

    _updatePagination(data) {
        if (!data)
            return;

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

        instanceFilterView.showDialog(
            this._getFilterOptionsDto(),
            this.filters,
            (filters) => {
                this.filters = filters;
                this._saveFilters();
                this.pageNumber = 1;
                this._updateFilterButtonState();
                this._updateFilterSummary();
                this._syncAutoRefreshState();
                this._loadData();
                this._enableHotkeys();
            },
            () => this._enableHotkeys()
        );
    }

    _hasServerFilters() {
        return !!(
            this.filters?.name ||
            this.filters?.localModuleVersion ||
            this.filters?.tsPiotVersion ||
            this.filters?.tsPiotLicense
        );
    }

    _createEmptyFilterOptions() {
        return {
            localModuleVersions: new Set(),
            tsPiotVersions: new Set()
        };
    }

    _updateFilterOptionsFromPacket(data, reset = false) {
        if (reset) {
            this.filterOptions = this._createEmptyFilterOptions();
        }

        this._updateFilterOptionsFromContent(data.content);

        const packetOptions = data.filterOptions ?? data.FilterOptions;
        if (packetOptions) {
            this._mergeFilterOptionsFromPacket(packetOptions);
        }

        this._ensureSelectedFiltersInOptions();
    }

    _updateFilterOptionsFromContent(content) {
        if (!content || !this.filterOptions) {
            return;
        }

        content.forEach((record) => {
            (record.localModules || []).forEach((module) => {
                const version = module.version ?? module.Version;
                if (version) {
                    this.filterOptions.localModuleVersions.add(String(version).trim());
                }
            });

            const tsPiots = record.TsPiots || record.tsPiots || [];
            tsPiots.forEach((item) => {
                const version = item.version ?? item.Version;
                if (version) {
                    this.filterOptions.tsPiotVersions.add(String(version).trim());
                }
            });
        });
    }

    _mergeFilterOptionsFromPacket(packetOptions) {
        const addValues = (set, values) => {
            (values || []).forEach((value) => {
                if (value != null && String(value).trim() !== "") {
                    set.add(String(value));
                }
            });
        };

        addValues(
            this.filterOptions.localModuleVersions,
            packetOptions.localModuleVersions ?? packetOptions.LocalModuleVersions
        );
        addValues(
            this.filterOptions.tsPiotVersions,
            packetOptions.tsPiotVersions ?? packetOptions.TsPiotVersions
        );
    }

    _ensureSelectedFiltersInOptions() {
        if (this.filters?.localModuleVersion) {
            this.filterOptions.localModuleVersions.add(this.filters.localModuleVersion);
        }

        if (this.filters?.tsPiotVersion) {
            this.filterOptions.tsPiotVersions.add(this.filters.tsPiotVersion);
        }
    }

    _getFilterOptionsDto() {
        const sortValues = (values) => [...values].sort((a, b) =>
            a.localeCompare(b, "ru", { sensitivity: "base" })
        );

        return {
            localModuleVersions: sortValues(this.filterOptions.localModuleVersions),
            tsPiotVersions: sortValues(this.filterOptions.tsPiotVersions)
        };
    }

    _updateFilterButtonState() {
        const button = $$(this.NAMES.filterBtn);
        if (!button) {
            return;
        }

        button.setValue(this._hasServerFilters() ? this.LABELS.filterActive : this.LABELS.filter);
    }

    // Формирует текстовое описание активных серверных фильтров.
    _buildFilterSummaryText() {
        const parts = [];

        if (this.filters?.name) {
            parts.push(`${this.LABELS.filterName} = ${this.filters.name}`);
        }

        if (this.filters?.localModuleVersion) {
            parts.push(`${this.LABELS.filterLocalModuleVersion} = ${this.filters.localModuleVersion}`);
        }

        if (this.filters?.tsPiotVersion) {
            parts.push(`${this.LABELS.filterTsPiotVersion} = ${this.filters.tsPiotVersion}`);
        }

        if (this.filters?.tsPiotLicense) {
            parts.push(`${this.LABELS.filterTsPiotLicense} = ${this._formatFilterDate(this.filters.tsPiotLicense)}`);
        }

        if (parts.length === 0) {
            return "";
        }

        return `${this.LABELS.filterSummaryPrefix} ${parts.join(", ")}`;
    }

    // Форматирует дату фильтра в виде дд.мм.гггг.
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

    // Обновляет отображение строки с активными фильтрами под таблицей.
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

    _syncAutoRefreshState() {
        const table = $$(this.NAMES.dataTable);
        const name = table?.getFilter(this.NAMES.instanceName)?.value || "";
        const version = table?.getFilter(this.NAMES.instanceVersion)?.value || "";
        const filtersClear = name === "" && version === "" && !this._hasServerFilters();
        const shouldRunPolling = filtersClear && this.autoRefreshEnabled;

        this._toggleAutoRefresh(shouldRunPolling);

        const autoRefreshCheckbox = $$("autoRefreshCheckbox");
        if (autoRefreshCheckbox) {
            autoRefreshCheckbox.setValue(shouldRunPolling);
        }
    }

    _isPollingActive() {
        return pollingManager.getInfo("instanceMonitoring")?.isRunning ?? false;
    }

    _goToPage(page) {
        if (page >= 1) {
            this.pageNumber = page;
            this._loadData();
        }
    }

    _showAddDialog() {

        this._disableHotkeys();

        instanceElementView.showDialog([], (createdRecord) => {
            this._addToTable(createdRecord);
            this._enableHotkeys();
        });
    }

    _editInstance(rowId) {
        const record = $$(this.NAMES.dataTable).getItem(rowId);

        if (!record) {
            return;
        }

        this._disableHotkeys();

        instanceElementView.showDialog(
            record,
            (editedRecord) => { this._updateTable(editedRecord); },
            () => this._enableHotkeys());
    }

    _updateTable(editedRecord) {
        const table = $$(this.NAMES.dataTable);

        this._assignRowHeightsToRecords([editedRecord]);
        table.updateItem(editedRecord.id, editedRecord);
    }

    _addToTable(createdRecord) {
        const table = $$(this.NAMES.dataTable);

        this._assignRowHeightsToRecords([createdRecord]);
        table.add(createdRecord);
    }

    _formatLocalModules(localModules) {
        if (!localModules || localModules.length === 0) {
            return '<span class="instance-module-empty">Нет модулей</span>';
        }

        return localModules.map((module) => {
            const lastSyncDate = new Date(module.lastSync);
            const formattedSyncDate = this._formatDate(lastSyncDate.toISOString());
            const status = module.status === "" ? "нет данных" : module.status;
            const version = module.version === "" ? "нет данных" : module.version;

            return `<div class="instance-module-item"><strong>${module.address}</strong><br>статус: ${status} | версия: ${version} | ${formattedSyncDate}</div>`;
        }).join("");
    }

    _formatTsPiots(tsPiots) {
        if (!tsPiots || tsPiots.length === 0) {
            return '<span class="instance-module-empty">Нет модулей</span>';
        }

        return tsPiots.map((item) => {
            const name = item.name == null || String(item.name).trim() === "" ? "нет данных" : item.name;
            const address = item.address == null || String(item.address).trim() === "" ? "нет данных" : item.address;
            const version = item.version == null || String(item.version).trim() === "" ? "нет данных" : item.version;
            const licenseTill = item.licenseActiveTill ?? item.LicenseActiveTill;
            const licenseText = this._formatLicenseActiveTill(licenseTill);

            return `<div class="instance-module-item"><strong>${name}</strong><br>${address} | версия: ${version}<br>лицензия до: ${licenseText}</div>`;
        }).join("");
    }

    _formatLicenseActiveTill(licenseActiveTill) {
        if (!licenseActiveTill) {
            return "нет данных";
        }

        const date = new Date(licenseActiveTill);
        if (isNaN(date.getTime())) {
            return "нет данных";
        }

        const day = date.getDate().toString().padStart(2, "0");
        const month = (date.getMonth() + 1).toString().padStart(2, "0");
        const year = date.getFullYear().toString().slice(-2);
        const formattedDate = `${day}.${month}.${year}`;

        const now = new Date();
        now.setHours(0, 0, 0, 0);
        const licenseDate = new Date(date);
        licenseDate.setHours(0, 0, 0, 0);

        if (licenseDate < now) {
            return `<span style="color: red; font-weight: bold;">${formattedDate}</span>`;
        }

        const diffInDays = (licenseDate - now) / (1000 * 60 * 60 * 24);
        if (diffInDays <= 30) {
            return `<span style="color: #ffc107; font-weight: bold;">${formattedDate}</span>`;
        }

        return formattedDate;
    }

    _scheduleRowHeightAdjust() {
        if (this._rowHeightAdjustTimer) {
            clearTimeout(this._rowHeightAdjustTimer);
        }

        this._rowHeightAdjustTimer = setTimeout(() => {
            this._rowHeightAdjustTimer = null;
            this._applyRowHeights(true);
        }, 200);
    }

    _assignRowHeightsToRecords(records) {
        const table = $$(this.NAMES.dataTable);
        const minHeight = table?.config?.rowHeight || 36;

        records.forEach((record) => {
            record.$height = this._estimateRowHeight(record, minHeight);
        });
    }

    _estimateRowHeight(record, minHeight) {
        const localCount = record.localModules?.length ?? 0;
        const tsCount = record.TsPiots?.length ?? 0;
        const blocks = Math.max(localCount || 1, tsCount || 1);
        const blockHeight = 68;
        const cellPadding = 16;

        return Math.max(minHeight, blocks * blockHeight + cellPadding);
    }

    _applyRowHeights(preserveState) {
        const table = $$(this.NAMES.dataTable);
        if (!table || !table.count()) {
            return;
        }

        const selectedId = preserveState ? table.getSelectedId() : null;
        const scroll = preserveState ? table.getScrollState() : null;
        const minHeight = table.config.rowHeight || 36;

        table.eachRow((rowId) => {
            const record = table.getItem(rowId);
            record.$height = this._estimateRowHeight(record, minHeight);
        });

        table.refresh();

        if (scroll) {
            table.scrollTo(scroll.x, scroll.y);
        }

        if (selectedId && table.exists(selectedId)) {
            table.select(selectedId);
        }
    }

    _statusColor(status) {
        switch (status) {
            case 'ready': return '#28a745';
            case 'error': return '#dc3545';
            case 'warning': return '#ffc107';
            default: return '#6c757d';
        }
    }

    _formatDate(dateString) {
        if (!dateString) return '';

        const date = new Date(dateString);
        if (isNaN(date.getTime())) return dateString;

        const day = date.getDate().toString().padStart(2, '0');
        const month = (date.getMonth() + 1).toString().padStart(2, '0');
        const year = date.getFullYear().toString().slice(-2);
        const hours = date.getHours().toString().padStart(2, '0');
        const minutes = date.getMinutes().toString().padStart(2, '0');

        const now = new Date();
        const diffInHours = (now - date) / (1000 * 60 * 60);

        const formattedDate = `${day}.${month}.${year} ${hours}:${minutes}`;

        if (diffInHours > 24) {
            return `<span style="color: red; font-weight: bold;">${formattedDate}</span>`;
        }

        return formattedDate;
    }

    _toggleAutoRefresh(enabled) {
        if (enabled === this._isPollingActive()) {
            return;
        }

        if (enabled) {
            this._startAutoRefresh();
        } else {
            this._stopAutoRefresh();
        }

        this._saveSettings();
    }

    _changePageSize(newSize) {
        this.pageSize = newSize;
        this.pageNumber = 1;
        this._loadData();
        this._saveSettings();
    }

    _updateRefreshInterval() {
        const input = $$("refreshIntervalInput");
        const seconds = parseInt(input.getValue()) || 60;

        if (seconds < 10) {
            webix.message({
                text: "Минимальный интервал: 10 секунд",
                type: "warning"
            });
            input.setValue(10);
            this.refreshInterval = 10;
        } else {
            this.refreshInterval = seconds;
        }

        const checkbox = $$("autoRefreshCheckbox");
        if (checkbox.getValue()) {
            this._stopAutoRefresh();
            this._startAutoRefresh();
        }

        this._saveSettings();
    }

    _startAutoRefresh() {
        const intervalMs = this.refreshInterval * 1000;
        const info = pollingManager.getInfo("instanceMonitoring");

        if (info?.isRunning && info.interval === intervalMs) {
            return;
        }

        pollingManager.register(
            "instanceMonitoring",
            () => this._loadData(),
            intervalMs,
            {
                autoStart: true,
                initialDelay: intervalMs
            }
        );
    }

    _stopAutoRefresh() {
        if (!pollingManager.getInfo("instanceMonitoring")) {
            return;
        }

        pollingManager.unregister("instanceMonitoring");
    }

    _disableHotkeys() {
        if (this._deleteHotkeyHandler) {
            webix.UIManager.removeHotKey("delete", this._deleteHotkeyHandler);
            this._deleteHotkeyHandler = null;
        }

        this.hotkeys.forEach(({ key }) => {
            if (key === "delete") {
                return;
            }
            webix.UIManager.removeHotKey(key, null);
        });
    }

    _registerDeleteHotkey() {
        const table = $$(this.NAMES.dataTable);

        this._deleteHotkeyHandler = (view, e) => {
            const tag = e?.target?.tagName;
            if (tag === "INPUT" || tag === "SELECT" || tag === "TEXTAREA") {
                return;
            }
            if (webix.UIManager.getFocus() !== table) {
                return;
            }

            const button = $$(this.NAMES.deleteBtn);
            if (button?.isVisible()) {
                button.config.click();
            }
        };

        webix.UIManager.addHotKey("delete", this._deleteHotkeyHandler);
    }

    _enableHotkeys() {
        this._registerDeleteHotkey();

        this.hotkeys.forEach(({ key, buttonId }) => {
            if (key === "delete") {
                return;
            }
            const button = $$(buttonId);
            if (button) {
                button.define({ hotkey: key });
            }
        });
    }

    _getSortedInstancesForExport() {
        const table = $$(this.NAMES.dataTable);
        const rows = table.serialize();

        const mapped = rows.map(x => ({
            name: x.name,
            token: x.id,
            address: x.address,
        }));

        return mapped.sort((a, b) =>
            a.name.localeCompare(b.name, "ru", { sensitivity: "base" })
        );
    }

    _printInstances() {
        const table = $$(this.NAMES.dataTable);
        table.showColumn(this.NAMES.id);
        table.hideColumn(this.NAMES.instanceUpdatedAt);

        webix.toPDF(table, { autowidth: true, filterHTML: true });

        table.hideColumn(this.NAMES.id);
        table.showColumn(this.NAMES.instanceUpdatedAt);
    }

    _exportInstancesCsv() {
        const data = this._getSortedInstancesForExport();

        const exportId = "instancesExportTable";
        if ($$(exportId)) {
            $$(exportId).destructor();
        }

        webix.ui({
            view: "datatable",
            id: exportId,
            data,
            columns: [
                { id: "name", header: "Название" },
                { id: "token", header: "Токен" },
                { id: "address", header: "Адрес" }
            ]
        });

        webix.toCSV($$(exportId), {
            filename: "instances",
            name: "Инстансы"
        });

        $$(exportId).destructor();
    }
}

export default async function createInstanceListView(id) {
    const view = new InstanceListView(id)
        .delayedDataLoading()
        .render();

    return view;
}