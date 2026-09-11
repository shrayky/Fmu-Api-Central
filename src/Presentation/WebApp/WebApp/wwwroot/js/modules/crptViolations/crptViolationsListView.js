import crptViolationsService from '../../services/crptViolationsService.js';
import organizationService from '../../services/organizationService.js';
import {
    getDefaultFilters,
    getPresetDates,
    PERIOD_PRESETS,
    PRESET_LABELS,
} from './crptViolationsPeriodPresets.js';

class CrptViolationsListView {
    constructor(id) {
        this.id = id;
        this.pageSize = parseInt(localStorage.getItem("crptViolations_pageSize")) || 50;
        this.pageNumber = 1;
        this.filters = this._loadFilters();
        this.activePreset = this.filters.periodPreset || PERIOD_PRESETS.today;
        this.organizations = [];

        this.LABELS = {
            formTitle: "Fmu-Api-Central: Отклонения ЧЗ",
            refresh: "Обновить",
            load: "Получить",
            loadSuccess: "Отклонения загружены",
            loadError: "Ошибка загрузки отклонений",
            page: "Страница",
            prevButton: "←",
            nextButton: "→",
            allOrganizations: "Все организации",
            violationsKpi: "Отклонения",
            penaltyKpi: "Штрафы",
            date: "Дата",
            organization: "Организация",
            productGroup: "Группа",
            region: "Регион",
            violationType: "Тип отклонения",
            count: "Количество",
            errorLoad: "Ошибка при загрузке данных",
        };

        this.NAMES = {
            formId: "crptViolationsListViewForm",
            refreshBtn: "crptViolationsRefreshBtn",
            loadBtn: "crptViolationsLoadBtn",
            organizationCombo: "crptViolationsOrganizationCombo",
            periodToolbar: "crptViolationsPeriodToolbar",
            violationsKpi: "crptViolationsKpiCount",
            penaltyKpi: "crptViolationsKpiPenalty",
            dataTable: "crptViolationsDataTable",
            prevButton: "crptViolationsPrevButton",
            nextButton: "crptViolationsNextButton",
            paginationInfo: "crptViolationsPaginationInfo",
        };
    }

    _loadFilters() {
        try {
            const stored = JSON.parse(localStorage.getItem("crptViolations_filters") || "null");
            if (stored && typeof stored === "object") {
                return stored;
            }
        } catch {
            // ignore
        }

        return getDefaultFilters();
    }

    _saveFilters() {
        localStorage.setItem("crptViolations_filters", JSON.stringify(this.filters));
    }

    delayedDataLoading() {
        setTimeout(() => {
            this._loadData();
        }, 10);

        return this;
    }

