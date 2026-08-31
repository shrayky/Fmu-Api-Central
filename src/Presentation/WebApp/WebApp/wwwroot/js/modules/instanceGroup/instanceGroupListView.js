import instanceGroupService from '../../services/instanceGroupService.js';
import instanceGroupElementView from './instanceGroupElementView.js';
import softwareUpdatesService from '../../services/softwareUpdatesService.js';

class InstanceGroupListView {
    constructor(id) {
        this.id = id;
        this.pageSize = 50;
        this.pageNumber = 1;

        this.LABELS = {
            formTitle: "Fmu-Api-Central: Группы инстансов",
            refresh: "Обновить",
            add: "Добавить",
            delete: "Удалить",
            name: "Имя",
            autoUpdateAllowed: "Автообновление",
            instancesCount: "Инстансы",
            errorLoad: "Ошибка при загрузке данных",
            errorDelete: "Ошибка при удалении записи",
            page: "Страница",
            prevButton: "←",
            nextButton: "→",
            actions: "Действия",
            forceInstall: "Принудительная установка",
            forceInstallTitle: "Принудительная установка",
            exportSettings: "Выгрузить настройки",
            settingsSchema: "Схема настроек",
            selectGroup: "Выберите группу",
            selectedGroup: "Группа",
            selectVersion: "Версия",
            applyForce: "Установить",
            cancel: "Отмена",
            noUpdates: "Нет загруженных пакетов обновлений",
            errorLoadUpdates: "Не удалось загрузить список обновлений",
            selectVersionRequired: "Выберите версию"
        };

        this.NAMES = {
            toolbarLabel: "toolbarLabel",
            refreshBtn: "instanceGroupRefreshBtn",
            addBtn: "instanceGroupAddBtn",
            deleteBtn: "instanceGroupDeleteBtn",
            dataTable: "instanceGroupDataTable",
            prevButton: "instanceGroupPrevButton",
            nextButton: "instanceGroupNextButton",
            paginationInfo: "instanceGroupPaginationInfo",
            formId: "instanceGroupListViewForm",
            actionsMenu: "instanceGroupToolbarMenu",
            forceUpdateWindow: "instanceGroupForceUpdateWindow",
            forceUpdateForm: "instanceGroupForceUpdateForm"
        };
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
                        { id: "actions:force-update", value: this.LABELS.forceInstall },
                        { id: "actions:export-settings", value: this.LABELS.exportSettings }
                    ]
                }
            ],
            on: {
                onMenuItemClick: (id) => {
                    if (id === "actions:force-update") {
                        this._showForceUpdateDialog();
                    }

                    if (id === "actions:export-settings") {
                        this._exportSettings();
                    }
                }
            }
        };
    }

    _getSelectedGroupId() {
        const selectedId = $$(this.NAMES.dataTable).getSelectedId();
        if (!selectedId) {
            return "";
        }

        if (typeof selectedId === "object") {
            return selectedId.id || selectedId.Id || "";
        }

        return selectedId;
    }

    async _exportSettings() {
        const groupId = this._getSelectedGroupId();
        if (!groupId) {
            webix.message({
                text: this.LABELS.selectGroup,
                type: "error"
            });
            return;
        }

        try {
            const result = await instanceGroupService.exportSettings([groupId]);
            webix.message(result.description || "Выгрузка назначена");
            this._loadData();
        } catch (error) {
            webix.message({
                text: error.message || "Ошибка выгрузки настроек",
                type: "error"
            });
        }
    }

    async _showForceUpdateDialog() {
        const groupId = this._getSelectedGroupId();
        if (!groupId) {
            webix.message({
                text: this.LABELS.selectGroup,
                type: "error"
            });
            return;
        }

        const record = $$(this.NAMES.dataTable).getItem(groupId);
        const groupName = record?.name || groupId;

        let updates = [];
        try {
            const data = await softwareUpdatesService.loadUpdates(1, 500);
            updates = data?.content || [];
        } catch {
            webix.message({
                text: this.LABELS.errorLoadUpdates,
                type: "error"
            });
            return;
        }

        if (updates.length === 0) {
            webix.message({
                text: this.LABELS.noUpdates,
                type: "error"
            });
            return;
        }

        const options = updates.map((update) => ({
            id: update.id,
            value: `${update.version}.${update.assembly} · ${update.os} · ${update.architecture}`
        }));

        if ($$(this.NAMES.forceUpdateWindow)) {
            $$(this.NAMES.forceUpdateWindow).destructor();
        }

        webix.ui({
            view: "window",
            id: this.NAMES.forceUpdateWindow,
            modal: true,
            width: 420,
            position: "center",
            head: this.LABELS.forceInstallTitle,
            body: {
                view: "form",
                id: this.NAMES.forceUpdateForm,
                elements: [
                    {
                        view: "label",
                        label: `${this.LABELS.selectedGroup}: ${groupName}`
                    },
                    {
                        view: "combo",
                        name: "updateId",
                        label: this.LABELS.selectVersion,
                        labelPosition: "top",
                        options,
                        required: true,
                        invalidMessage: this.LABELS.selectVersionRequired
                    },
                    {
                        cols: [
                            {
                                view: "button",
                                value: this.LABELS.applyForce,
                                css: "webix_primary",
                                click: () => this._applyForcedUpdate(groupId)
                            },
                            {
                                view: "button",
                                value: this.LABELS.cancel,
                                click: () => $$(this.NAMES.forceUpdateWindow).close()
                            }
                        ]
                    }
                ]
            }
        }).show();
    }

    async _applyForcedUpdate(groupId) {
        const form = $$(this.NAMES.forceUpdateForm);
        if (!form.validate()) {
            return;
        }

        try {
            const result = await instanceGroupService.assignForcedUpdate([groupId], form.getValues().updateId);
            webix.message(result.description || "Назначение выполнено");
            $$(this.NAMES.forceUpdateWindow).close();
            this._loadData();
        } catch (error) {
            webix.message({
                text: error.message || "Ошибка назначения",
                type: "error"
            });
        }
    }

    _dataTable() {
        return {
            view: "datatable",
            id: this.NAMES.dataTable,
            columns: [
                { id: "name", header: this.LABELS.name, fillspace: true },
                {
                    id: "settingsSchemaName",
                    header: this.LABELS.settingsSchema,
                    width: 220,
                    template: (obj) => obj.settingsSchema?.name || ""
                },
                {
                    id: "autoUpdateAllowed",
                    header: this.LABELS.autoUpdateAllowed,
                    width: 180,
                    template: (obj) => obj.autoUpdateAllowed ? "Да" : "Нет"
                },
                {
                    id: "instancesCount",
                    header: this.LABELS.instancesCount,
                    width: 140,
                    template: (obj) => `${obj.instancesOnline ?? 0} / ${obj.instancesTotal ?? 0}`
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
            const data = await instanceGroupService.list(this.pageNumber, this.pageSize);

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
        instanceGroupElementView.showDialog({}, (created) => {
            $$(this.NAMES.dataTable).add({
                ...created,
                instancesTotal: 0,
                instancesOnline: 0
            });
        });
    }

    _edit(rowId) {
        const record = $$(this.NAMES.dataTable).getItem(rowId);
        if (!record) {
            return;
        }

        instanceGroupElementView.showDialog(record, (edited) => {
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
            text: "Вы собираетесь удалить запись?",
            ok: "Да",
            cancel: "Нет"
        }).then(async () => {
            try {
                await instanceGroupService.delete(recordId);
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

export default async function createInstanceGroupListView(id) {
    const view = new InstanceGroupListView(id)
        .delayedDataLoading()
        .render();

    return view;
}
