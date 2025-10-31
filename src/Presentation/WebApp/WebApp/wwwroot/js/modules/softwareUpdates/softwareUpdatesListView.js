import softwareUpdatesService from '../../services/softwareUpdatesService.js';
import softwareUpdatesElementView from '../../services/softwareUpdatesService.js';

class SoftwareUpdatesListView {
    constructor(id) {
        this.formName = "SoftwareUpdatesView";
        this.id = id;
        this.pageSize = 50;
        this.pageNumber = 1;

        this.LABELS = {
            formTitle: "Fmu-Api-Central: Обновления ПО",
            refresh: "Обновить",
            upload: "Загрузить",
            delete: "Удалить",
            version: "Версия",
            assembly: "Сборка",
            architecture: "Архитектура",
            os: "ОС",
            fileSize: "Размер",
            createdAt: "Дата создания",
            comment: "Комментарий",
            errorLoad: "Ошибка при загрузке данных",
            errorDelete: "Ошибка при удалении записи",
            page: "Страница",
            prevButton: "←",
            nextButton: "→",
            download: "Скачать"
        };

        this.NAMES = {
            toolbarLabel: "toolbarLabel",
            refreshBtn: "refreshBtn",
            uploadBtn: "uploadBtn",
            deleteBtn: "deleteBtn",
            dataTable: "softwareUpdatesDataTable",
            prevButton: "prevButton",
            nextButton: "nextButton",
            paginationInfo: "paginationInfo",
            version: "version",
            assembly: "assembly",
            architecture: "architecture",
            os: "os",
            fileSize: "fileSize",
            createdAt: "createdAt",
            comment: "comment",
            downloadBtn: "downloadBtn",
            form: "listViewForm",
        };

        this.TEMPLATES = {
            fileSize: "#fileSize# байт",
            createdAt: "#createdAt#"
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
            id: this.NAMES.form,
            elements: [
                this._toolbar(),
                this._dataTable()
            ]
        };

        return {
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
    }

    _toolbar() {
        return {
            view: "toolbar",
            elements: [
                {
                    view: "button",
                    id: this.NAMES.uploadBtn,
                    value: this.LABELS.upload,
                    width: 100,
                    click: () => this._showUploadDialog(),
                    hotkey: "insert"
                },
                {
                    view: "button",
                    id: this.NAMES.deleteBtn,
                    value: this.LABELS.delete,
                    width: 100,
                    click: () => this._deleteFile(),
                    hotkey: "delete"
                },
                {
                    view: "button",
                    id: this.NAMES.downloadBtn,
                    value: this.LABELS.download,
                    width: 100,
                    click: () => this._downloadFile(),
                    hotkey: "f5"
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
                { id: this.NAMES.version, header: this.LABELS.version, width: 80 },
                { id: this.NAMES.assembly, header: this.LABELS.assembly, width: 80 },
                { id: this.NAMES.architecture, header: this.LABELS.architecture, width: 100 },
                { id: this.NAMES.os, header: this.LABELS.os, width: 100 },
                { id: this.NAMES.fileSize, header: this.LABELS.fileSize, width: 200, template: this.TEMPLATES.fileSize },
                { id: this.NAMES.createdAt, header: this.LABELS.createdAt, width: 150, template: this.TEMPLATES.createdAt },
                { id: this.NAMES.comment, header: this.LABELS.comment, width: 200, fillspace: true },
            ],
            select: "row",
            multiselect: false,
        };
    }

    async _loadData() {
        try {
            const data = await softwareUpdatesService.loadUpdates(this.pageNumber, this.pageSize);

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

    async _downloadFile() {
        const record = $$(this.NAMES.dataTable).getSelectedId();

        if (!record) {
            webix.message({
                text: "Выберите запись для скачивания",
                type: "error"
            });

            return;
        }

        const form = $$(this.NAMES.form);
        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        const data = await softwareUpdatesService.downloadFile(record.id);
        
        form.hideProgress();
        form.enable();
        
        if (!data.result    ) {
            webix.message({
                text: data.error,
                type: "error"
            });

            return;
        }
        const fileName = `fmu-api.zip`;

        const file = new Blob([data.value], { type: 'application/octet-stream'});
        const url = URL.createObjectURL(file);
        
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        URL.revokeObjectURL(url);
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

    async _deleteFile() {
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
                await softwareUpdatesService.deleteUpdate(recordId);
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

    _showUploadDialog() {
        softwareUpdatesElementView.showUploadDialog((createdRecord, formData, file_obj) => {
            this._addToTable(createdRecord, formData, file_obj);
        });
    }

    _addToTable(createdRecord, formData, file_obj) {
        const newRecord = {
            id: createdRecord.id,
            version: formData.version,
            assembly: formData.assembly,
            architecture: formData.architecture,
            os: formData.os,
            fileSize: file_obj.size,
            createdAt: new Date().toISOString(),
            comment: formData.comment || ""
        };

        $$(this.NAMES.dataTable).add(newRecord);
    }

    _goToPage(page) {
        if (page >= 1) {
            this.pageNumber = page;
            this._loadData();
        }
    }
}

export default async function createSoftwareUpdatesListView(id) {
    const view = new SoftwareUpdatesListView(id)
                    .delayedDataLoading()
                    .render();

    return view;
}