import instanceMonitoringService from '../../services/instanceMonitoringService.js';
import instanceElementView from './instanceElementView.js';

class InstanceListView {
    constructor(id) {
        this.formName = "SoftwareUpdatesView";
        this.id = id;
        this.pageSize = 50;
        this.pageNumber = 1;

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
            instanceUpdatedAt: "Дата обновления",
            instanceVersion: "Версия",
            errorLoad: "Ошибка при загрузке данных"
        };

        this.NAMES = {
            toolbarLabel: "toolbarLabel",
            refreshBtn: "refreshBtn",
            addBtn: "addBtn",
            editBtn: "editBtn",
            deleteBtn: "deleteBtn",
            prevButton: "prevButton",
            nextButton: "nextButton",
            paginationInfo: "paginationInfo",
            dataTable: "dataTable",
            instanceName: "name",
            instanceVersion: "version",
            instanceUpdatedAt: "lastUpdated",
            instanceToken: "token",
        };
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
            elements: [
                this._toolbar(),
                this._dataTable(),
                {}
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
                    click: () => this._showAddDialog()
                },
                {
                    view: "button",
                    id: this.NAMES.deleteBtn,
                    value: this.LABELS.delete,
                    width: 100,
                    click: () => this._deleteInstance()
                },
                {
                    view: "button",
                    id: this.NAMES.refreshBtn,
                    value: this.LABELS.refresh,
                    width: 100,
                    click: () => this._loadData()
                },
                {},
                {
                    view: "button",
                    id: this.NAMES.prevButton,
                    value: this.LABELS.prevButton,
                    width: 50,
                    disabled: true,
                    click: () => this._goToPage(this.pageNumber - 1)
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
                    click: () => this._goToPage(this.pageNumber + 1)
                },
            ]
        };
    }

    _dataTable() {
        return {
            view: "datatable",
            id: this.NAMES.dataTable,
            columns: [
                { id: this.NAMES.instanceName, header: this.LABELS.instanceName, fillspace: true },
                { id: this.NAMES.instanceVersion, header: this.LABELS.instanceVersion, width: 100 },
                { id: this.NAMES.instanceUpdatedAt, header: this.LABELS.instanceUpdatedAt, width: 100 },
                { id: this.NAMES.instanceToken, header: this.LABELS.instanceToken, width: 100 },
            ],
            select: "row",
            multiselect: false,
            autoheight: true,
        };
    }

    async _loadData() {
        try {
            const data = await instanceMonitoringService.list(this.pageNumber, this.pageSize);

            if (!data.content) {
                console.warn("no data");
                $$(this.id).enable();
                return;
            }

            const table = $$(this.NAMES.dataTable);
            table.clearAll();
            table.parse(data.Content);
            $$(this.id).enable();

            this._updatePagination(data);

        } catch (error) {
            console.error(this.LABELS.errorLoad, error);
            webix.message({
                text: this.LABELS.errorLoad,
                type: "error"
            });
        }
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

    _goToPage(page) {
        if (page >= 1) {
            this.pageNumber = page;
            this._loadData();
        }
    }

    _showAddDialog() {
        instanceElementView.showDialog((createdRecord, formData, file_obj) => {
            this._addToTable(createdRecord, formData, file_obj);
        });
    }
}

export default async function createInstanceListView(id) {
    const view = new InstanceListView(id)
                    .delayedDataLoading()
                    .render();

    return view;
}