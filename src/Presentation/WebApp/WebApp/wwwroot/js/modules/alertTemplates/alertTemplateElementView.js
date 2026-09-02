import api from '../../services/alertTemplatesService.js';
import { Text, CheckBox } from '../../utils/ui.js';
import { ALERT_SCRIPT_DTS } from './alertScriptTypes.js';

const DEFAULT_SCRIPT = `const hours = settings.offlineNodeAlertInterval || 12;
const offline = instances.filter(i => i.hoursSinceUpdate >= hours);

return {
  title: "Недоступные узлы",
  items: offline.map(i => i.name + " (" + i.address + ")")
};`;

class AlertTemplateElementView {

    constructor() {
        this.elementId = "";
        this.resizeHandlerInitialized = false;
        this.savedWindowState = null;
        this.defaultEditorHeight = 360;
        this.reservedFormHeight = 280;

        this.LABELS = {
            name: "Имя шаблона",
            enabled: "Включён",
            scriptTab: "Скрипт",
            scheduleTab: "Расписание",
            addScheduleTime: "Добавить время",
            removeScheduleTime: "Удалить",
            content: "JS-скрипт набора данных",
            invalidNameMessage: "укажите имя",
            invalidScriptMessage: "укажите скрипт набора данных",
            invalidSchedulerMessage: "укажите хотя бы одно время запуска",
            invalidTimeMessage: "некорректное время",
            createButton: "Сохранить",
            cancelButton: "Отмена",
            previewButton: "Проверить набор",
            formTitle: "Шаблон оповещения",
            expandFullscreen: "На весь экран",
            collapseFullscreen: "Свернуть",
            previewEmpty: "Набор пуст — оповещение отправлено не будет",
            previewTitle: "Набор данных",
            help: "Скрипт получает <code>instances</code>, <code>statistics</code>, <code>now</code>, <code>settings</code> " +
                "и функцию <code>isVersionBelowThreshold</code>. Верните <code>{ title, items }</code>, массив строк или текст. " +
                "Пустой набор не отправляется. Запуск по расписанию шаблона."
        };

        this.NAMES = {
            windowId: "alertTemplateWindow",
            formId: "alertTemplateForm",
            fullscreenBtnId: "alertTemplateFullscreenBtn",
            name: "alertTemplateName",
            enabled: "alertTemplateEnabled",
            help: "alertTemplateHelp",
            tabView: "alertTemplateTabView",
            scriptTab: "alertTemplateScriptTab",
            scheduleTab: "alertTemplateScheduleTab",
            schedulerGrid: "alertTemplateSchedulerGrid",
            content: "alertTemplateScript"
        };
    }

    showDialog(editedData = {}, onSuccess, onClose) {
        if (editedData.id)
            this.elementId = editedData.id;
        else
            this.elementId = crypto.randomUUID();

        this._isNew = !editedData.id;
        this.savedWindowState = this._centeredSize();

        const scriptValue = editedData.script || (this._isNew ? DEFAULT_SCRIPT : "");
        const enabledValue = this._isNew ? false : !!editedData.enabled;
        const schedulerValue = this._prepareScheduler(editedData.scheduler);

        if ($$(this.NAMES.windowId)) {
            $$(this.NAMES.windowId).destructor();
        }

        const size = this._centeredSize();

        webix.ui({
            view: "window",
            id: this.NAMES.windowId,
            modal: true,
            autofit: false,
            resize: true,
            width: size.width,
            height: size.height,
            left: size.left,
            top: size.top,
            position: "center",
            move: true,
            head: this._createWindowHead(),
            body: {
                padding: 10,
                rows: [
                    {
                        view: "form",
                        id: this.NAMES.formId,
                        autoheight: true,
                        elements: [
                            Text(this.LABELS.name,
                                this.NAMES.name,
                                editedData.name || "",
                                { required: true, invalidMessage: this.LABELS.invalidNameMessage }
                            ),
                            {
                                cols: [
                                    CheckBox(this.LABELS.enabled, this.NAMES.enabled, {
                                        value: enabledValue,
                                        width: 180
                                    }),
                                    {}
                                ]
                            }
                        ]
                    },
                    this._tabs(scriptValue, schedulerValue),
                    this._createButtons(onSuccess, onClose)
                ]
            }
        }).show();

        this._setupWindowResizeHandler();
        this._restoreCenteredWindow();

        setTimeout(() => {
            const nameField = $$(this.NAMES.name);
            if (nameField) {
                nameField.focus();
            }
            this._restoreCenteredWindow();
            this._adjustEditorHeight();
        }, 100);
    }

    _centeredSize() {
        const width = Math.floor(window.innerWidth * 0.9);
        const height = Math.floor(window.innerHeight * 0.9);

        return {
            width,
            height,
            left: Math.floor((window.innerWidth - width) / 2),
            top: Math.floor((window.innerHeight - height) / 2),
            position: "center"
        };
    }

