import api from '../../services/alertTemplatesService.js';
import elementView from './alertTemplateElementView.js';

class AlertTemplatesListView {
    constructor(id) {
        this.id = id;
        this.pageSize = 50;
        this.pageNumber = 1;

        this.LABELS = {
            refresh: "Обновить",
            add: "Добавить",
            delete: "Удалить",
            name: "Имя",
            enabled: "Включён",
            schedule: "Расписание",
            errorLoad: "Ошибка при загрузке данных",
            errorDelete: "Ошибка при удалении записи",
            page: "Страница",
            prevButton: "←",
            nextButton: "→"
        };

        this.NAMES = {
            refreshBtn: "alertTplRefreshBtn",
            addBtn: "alertTplAddBtn",
            deleteBtn: "alertTplDeleteBtn",
            dataTable: "alertTplDataTable",
            prevButton: "alertTplPrevButton",
            nextButton: "alertTplNextButton",
            paginationInfo: "alertTplPaginationInfo",
            formId: "alertTplListForm"
        };
    }

    delayedDataLoading() {
        setTimeout(() => {
            this._loadData();
        }, 10);
        return this;
    }

    render() {
        return {
            id: this.id,
            rows: [
                {
                    view: "form",
                    id: this.NAMES.formId,
                    elements: [
                        this._toolbar(),
                        this._dataTable()
                    ]
                }
            ]
        };
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
                    click: () => this._delete()
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
                }
            ]
        };
    }

    _dataTable() {
        return {
            view: "datatable",
            id: this.NAMES.dataTable,
            columns: [
                {
                    id: "name",
                    header: this.LABELS.name,
                    fillspace: true,
                    sort: "string"
                },
                {
                    id: "enabled",
                    header: this.LABELS.enabled,
                    width: 120,
                    template: (obj) => obj.enabled ? "Да" : "Нет"
                },
                {
                    id: "schedule",
                    header: this.LABELS.schedule,
                    width: 220,
                    template: (obj) => formatScheduler(obj.scheduler)
                }
            ],
            select: "row",
            multiselect: false,
            on: {
                onItemDblClick: (cell) => this._edit(cell.row)
            }
        };
    }

    async _loadData() {
        try {
            const data = await api.list(this.pageNumber, this.pageSize);

            if (!data.content) {
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

            if (data.content.length > 0) {
                table.select(data.content[0].id);
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

    _showAddDialog() {
        elementView.showDialog({}, (created) => {
            $$(this.NAMES.dataTable).add(created);
        });
    }

    _edit(rowId) {
        const record = $$(this.NAMES.dataTable).getItem(rowId);
        if (!record) {
            return;
        }

        elementView.showDialog(record, (edited) => {
            $$(this.NAMES.dataTable).updateItem(edited.id, {
                ...record,
                ...edited
            });
        });
    }

    async _delete() {
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
            text: "Вы собираетесь удалить шаблон оповещения?",
            ok: "Да",
            cancel: "Нет"
        }).then(async () => {
            try {
                await api.delete(recordId);
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
            paginationInfo.setValue(`${data.currentPage} из ${data.totalPages}`);
        }
    }

    _goToPage(page) {
        if (page >= 1) {
            this.pageNumber = page;
            this._loadData();
        }
    }
}

function formatScheduler(scheduler) {
    if (!Array.isArray(scheduler) || scheduler.length === 0) {
        return "";
    }

    return scheduler
        .map((slot) => String(slot.time || "").slice(0, 5))
        .filter(Boolean)
        .join(", ");
}

export function createAlertTemplatesTab(containerId) {
    const listView = new AlertTemplatesListView(containerId);
    const ui = listView.render();
    listView.delayedDataLoading();
    return ui;
}
