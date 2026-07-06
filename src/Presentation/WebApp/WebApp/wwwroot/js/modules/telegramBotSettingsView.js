import { loadConfiguration, saveConfigurationSections } from '../services/ConfigurationService.js';
import { Number, CheckBox, Text } from '../utils/ui.js';
import { AuthService } from '../services/AuthService.js';

class TelegramBotSettingsView {
    constructor(id) {
        this.id = id;
        this.telegramBotSettingsElementsId = "telegramBotSettingsView";
        this.labels = {
            title: "Fmu-Api-Central: Настройки Telegram бота",
            telegramBotSettings: "Настройки Telegram бота",
            isEnabled: "Использовать",
            chatId: "ID чата",
            botToken: "Токен бота",
            botProtocol: "Протокол",
            scheduler: "Расписание оповещений",
            addScheduleTime: "Добавить время",
            remove: "Удалить",
            offlineNodeAlertInterval: "Оповещать о недоступных узлах (часы)",
            localModuleVersionAlert: "Оповещать о версии локального модуля ниже указанной",
            localModuleDaysWithoutSynchronization: "Оповещать, если не было синхронизации локального модуля более чем указанных дней",
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

        return rows.map((x, index) => ({
            id: x.id,
            time: toTimeDate(x.time)
        }));
    }

    _getNextScheduleId() {
        const grid = $$("telegramSchedulerGrid");
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

    _cancelSchedulerEdit(grid) {
        if (grid.getEditor && grid.getEditor()) {
            grid.editCancel();
        }
    }

    _removeSchedulerRow(grid, rowId) {
        if (!rowId) return;

        this._cancelSchedulerEdit(grid);
        grid.remove(rowId);
    }

    async loadData() {
        const requestResult = await loadConfiguration();

        if (!requestResult.result) {
            console.error(requestResult);
            webix.message({ type: "error", text: requestResult.error });
            return this;
        }

        let configuration = requestResult.value.Content;

        this.telegramBotSettings = {
            isEnabled: configuration.telegramBotSettings?.isEnabled || false,
            chatId: configuration.telegramBotSettings?.chatId || 0,
            botToken: configuration.telegramBotSettings?.botToken || "",
            provider: configuration.telegramBotSettings?.provider || "telegram",
            offlineNodeAlertInterval: configuration.telegramBotSettings?.offlineNodeAlertInterval || 0,
            localModuleVersionAlert: configuration.telegramBotSettings?.localModuleVersionAlert || "",
            localModuleDaysWithoutSynchronization: configuration.telegramBotSettings?.localModuleDaysWithoutSynchronization || 3,
            scheduler: this._prepareScheduler(configuration.telegramBotSettings?.scheduler)
        };

        return this;
    }

    renderView() {
        $$("toolbarLabel").setValue(this.labels.title);

        const telegramBotSettings = {
            id: this.telegramBotSettingsElementsId,
            disabled: !this.telegramBotSettings.isEnabled,
            rows: [],
        };

        const enaledCheckBox = CheckBox(this.labels.isEnabled, "isEnabled", {
            value: this.telegramBotSettings.isEnabled,
            on: {
                onChange: (enabled) => {
                    if (enabled) {
                        $$(this.telegramBotSettingsElementsId).enable();
                    } else {
                        $$(this.telegramBotSettingsElementsId).disable();
                    }
                }
            }
        });

        telegramBotSettings.rows.push(
            {
                view: "richselect",
                label: this.labels.botProtocol,
                name: "provider",
                value: this.telegramBotSettings.provider,
                options: [
                    { id: "telegram", value: "telegram" },
                    { id: "max", value: "max" },
                    { id: "ntfy", value: "ntfy" }
                ]
            },

            Number(this.labels.chatId, "chatId", this.telegramBotSettings.chatId),

            Text(this.labels.botToken, "botToken", this.telegramBotSettings.botToken),

            {
                rows: [
                    { view: "label", label: this.labels.scheduler },
                    {
                        view: "datatable",
                        id: "telegramSchedulerGrid",
                        height: 220,
                        editable: true,
                        editaction: "click",
                        select: "row",
                        data: this.telegramBotSettings.scheduler,
                        columns: [
                            { id: "id", header: "№", hidden: false, width: 80 },
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
                        },
                        onClick: {
                            "remove-schedule-row": function (e, cell) {
                                this.remove(cell.row);
                                return false;
                            }
                        },
                    },
                    {
                        cols: [
                            {
                                view: "button",
                                value: this.labels.addScheduleTime,
                                width: 180,
                                click: () => {
                                    const grid = $$("telegramSchedulerGrid");
                                    grid.add({
                                        id: this._getNextScheduleId(),
                                        time: "09:00:00"
                                    });
                                }
                            },
                            {
                                view: "button",
                                value: this.labels.remove,
                                width: 180,
                                click: () => {
                                    const grid = $$("telegramSchedulerGrid");
                                    this._removeSchedulerRow(grid, grid.getSelectedId());
                                }
                            },
                            {}
                        ]
                    }
                ]
            },

            Number(this.labels.offlineNodeAlertInterval, "offlineNodeAlertInterval", this.telegramBotSettings.offlineNodeAlertInterval),

            Text(this.labels.localModuleVersionAlert, "localModuleVersionAlert", this.telegramBotSettings.localModuleVersionAlert),

            Number(this.labels.localModuleDaysWithoutSynchronization, "localModuleDaysWithoutSynchronization", this.telegramBotSettings.localModuleDaysWithoutSynchronization)
        );

        const info = {
            view: "template",
            template: `<div
                <strong>Бот проверяет следующие параметры:</strong><br>
                 - связь с нодами<br>
                 - статус локальных модулей нод (если не ready)<br>
                 - версия локальных модулей нод<br>
                 - дата-время синхронизации локальных ноды
            </div>`,
            height: 100,
            borderless: true,
        };

        let elements = [
            enaledCheckBox,
            telegramBotSettings,
            {
                cols: [
                    this._saveButton,
                    this._testButton,
                    this._sendAllertsButton,
                    {}
                ]
            },
            info,
            {}
        ];

        const telegramBotSettingsForm = {
            view: "form",
            elements: elements
        };

        return {
            id: this.id,
            rows: [
                telegramBotSettingsForm,
            ],
        };
    }

    _saveButton = {
        view: "button",
        value: "Сохранить",
        css: "webix_primary",
        width: 120,
        click: async function () {
            const form = this.getFormView();

            if (!form.validate()) return;

            const values = form.getValues();
            console.log(values);
            const schedulerGrid = $$("telegramSchedulerGrid");
            const schedulerRows = [];

            const toTimeString = webix.Date.dateToStr("%H:%i:%s");
            schedulerGrid.data.each((item) => {
                schedulerRows.push({
                    id: item.id,
                    time: toTimeString(item.time)
                });
            });

            const timeRegex = /^([01]\d|2[0-3]):([0-5]\d):([0-5]\d)$/;

            if (values.isEnabled) {
                if (schedulerRows.length === 0) {
                    webix.message({ type: "error", text: "Добавьте хотя бы одно время в расписание" });
                    return;
                }

                const invalidRow = schedulerRows.find(r => !timeRegex.test(String(r.time || "").trim()));
                if (invalidRow) {
                    webix.message({ type: "error", text: `Некорректное время в строке №${invalidRow.scheduleId}` });
                    return;
                }

                if (values.offlineNodeAlertInterval <= 0) {
                    webix.message({ type: "error", text: "Интервал оповещений о недоступных узлах должен быть больше 0" });
                    return;
                }

                if (values.localModuleDaysWithoutSynchronization <= 0) {
                    webix.message({ type: "error", text: "Дни без синхронизации локального модуля должны быть больше 0" });
                    return;
                }

                if (values.localModuleVersionAlert <= 0) {
                    webix.message({ type: "error", text: "Версия локального модуля должна быть больше 0" });
                    return;
                }

                if (values.chatId == 0) {
                    webix.message({ type: "error", text: "ID чата не может быть 0" });
                    return;
                }
            }

            const saveResult = await saveConfigurationSections({
                telegramBotSettings: _ => ({
                    isEnabled: !!values.isEnabled,
                    chatId: parseInt(values.chatId) || 0,
                    botToken: values.botToken || "",
                    provider: values.provider || "telegram",
                    offlineNodeAlertInterval: parseInt(values.offlineNodeAlertInterval) || 0,
                    localModuleVersionAlert: values.localModuleVersionAlert || "",
                    localModuleDaysWithoutSynchronization: parseInt(values.localModuleDaysWithoutSynchronization) || 3,
                    scheduler: schedulerRows.map((row, index) => ({
                        id: row.id,
                        time: String(row.time || "").trim()
                    }))
                })
            });

            if (!saveResult.result) {
                webix.message({ type: "error", text: saveResult.error });
                return;
            }

            webix.message({
                type: "success",
                text: "Настройки Telegram бота сохранены. Необходимо перезапустить службу для применения изменений."
            });
        }
    };

    _testButton = {
        view: "button",
        value: "Тест",
        width: 120,
        click: async function () {
            let answer = await AuthService.makeAuthenticatedRequest('/api/BotTest', {
                method: 'GET'
            });

            if (!answer.result) {
                webix.message({
                    type: "error",
                    text: answer.value
                });
            }
        }
    }

    _sendAllertsButton = {
        view: "button",
        value: "Отправить уведомления",
        width: 120,
        click: async function () {
            let answer = await AuthService.makeAuthenticatedRequest('/api/BotTest/sendAllerts', {
                method: 'GET'
            });

            if (!answer.result) {
                webix.message({
                    type: "error",
                    text: answer.value
                });
            }
        }
    }
}

export default async function createTelegramBotSettingsView(id) {
    const view = new TelegramBotSettingsView(id);
    await view.loadData();
    return view.renderView();
}