    /**
     * Ставит окно в центр с шириной 90% — после fullscreen Webix оставляет left=0.
     */
    _restoreCenteredWindow() {
        const win = $$(this.NAMES.windowId);
        if (!win || win.config.fullscreen) {
            return;
        }

        const size = this._centeredSize();
        this.savedWindowState = size;

        win.define({
            fullscreen: false,
            autofit: false,
            width: size.width,
            height: size.height,
            left: size.left,
            top: size.top,
            position: "center"
        });
        win.resize();
        win.setPosition(size.left, size.top);
    }

    _tabs(scriptValue, schedulerValue) {
        return {
            view: "tabview",
            id: this.NAMES.tabView,
            gravity: 1,
            tabbar: {
                on: {
                    onChange: (tabId) => {
                        if (tabId === this.NAMES.scriptTab) {
                            this._adjustEditorHeight();
                        }
                    }
                }
            },
            cells: [
                {
                    header: this.LABELS.scriptTab,
                    body: this._scriptTab(scriptValue)
                },
                {
                    header: this.LABELS.scheduleTab,
                    body: this._schedulerBlock(schedulerValue)
                },
            ]
        };
    }

    _scriptTab(scriptValue) {
        return {
            id: this.NAMES.scriptTab,
            rows: [
                {
                    view: "template",
                    id: this.NAMES.help,
                    borderless: true,
                    autoheight: true,
                    template: `<div style="color:#666;font-size:12px;line-height:1.4">${this.LABELS.help}</div>`
                },
                {
                    view: "monaco",
                    id: this.NAMES.content,
                    name: this.NAMES.content,
                    value: scriptValue,
                    gravity: 1,
                    minHeight: 200,
                    extraLib: ALERT_SCRIPT_DTS,
                    extraLibName: "alert-dataset.d.ts"
                }
            ]
        };
    }

    _prepareScheduler(rawScheduler) {
        const rows = Array.isArray(rawScheduler) ? rawScheduler : [];
        const toTimeDate = (value) => {
            const parsed = webix.Date.strToDate("%H:%i:%s")(String(value || ""));
            return parsed || webix.Date.strToDate("%H:%i:%s")("09:00:00");
        };

        if (rows.length === 0) {
            return [{
                id: 1,
                time: toTimeDate("09:00:00")
            }];
        }

        return rows.map((x) => ({
            id: x.id,
            time: toTimeDate(x.time)
        }));
    }

    _getNextScheduleId() {
        const grid = $$(this.NAMES.schedulerGrid);
        if (!grid) return 1;

        const rows = grid.serialize();
        if (!rows.length) return 1;

        let maxId = 0;
        rows.forEach(row => {
            const current = row.id || 0;
            if (current > maxId) {
                maxId = current;
            }
        });

        return maxId + 1;
    }

    _collectSchedulerRows() {
        const grid = $$(this.NAMES.schedulerGrid);
        const schedulerRows = [];
        if (!grid) {
            return schedulerRows;
        }

        const toTimeString = webix.Date.dateToStr("%H:%i:%s");
        grid.data.each((item) => {
            schedulerRows.push({
                id: item.id,
                time: toTimeString(item.time)
            });
        });

        return schedulerRows;
    }

    _schedulerBlock(schedulerValue) {
        return {
            id: this.NAMES.scheduleTab,
            rows: [
                {
                    cols: [
                        {
                            view: "button",
                            value: this.LABELS.addScheduleTime,
                            width: 180,
                            click: () => {
                                const grid = $$(this.NAMES.schedulerGrid);
                                grid.add({
                                    id: this._getNextScheduleId(),
                                    time: webix.Date.strToDate("%H:%i:%s")("09:00:00")
                                });
                            }
                        },
                        {
                            view: "button",
                            value: this.LABELS.removeScheduleTime,
                            width: 180,
                            click: () => {
                                const grid = $$(this.NAMES.schedulerGrid);
                                const rowId = grid.getSelectedId();
                                if (!rowId) {
                                    return;
                                }

                                if (grid.getEditor && grid.getEditor()) {
                                    grid.editCancel();
                                }

                                grid.remove(rowId);
                            }
                        },
                        {}
                    ]
                },
                {
                    view: "datatable",
                    id: this.NAMES.schedulerGrid,
                    gravity: 1,
                    editable: true,
                    editaction: "click",
                    select: "row",
                    data: schedulerValue,
                    columns: [
                        { id: "id", header: "№", width: 80 },
                        {
                            id: "time",
                            header: "Время (HH:mm:ss)",
                            fillspace: true,
                            editor: "dateTime",
                            format: webix.Date.dateToStr("%H:%i:%s")
                        }
                    ],
                    on: {
                        onBeforeDelete: function () {
                            if (this.getEditor && this.getEditor()) {
                                this.editCancel();
                            }
                            return true;
                        }
                    }
                }
            ]
        };
    }

