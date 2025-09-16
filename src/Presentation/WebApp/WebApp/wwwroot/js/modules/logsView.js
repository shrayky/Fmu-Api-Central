// wwwroot/js/modules/logsView.js

import { AuthService } from '../services/AuthService.js';

class LogsView {
    constructor(id) {
        this.formName = "LogsView";
        this.id = id;

        this.LABELS = {
            formTitle: "Fmu-Api-Central: Логи работы",
            title: "Просмотр логов",
            logFile: "Лог работы:",
            refresh: "Обновить",
            save: "Сохранить",
            downloadAll: "Выгрузить все логи",
            close: "Закрыть",
            fileDescription: "Текстовый файл",
            errorDownload: "Ошибка при выгрузке логов",
            errorLoad: "Ошибка при загрузке логов"
        }

        this.NAMES = {
            toolbarLabel: "toolbarLabel",
            logFiles: "logFiles",
            refreshLogBtn: "refreshLogBtn",
            uploadLogBtn: "uploadLogBtn",
            log: "log"
        }
    }

    renderView() {
        $$(this.NAMES.toolbarLabel).setValue(this.LABELS.formTitle);

        var formElements = [
            this._logToolbar(),
            {
                view: "textarea",
                id: this.NAMES.log,
                readonly: true,
            }];

        var form = {
            view: "form",
            id: this.id,
            name: this.formName,
            disabled: true,
            on: {
                onViewShow: this._loadLogs("", this.id),
            },
            elements: formElements
        }

        return form;
    }

    _logToolbar() {

        const combo = {
            view: "combo",
            id: this.NAMES.logFiles,
            label: this.LABELS.logFile,
            labelWidth: 100,
            newValues: false,
            options: [],
            on: {
                "onChange": (newValue) => {
                    this._loadLogs(newValue, this.id);
                }
            }
        };

        const refreshButton = {
            view: "button",
            value: this.LABELS.refresh,
            id: this.NAMES.refreshLogBtn,
            icon: "wxi-sync",
            autoWidth: "false",
            width: 400,
            click: async _ => {
                var combo = $$(this.NAMES.logFiles);
                var chosenLogName = combo.getValue();

                if (!chosenLogName)
                    return;

                if (chosenLogName == "")
                    return;

                this._loadLogs(chosenLogName, this.id);
            }
        };

        const uploadLogBtn = {
            view: "button",
            value: this.LABELS.save,
            id: this.NAMES.uploadLogBtn,
            icon: "wxi-download",
            autoWidth: "false",
            width: 400,
            click: async _ => {
                var logText = $$(this.NAMES.log);
                var fileHandler = await this._getNewFileHandle();
                await this._writeTextFile(fileHandler, logText.getValue());
            }
        }

        const toolbar = {
            cols: [combo, refreshButton, uploadLogBtn]
        };

        return toolbar;
    }

    _loadLogs(fileName, elementId) {
        if (!$$(elementId).showProgress) {
            webix.extend($$(elementId), webix.ProgressBar);
        }

        $$(elementId).showProgress({ type: "icon", icon: "wxi-sync" });

        fileName = fileName.length == 0 ? "now" : fileName;


        AuthService.makeAuthenticatedRequest(`/api/logs/${fileName}`, "GET")
            .then((data) => {

                if (!data.result) {
                    webix.message({ type: "error", text: data.error });
                    return;
                }

                var logInfo = data.value;

                var combo = $$(this.NAMES.logFiles);

                combo.blockEvent();

                var comboOptions = combo.getPopup().getList();
                comboOptions.clearAll();
                comboOptions.parse(logInfo.logFilesNames);

                combo.setValue(logInfo.selectedLogFileName);

                combo.unblockEvent();

                var logText = $$(this.NAMES.log);
                const lines = logInfo.text.split('\n').reverse().join('\n');
                logText.setValue(lines);

                $$(elementId).enable();
                try {
                    $$(elementId).hideProgress();
                }
                catch (e) {};

            });
    }

    async _getNewFileHandle() {
        const options = {
            types: [
                {
                    keepExistingData: false,
                    description: 'Текстовый файл',
                    accept: {
                        'text/plain': ['.txt'],
                    },
                },
            ],
        };

        const handle = await window.showSaveFilePicker(options);
        return handle;
    }

    async _writeTextFile(fileHandle, data) {
        const writableStream = await fileHandle.createWritable();
        const lines = data.split('\n').reverse().join('\n');
        await writableStream.write(lines);
        await writableStream.close();
    }
}

export default function createLogsView(id) {
    const view = new LogsView(id);

    return view.renderView();
}