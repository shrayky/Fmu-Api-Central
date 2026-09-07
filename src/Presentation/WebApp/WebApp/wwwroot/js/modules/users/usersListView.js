import usersService from '../../services/usersService.js';
import userElementView from './userElementView.js';

class UsersListView {
    constructor(id) {
        this.id = id;
        this.pageSize = 50;
        this.pageNumber = 1;

        this.LABELS = {
            formTitle: "Fmu-Api-Central: Пользователи",
            refresh: "Обновить",
            add: "Добавить",
            delete: "Удалить",
            name: "Имя",
            errorLoad: "Ошибка при загрузке данных",
            errorDelete: "Ошибка при удалении записи",
            lastUserDelete: "Нельзя удалить последнего пользователя",
            page: "Страница",
            prevButton: "←",
            nextButton: "→"
        };

        this.NAMES = {
            toolbarLabel: "toolbarLabel",
            refreshBtn: "usersRefreshBtn",
            addBtn: "usersAddBtn",
            deleteBtn: "usersDeleteBtn",
            dataTable: "usersDataTable",
            prevButton: "usersPrevButton",
            nextButton: "usersNextButton",
            paginationInfo: "usersPaginationInfo",
            formId: "usersListViewForm"
        };

        this.hotkeys = [
            { key: "insert", buttonId: this.NAMES.addBtn },
            { key: "delete", buttonId: this.NAMES.deleteBtn },
            { key: "f5", buttonId: this.NAMES.refreshBtn },
            { key: "ctrl+left", buttonId: this.NAMES.prevButton },
            { key: "ctrl+right", buttonId: this.NAMES.nextButton }
        ];
    }

    delayedDataLoading() {
        setTimeout(() => {
            this._loadData();
        }, 10);
        return this;
    }

    render() {
        $$(this.NAMES.toolbarLabel).setValue(this.LABELS.formTitle);

        return {
            id: this.id,
            disabled: true,
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
                    click: () => this._showAddDialog(),
                    hotkey: "insert"
                },
                {
                    view: "button",
                    id: this.NAMES.deleteBtn,
                    value: this.LABELS.delete,
                    width: 100,
                    click: () => this._delete(),
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
                }
            ]
        };
    }

    _dataTable() {
        return {
            view: "datatable",
            id: this.NAMES.dataTable,
            columns: [
                { id: "name", header: this.LABELS.name, fillspace: true }
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
            const data = await usersService.list(this.pageNumber, this.pageSize);

            if (!data.content) {
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
        this._disableHotkeys();
        userElementView.showDialog(
            {},
            () => this._loadData(),
            () => this._enableHotkeys()
        );
    }

    _edit(rowId) {
        const record = $$(this.NAMES.dataTable).getItem(rowId);
        if (!record) {
            return;
        }

        this._disableHotkeys();
        userElementView.showDialog(
            record,
            () => this._loadData(),
            () => this._enableHotkeys()
        );
    }

    async _delete() {
        const selectedId = $$(this.NAMES.dataTable).getSelectedId();
        let recordId;

        if (typeof selectedId === "object" && selectedId !== null) {
            recordId = selectedId.id || selectedId.Id || null;
        } else {
            recordId = selectedId;
        }

        if (!recordId) {
            webix.message({
                text: "Выберите запись для удаления",
                type: "error"
            });
            return;
        }

        const record = $$(this.NAMES.dataTable).getItem(recordId);
        if (record && record.isLastUser) {
            webix.message({
                text: this.LABELS.lastUserDelete,
                type: "error"
            });
            return;
        }

        webix.confirm({
            title: "Вы уверены?",
            text: "Вы собираетесь удалить пользователя?",
            ok: "Да",
            cancel: "Нет"
        }).then(async () => {
            try {
                await usersService.delete(recordId);
                webix.message("Запись удалена успешно");
                await this._loadData();
            } catch (error) {
                console.error(this.LABELS.errorDelete, error);
                webix.message({
                    text: error.message || this.LABELS.errorDelete,
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

export default async function createUsersListView(id) {
    const view = new UsersListView(id)
        .delayedDataLoading()
        .render();

    return view;
}
