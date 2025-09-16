// src/Presentation/WebApp/WebApp/wwwroot/js/modules/softwareUpdatesElementView.js
import { Number, TextBox } from '../../utils/ui.js';
import softwareUpdatesService from '../../services/softwareUpdatesService.js';

class SoftwareUpdatesElementView {
    constructor() {
        this.LABELS = {
            upload: "Загрузить",
            version: "Версия",
            assembly: "Сборка",
            architecture: "Архитектура",
            os: "ОС",
            comment: "Комментарий",
            uploadTitle: "Загрузка файла обновления"
        };
    }

    showUploadDialog(onSuccess) {
        webix.ui({
            view: "window",
            id: "uploadWindow",
            modal: true,
            width: 500,
            position: "center",
            head: this.LABELS.uploadTitle,
            body: {
                view: "form",
                id: "uploadForm",
                elements: [
                    Number("Версия", "version", "0", "1111",{ required: true, invalidMessage: "укажите версию" }),
                    Number("Сборка", "assembly", "0", "1111", { required: true, invalidMessage: "укажите номер сборки" }),
                    this._createArchitectureSelect(),
                    this._createOsSelect(),
                    TextBox("text", "Комментарий", "comment"),
                    this._createFileUploader(),
                    this._createUploadedFilesList(),
                    this._createButtons(onSuccess)
                ]
            }
        }).show();
    }

    _createArchitectureSelect() {
        return {
            view: "select",
            id: "architecture",
            name: "architecture",
            label: this.LABELS.architecture,
            labelPosition: "top",
            options: [
                { id: "x64", value: "x64" },
                { id: "x86", value: "x86" }
            ],
            value: "x64"
        };
    }

    _createOsSelect() {
        return {
            view: "select",
            id: "os",
            name: "os",
            label: this.LABELS.os,
            labelPosition: "top",
            options: [
                { id: "windows", value: "Windows" },
                { id: "linux", value: "Linux" }
            ],
            value: "windows"
        };
    }

    _createFileUploader() {
        return {
            view: "uploader",
            name: "file",
            id: "uploader",
            label: "Нажмите для выбора файла",
            autosend: false,
            accept: "application/zip",
            multiple: false,
            link: "uploadedFilesList",
            on: {
                onAfterFileAdd: (file) => this._parseAndFillFileName(file.name)
            }
        };
    }

    _createUploadedFilesList() {
        return {
            view: "list",
            id: "uploadedFilesList",
            type: "uploader",
            autoheight: true,
            borderless: true
        };
    }

    _createButtons(onSuccess) {
        return {
            cols: [
                { view: "button", value: "Загрузить", click: () => this._uploadFile(onSuccess) },
                { view: "button", value: "Отмена", click: () => $$("uploadWindow").close() }
            ]
        };
    }

    _parseFileName(fileName) {
        const result = {};
        const nameWithoutExt = fileName.replace(/\.(zip|exe|msi)$/i, '');

        const versionMatch = nameWithoutExt.match(/(\d+)\.(\d+)|(\d+)_(\d+)|(\d+)-(\d+)/);
        if (versionMatch) {
            result.version = versionMatch[1] || versionMatch[3] || versionMatch[5];
            result.assembly = versionMatch[2] || versionMatch[4] || versionMatch[6];
        }

        result.architecture = nameWithoutExt.toLowerCase().includes('x86') ? 'x86' : 'x64';
        result.os = nameWithoutExt.toLowerCase().includes('linux') ? 'linux' : 'windows';

        return result;
    }

    _parseAndFillFileName(fileName) {
        const parsedData = this._parseFileName(fileName);

        if (parsedData.version) $$("version").setValue(parsedData.version);
        if (parsedData.assembly) $$("assembly").setValue(parsedData.assembly);
        if (parsedData.architecture) $$("architecture").setValue(parsedData.architecture);
        if (parsedData.os) $$("os").setValue(parsedData.os);
    }

    _validateForm() {
        const version = $$("version").getValue();
        const assembly = $$("assembly").getValue();
        const files = $$("uploader")?.files.data?.pull;

        if (!version || version === "") {
            webix.message({ text: "Поле 'Версия' обязательно для заполнения", type: "error" });
            return false;
        }

        if (!assembly || assembly === "") {
            webix.message({ text: "Поле 'Сборка' обязательно для заполнения", type: "error" });
            return false;
        }

        if (!files || files.length === 0) {
            webix.message({ text: "Выберите файл для загрузки", type: "error" });
            return false;
        }

        return true;
    }

    async _uploadFile(onSuccess) {
        if (!this._validateForm()) return;

        const form = $$("uploadForm");
        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        try {
            const formData = $$("uploadForm").getValues();
            const files = $$("uploader")?.files;
            const file_id = files.getFirstId();
            const file_obj = files.getItem(file_id).file;

            const fileHash = await softwareUpdatesService.calculateFileHash(file_obj);

            const recordData = {
                version: parseInt(formData.version),
                assembly: parseInt(formData.assembly),
                architecture: formData.architecture,
                os: formData.os,
                comment: formData.comment || "",
                fileSize: file_obj.size,
                sha256: fileHash
            };

            const createdRecord = await softwareUpdatesService.createUpdate(recordData);
            
            await softwareUpdatesService.attachFile(createdRecord.id, file_obj);

            webix.message("Файл загружен успешно");
            
            $$("uploadWindow").close();

            if (onSuccess) onSuccess(createdRecord, formData, file_obj);

        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            form.enable();
            form.hideProgress();
        }
    }
}

export default new SoftwareUpdatesElementView();