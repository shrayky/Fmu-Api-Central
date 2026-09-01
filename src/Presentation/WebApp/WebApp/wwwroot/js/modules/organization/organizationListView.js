import organizationService from '../../services/organizationService.js';
import organizationElementView from './organizationElementView.js';

class OrganizationListView {
    constructor(id) {
        this.id = id;
        this.pageSize = 50;
        this.pageNumber = 1;

        this.LABELS = {
            formTitle: "Fmu-Api-Central: Организации",
            refresh: "Обновить",
            add: "Добавить",
            delete: "Удалить",
            name: "Наименование",
            inn: "ИНН",
            token: "Токен",
            gisMt: "ГИС МТ",
            gisMtEmpty: "—",
            actions: "Действия",
            loadProductGroups: "Получить товарные группы",
            loadDocuments: "Загрузить документы",
            loadStock: "Загрузить остатки",
            selectOrganization: "Выберите организацию",
            operationAccepted: "Задание принято. Обновите список через несколько секунд.",
            tokenMissing: "Токен не получен",
            tokenReceived: "Получен, до",
            tokenCopyHint: "Копировать токен",
            tokenCopied: "Токен скопирован в буфер обмена",
            tokenCopyError: "Ошибка при копировании токена",
            trueApiOn: "True API включена",
            trueApiOff: "True API отключена",
            errorLoad: "Ошибка при загрузке данных",
            errorDelete: "Ошибка при удалении записи",
            page: "Страница",
            prevButton: "←",
            nextButton: "→"
        };

        this.NAMES = {
            toolbarLabel: "toolbarLabel",
            refreshBtn: "organizationRefreshBtn",
            actionsMenu: "organizationActionsMenu",
            addBtn: "organizationAddBtn",
            deleteBtn: "organizationDeleteBtn",
            dataTable: "organizationDataTable",
            prevButton: "organizationPrevButton",
            nextButton: "organizationNextButton",
            paginationInfo: "organizationPaginationInfo",
            formId: "organizationListViewForm"
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
                this._actionsMenu(),
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

    /** Меню ручных операций ГИС МТ — как у групп инстансов. */
    _actionsMenu() {
        return {
            view: "menu",
            id: this.NAMES.actionsMenu,
            autowidth: true,
            data: [
                {
                    id: "actions",
                    value: this.LABELS.actions,
                    submenu: [
                        { id: "actions:product-groups", value: this.LABELS.loadProductGroups },
                        { id: "actions:documents", value: this.LABELS.loadDocuments },
                        { id: "actions:stock", value: this.LABELS.loadStock }
                    ]
                }
            ],
            on: {
                onMenuItemClick: (id) => {
                    if (id === "actions:product-groups") {
                        this._enqueueGisMt("product-groups");
                    }
                    if (id === "actions:documents") {
                        this._enqueueGisMt("documents");
                    }
                    if (id === "actions:stock") {
                        this._enqueueGisMt("stock");
                    }
                }
            }
        };
    }

    /** Идентификатор выделенной строки списка организаций. */
    _getSelectedOrganizationId() {
        const selectedId = $$(this.NAMES.dataTable).getSelectedId();
        if (!selectedId) {
            return "";
        }

        if (typeof selectedId === "object") {
            return selectedId.id || selectedId.Id || "";
        }

        return selectedId;
    }

    /** Отправляет операцию ГИС МТ; без строки или токена запрос не уходит. */
    async _enqueueGisMt(operation) {
        const organizationId = this._getSelectedOrganizationId();
        if (!organizationId) {
            webix.message({
                text: this.LABELS.selectOrganization,
                type: "error"
            });
            return;
        }

        const record = $$(this.NAMES.dataTable).getItem(organizationId);
        if (!record?.trueApiTokenReceived) {
            webix.message({
                text: this.LABELS.tokenMissing,
                type: "error"
            });
            return;
        }

        try {
            await organizationService.enqueueGisMt(organizationId, operation);
            webix.message(this.LABELS.operationAccepted);
        } catch (error) {
            webix.message({
                text: error.message || this.LABELS.errorLoad,
                type: "error"
            });
        }
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
                    template: (obj) => this._formatName(obj)
                },
                {
                    id: "gisMtStatus",
                    header: this.LABELS.gisMt,
                    width: 280,
                    template: (obj) => this._formatGisMtStatus(obj)
                },
                { id: "inn", header: this.LABELS.inn, width: 180 },
                {
                    id: "tokenStatus",
                    header: this.LABELS.token,
                    width: 240,
                    template: (obj) => this._formatToken(obj)
                }
            ],
            select: "row",
            multiselect: false,
            on: {
                onItemClick: (cell) => {
                    if (cell.column === "tokenStatus") {
                        this._copyToken(cell.row);
                    }
                },
                onItemDblClick: (cell) => this._edit(cell.row)
            }
        };
    }

    _formatGisMtStatus(obj) {
        const status = obj.gisMtLastStatus || {};
        const code = status.code;
        if (code === null || code === undefined || code === "") {
            return this.LABELS.gisMtEmpty;
        }

        const numeric = Number(code);
        const ok = numeric >= 200 && numeric < 300;
        const description = ok ? "" : (status.description || "");
        const text = `${code} ${description}`.trim()
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");
        const color = ok ? "" : "color:#E74C3C;";
        return `<span style="${color}">${text}</span>`;
    }

    _formatToken(obj) {
        if (!obj.trueApiTokenReceived) {
            return `<span style="color:#95A5A6;cursor:pointer" title="${this.LABELS.tokenMissing}">${this.LABELS.tokenMissing}</span>`;
        }

        const expired = obj.trueApiTokenExpired ? new Date(obj.trueApiTokenExpired) : null;
        const until = expired && !Number.isNaN(expired.getTime())
            ? expired.toLocaleString("ru-RU", {
                day: "2-digit",
                month: "2-digit",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit"
            })
            : "";
        const text = until
            ? `${this.LABELS.tokenReceived} ${until}`
            : this.LABELS.tokenReceived;

        return `<span style="cursor:pointer" title="${this.LABELS.tokenCopyHint}">${text}</span>`;
    }

    async _copyToken(rowId) {
        const record = $$(this.NAMES.dataTable).getItem(rowId);
        if (!record?.trueApiTokenReceived) {
            webix.message({
                text: this.LABELS.tokenMissing,
                type: "error"
            });
            return;
        }

        try {
            const data = await organizationService.getToken(record.inn);
            const token = data.token ?? data.Token ?? "";
            if (!token) {
                webix.message({
                    text: this.LABELS.tokenMissing,
                    type: "error"
                });
                return;
            }

            await navigator.clipboard.writeText(token);
            webix.message(this.LABELS.tokenCopied);
        } catch (error) {
            webix.message({
                text: error.message || this.LABELS.tokenCopyError,
                type: "error"
            });
        }
    }

    _formatName(obj) {
        const enabled = !!obj.trueApiEnabled;
        const color = enabled ? "#2ECC71" : "#E74C3C";
        const title = enabled ? this.LABELS.trueApiOn : this.LABELS.trueApiOff;
        const name = String(obj.name || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");

        return `<span style="display:inline-flex;align-items:center;gap:8px;" title="${title}">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 -960 960 960" width="18" height="18" style="flex-shrink:0">
                <path fill="${color}" d="M160-120v-170l527-526q12-12 27-18t30-6q16 0 30.5 6t25.5 18l56 56q12 11 18 25.5t6 30.5q0 15-6 30t-18 27L330-120H160Zm80-80h56l393-392-28-29-29-28-392 393v56Zm560-503-57-57 57 57Zm-139 82-29-28 57 57-28-29ZM560-120q74 0 137-37t63-103q0-36-19-62t-51-45l-59 59q23 10 36 22t13 26q0 23-36.5 41.5T560-200q-17 0-28.5 11.5T520-160q0 17 11.5 28.5T560-120ZM183-426l60-60q-20-8-31.5-16.5T200-520q0-12 18-24t76-37q88-38 117-69t29-70q0-55-44-87.5T280-840q-45 0-80.5 16T145-785q-11 13-9 29t15 26q13 11 29 9t27-13q14-14 31-20t42-6q41 0 60.5 12t19.5 28q0 14-17.5 25.5T262-654q-80 35-111 63.5T120-520q0 32 17 54.5t46 39.5Z"/>
            </svg>
            ${name}
        </span>`;
    }

    async _loadData() {
        try {
            const data = await organizationService.list(this.pageNumber, this.pageSize);

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
        organizationElementView.showDialog(
            {},
            (created) => {
                $$(this.NAMES.dataTable).add(created);
            },
            () => this._enableHotkeys()
        );
    }

    _edit(rowId) {
        const record = $$(this.NAMES.dataTable).getItem(rowId);
        if (!record) {
            return;
        }

        this._disableHotkeys();
        organizationElementView.showDialog(
            record,
            (edited) => {
                $$(this.NAMES.dataTable).updateItem(edited.id, {
                    ...record,
                    ...edited
                });
            },
            () => this._enableHotkeys()
        );
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
            text: "Вы собираетесь удалить запись?",
            ok: "Да",
            cancel: "Нет"
        }).then(async () => {
            try {
                await organizationService.delete(recordId);
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

export default async function createOrganizationListView(id) {
    const view = new OrganizationListView(id)
        .delayedDataLoading()
        .render();

    return view;
}