    _createWindowHead() {
        return {
            view: "toolbar",
            cols: [
                { view: "label", label: this.LABELS.formTitle },
                {},
                {
                    view: "icon",
                    id: this.NAMES.fullscreenBtnId,
                    icon: "mdi mdi-fullscreen",
                    tooltip: this.LABELS.expandFullscreen,
                    click: () => this._toggleFullscreen()
                }
            ]
        };
    }

    _toggleFullscreen() {
        const win = $$(this.NAMES.windowId);

        if (!win) {
            return;
        }

        const btn = $$(this.NAMES.fullscreenBtnId);

        if (win.config.fullscreen) {
            this._restoreCenteredWindow();

            if (btn) {
                btn.define({
                    icon: "mdi mdi-fullscreen",
                    tooltip: this.LABELS.expandFullscreen
                });
                btn.refresh();
            }
        } else {
            this.savedWindowState = this._centeredSize();

            win.define({
                fullscreen: true,
                position: false,
                top: 0,
                left: 0
            });

            if (btn) {
                btn.define({
                    icon: "mdi mdi-fullscreen-exit",
                    tooltip: this.LABELS.collapseFullscreen
                });
                btn.refresh();
            }

            win.resize();
        }

        this._adjustEditorHeight();
    }

    _adjustEditorHeight() {
        const tabview = $$(this.NAMES.tabView);
        const editor = $$(this.NAMES.content);

        if (tabview) {
            tabview.resize();
        }

        if (editor) {
            editor.resize();
        }
    }

    _createButtons(onSuccess, onClose) {
        return {
            height: 40,
            cols: [
                {
                    view: "button",
                    value: this.LABELS.previewButton,
                    width: 180,
                    click: () => this._preview()
                },
                {},
                {
                    view: "button",
                    value: this.LABELS.createButton,
                    click: () => this._send(onSuccess),
                    hotkey: "alt+enter"
                },
                {
                    view: "button",
                    value: this.LABELS.cancelButton,
                    click: () => {
                        if (onClose) {
                            onClose();
                        }
                        $$(this.NAMES.windowId).close();
                    },
                    hotkey: "esc"
                }
            ]
        };
    }

    async _preview() {
        const form = $$(this.NAMES.formId);
        const script = $$(this.NAMES.content).getValue();

        if (!script || script === "") {
            webix.message({ text: this.LABELS.invalidScriptMessage, type: "error" });
            return;
        }

        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        try {
            const dataset = await api.preview(script);
            const text = dataset?.message
                ? dataset.message.replace(/%0A/g, "<br>").replace(/\n/g, "<br>")
                : this.LABELS.previewEmpty;

            webix.alert({
                title: this.LABELS.previewTitle,
                text: text,
                width: 640
            });
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
        } finally {
            form.enable();
            form.hideProgress();
        }
    }

    async _send(onSuccess) {
        const win = $$(this.NAMES.windowId);
        const form = $$(this.NAMES.formId);

        if (!this._validate())
            return;

        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        const name = $$(this.NAMES.name).getValue();
        const script = $$(this.NAMES.content).getValue();
        const enabled = !!$$(this.NAMES.enabled).getValue();
        const scheduler = this._collectSchedulerRows();

        const data = {
            id: this.elementId,
            name: name,
            script: script || "",
            enabled: enabled,
            scheduler: scheduler
        };

        try {
            await api.create(data);

            if (onSuccess) {
                onSuccess(data);
            }

            win.close();
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            form.enable();
            form.hideProgress();
        }
    }

    _validate() {
        const name = $$(this.NAMES.name).getValue();
        const script = $$(this.NAMES.content).getValue();
        const enabled = !!$$(this.NAMES.enabled).getValue();
        const schedulerRows = this._collectSchedulerRows();
        const timeRegex = /^([01]\d|2[0-3]):([0-5]\d):([0-5]\d)$/;

        if (!name || name === "") {
            webix.message({ text: this.LABELS.invalidNameMessage, type: "error" });
            return false;
        }

        if (!script || script === "") {
            webix.message({ text: this.LABELS.invalidScriptMessage, type: "error" });
            return false;
        }

        if (enabled && schedulerRows.length === 0) {
            webix.message({ text: this.LABELS.invalidSchedulerMessage, type: "error" });
            return false;
        }

        const invalidRow = schedulerRows.find(r => !timeRegex.test(String(r.time || "").trim()));
        if (invalidRow) {
            webix.message({ text: `${this.LABELS.invalidTimeMessage} в строке №${invalidRow.id}`, type: "error" });
            return false;
        }

        return true;
    }

    _setupWindowResizeHandler() {
        if (this.resizeHandlerInitialized) {
            return;
        }

        webix.event(window, "resize", () => {
            const win = $$(this.NAMES.windowId);

            if (!win || !win.isVisible()) {
                return;
            }

            if (win.config.fullscreen) {
                win.resize();
                this._adjustEditorHeight();
            } else {
                this._restoreCenteredWindow();
            }
        });

        this.resizeHandlerInitialized = true;
    }
}

export default new AlertTemplateElementView();
