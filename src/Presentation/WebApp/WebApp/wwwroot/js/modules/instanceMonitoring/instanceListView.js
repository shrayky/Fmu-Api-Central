import instanceMonitoringService from '../../services/instanceMonitoringService.js';
import instanceElementView from './instanceElementView.js';

const style = document.createElement('style');

style.textContent = `
    .multiline_datatable .webix_cell {
        white-space: normal !important;
        vertical-align: top !important;
        line-height: normal !important;
    }
`;

document.head.appendChild(style);

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
            instanceUpdatedAt: "Обновлен",
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
            instanceToken: "id",
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
                this._dataTable()
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
                    hotkey: "insert"
                },
                {
                    view: "button",
                    id: this.NAMES.deleteBtn,
                    value: this.LABELS.delete,
                    width: 100,
                    click: () => this._deleteInstance(),
                    hotkey: "delete"
                },
                {
                    view: "button",
                    id: this.NAMES.refreshBtn,
                    value: this.LABELS.refresh,
                    width: 100,
                    click: () => this._loadData(),
                    hotkey: "f5"
                },
                {},
                {
                    view: "button",
                    id: this.NAMES.prevButton,
                    value: this.LABELS.prevButton,
                    width: 50,
                    disabled: true,
                    click: () => this._goToPage(this.pageNumber - 1),
                    hotkey: "ctrl+left"
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
                    hotkey: "ctrl+right"
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
                    header: this.LABELS.instanceName,
                    fillspace: true
                },
                { 
                    id: this.NAMES.instanceVersion,
                    header: this.LABELS.instanceVersion,
                    width: 100 
                },
                {
                    id: "localModules",
                    header: "Локальные модули",
                    fillspace: true,
                    template: (obj) => this._formatLocalModules(obj.localModules)
                },
                { 
                    id: this.NAMES.instanceUpdatedAt, 
                    header: this.LABELS.instanceUpdatedAt, 
                    width: 120,
                    template: (obj) => this._formatDate(obj.lastUpdated)
                },
            ],
            select: "row",
            multiselect: false,
            rowHeight: 120,
            css: "multiline_datatable",
            on: {
                onItemDblClick: (rowId) => this._editInstance(rowId),
            }
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

            if (!data.listEnabled) {
                webix.message({
                    text: data.description,
                    type: "error"
                });
                return;
            }

            const table = $$(this.NAMES.dataTable);
            table.clearAll();
            table.parse(data.content);

            $$(this.id).enable();

            if (data.content.length > 0) {
                table.select(data.content[0].id);
                webix.UIManager.setFocus(this.NAMES.dataTable); 
            }
            
            this._updatePagination(data);
        } catch (error) {
            console.error(this.LABELS.errorLoad, error);
            webix.message({
                text: this.LABELS.errorLoad,
                type: "error"
            });
        }
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

    _showAddDialog() {
        instanceElementView.showDialog([], (createdRecord) => {
            this._addToTable(createdRecord);
        });
    }

    _editInstance(rowId) {
        const record = $$(this.NAMES.dataTable).getItem(rowId);

        if (!record) {
            return;
        }

        instanceElementView.showDialog(
            record,
            (editedRecord) => {this._updateTable(editedRecord);});
    }

    _updateTable(editedRecord) {
        const table = $$(this.NAMES.dataTable);

        table.updateItem(editedRecord.id, editedRecord);
    }

    _addToTable(createdRecord) {
        const table = $$(this.NAMES.dataTable);

        table.add(createdRecord);
    }

    _formatLocalModules(localModules) {
        if (!localModules || localModules.length === 0) {
            return 'Нет модулей';
        }

        let lm = localModules.map(module => {
            const lastSyncDate = new Date(module.lastSync);
            const formattedSyncDate = this._formatDate(lastSyncDate.toISOString());

            const status = module.status == "" ? "нет данных" : module.status;
            const version = module.version == "" ? "нет данных" : module.version;
            
            return `<strong>${module.address}</strong><br>статус: ${status} | версия: ${version} | ${formattedSyncDate}`;
        }).join('<br>');

        return lm;
    }

    _calculateRowHeight(obj) {
        const baseHeight = 50;
        const moduleHeight = 20;
        
        console.log(2);

        if (!obj.localModules || obj.localModules.length === 0) {
            return baseHeight;
        }

        console.log(3);
        
        return baseHeight + (obj.localModules.length * moduleHeight);
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
}

export default async function createInstanceListView(id) {
    const view = new InstanceListView(id)
                    .delayedDataLoading()
                    .render();

    return view;
}