    render() {
        $$("toolbarLabel").setValue(this.LABELS.formTitle);

        return {
            id: this.id,
            disabled: true,
            rows: [{
                view: "form",
                id: this.NAMES.formId,
                elements: [
                    this._toolbar(),
                    this._periodToolbar(),
                    this._kpi(),
                    this._dataTable(),
                    this._footer(),
                ]
            }]
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
                    id: this.NAMES.loadBtn,
                    value: this.LABELS.load,
                    width: 110,
                    click: () => this._loadFromCrpt(),
                },
                {
                    view: "richselect",
                    id: this.NAMES.organizationCombo,
                    width: 360,
                    value: this.filters.inn || "__all__",
                    options: [{ id: "__all__", value: this.LABELS.allOrganizations }],
                    on: {
                        onChange: (inn) => this._applyOrganization(inn)
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
                id: `crptViolationsPreset_${preset}`,
                value: PRESET_LABELS[preset],
                width: 100,
                css: this.activePreset === preset ? "webix_primary" : "webix_secondary",
                click: () => this._applyPeriodPreset(preset),
            }))
        };
    }

    _kpi() {
        return {
            view: "toolbar",
            elements: [
                {
                    view: "label",
                    id: this.NAMES.violationsKpi,
                    label: `${this.LABELS.violationsKpi}: 0`,
                    width: 220
                },
                {
                    view: "label",
                    id: this.NAMES.penaltyKpi,
                    label: `${this.LABELS.penaltyKpi}: 0 ₽`,
                    width: 280
                },
                {}
            ]
        };
    }

    _dataTable() {
        return {
            view: "datatable",
            id: this.NAMES.dataTable,
            columns: [
                { id: "date", header: [this.LABELS.date], width: 110, sort: "string" },
                { id: "organizationName", header: [this.LABELS.organization], fillspace: true, sort: "string" },
                { id: "productGroupName", header: [this.LABELS.productGroup], width: 220, sort: "string" },
                { id: "region", header: [this.LABELS.region], width: 180, sort: "string" },
                { id: "violationResultName", header: [this.LABELS.violationType], fillspace: true, sort: "string" },
                { id: "violationNumber", header: [this.LABELS.count], width: 120, sort: "int" },
            ],
            select: "row",
            multiselect: false
        };
    }

    _footer() {
        return {
            view: "toolbar",
            borderless: true,
            elements: [
                {
                    view: "label",
                    label: "Элементов на странице:",
                    width: 210
                },
                {
                    view: "select",
                    value: this.pageSize,
                    width: 80,
                    options: [
                        { id: 25, value: "25" },
                        { id: 50, value: "50" },
                        { id: 100, value: "100" },
                        { id: 200, value: "200" },
                    ],
                    on: {
                        onChange: (newValue) => this._changePageSize(newValue)
                    }
                },
            ]
        };
    }

    _applyPeriodPreset(preset) {
        this.activePreset = preset;
        this.filters = {
            ...this.filters,
            ...getPresetDates(preset),
            periodPreset: preset,
        };
        this.pageNumber = 1;
        this._saveFilters();
        this._updatePresetButtons();
        this._loadData();
    }

    async _loadFromCrpt() {
        const button = $$(this.NAMES.loadBtn);
        if (button) {
            button.disable();
        }

        try {
            await crptViolationsService.load(this.filters.inn);
            webix.message({
                text: this.LABELS.loadSuccess,
                type: "success"
            });
            await this._loadData();
        } catch (error) {
            console.error(this.LABELS.loadError, error);
            webix.message({
                text: error.message || this.LABELS.loadError,
                type: "error"
            });
        }

        if (button) {
            button.enable();
        }
    }

    _applyOrganization(inn) {
        this.filters = {
            ...this.filters,
            inn: !inn || inn === "__all__" ? "" : inn,
        };
        this.pageNumber = 1;
        this._saveFilters();
        this._loadData();
    }

    _updatePresetButtons() {
        [PERIOD_PRESETS.today, PERIOD_PRESETS.yesterday, PERIOD_PRESETS.week, PERIOD_PRESETS.month]
            .forEach((preset) => {
                const button = $$(`crptViolationsPreset_${preset}`);
                if (!button?.getNode) {
                    return;
                }

                const node = button.getNode();
                node.classList.remove("webix_primary", "webix_secondary");
                node.classList.add(this.activePreset === preset ? "webix_primary" : "webix_secondary");
            });
    }

    async _loadData() {
        try {
            await this._loadOrganizations();

            const data = await crptViolationsService.list(
                this.pageNumber,
                this.pageSize,
                this.filters
            );

            if (!data.listEnabled) {
                webix.message({
                    text: data.description,
                    type: "error"
                });
                $$(this.id).enable();
                return;
            }

            const table = $$(this.NAMES.dataTable);
            table.clearAll();
            table.parse(data.content || []);
            $$(this.id).enable();

            this._updateKpi(data);
            this._updatePagination(data);
        } catch (error) {
            console.error(this.LABELS.errorLoad, error);
            webix.message({
                text: this.LABELS.errorLoad,
                type: "error"
            });
            $$(this.id).enable();
        }
    }

    async _loadOrganizations() {
        const data = await organizationService.list(1, 500);
        this.organizations = data.content || [];

        const combo = $$(this.NAMES.organizationCombo);
        if (!combo) {
            return;
        }

        combo.blockEvent();
        combo.define("options", [
            { id: "__all__", value: this.LABELS.allOrganizations },
            ...this.organizations.map((item) => ({
                id: item.inn,
                value: `${item.name} (${item.inn})`
            }))
        ]);
        combo.refresh();
        combo.setValue(this.filters.inn || "__all__");
        combo.unblockEvent();
    }

    _updateKpi(data) {
        const violations = $$(this.NAMES.violationsKpi);
        const penalty = $$(this.NAMES.penaltyKpi);

        if (violations) {
            violations.setValue(`${this.LABELS.violationsKpi}: ${data.totalViolations || 0}`);
        }

        if (penalty) {
            const amount = Number(data.totalPenaltyAmountRub || 0).toLocaleString("ru-RU");
            penalty.setValue(`${this.LABELS.penaltyKpi}: ${amount} ₽`);
        }
    }

    _updatePagination(data) {
        const prevButton = $$(this.NAMES.prevButton);
        const nextButton = $$(this.NAMES.nextButton);
        const paginationInfo = $$(this.NAMES.paginationInfo);
        const totalPages = data.totalPages == 0 ? 1 : data.totalPages;

        if (prevButton) {
            prevButton.enable();
            if (data.currentPage <= 1) {
                prevButton.disable();
            }
        }

        if (nextButton) {
            nextButton.enable();
            if (data.currentPage >= totalPages) {
                nextButton.disable();
            }
        }

        if (paginationInfo) {
            paginationInfo.setValue(`${data.currentPage} из ${totalPages}`);
        }

        this.pageNumber = data.currentPage || this.pageNumber;
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
        localStorage.setItem("crptViolations_pageSize", this.pageSize.toString());
        this._loadData();
    }
}

export default async function createCrptViolationsListView(id) {
    const view = new CrptViolationsListView(id)
        .delayedDataLoading()
        .render();

    return view;
}
