import { loadConfiguration, saveConfigurationSections } from '../services/ConfigurationService.js';
import { Number, CheckBox, Text } from '../utils/ui.js';
import { AuthService } from '../services/AuthService.js';

class AlertSettingsView {
    constructor(id) {
        this.id = id;
        this.alertSettingsElementsId = "alertSettingsView";
        this.labels = {
            title: "Fmu-Api-Central: Настройки оповещений",
            alertSettings: "Настройки оповещений",
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
            tsPiotStatusAlertEnabled: "Оповещать о состоянии ТС ПИоТ",
            tsPiotLicenseAlertEnabled: "Оповещать об истечении лицензии ТС ПИоТ",
            tsPiotLicenseAlertDays: "За сколько дней оповещать об истечении лицензии ТС ПИоТ",
            tsPiotVersionAlert: "Оповещать о версии ТС ПИоТ ниже указанной",
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
        const grid = $$("alertSchedulerGrid");
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
        const bot = configuration.telegramBotSettings || {};
        const localModuleAlerts = bot.localModuleAlerts || {};
        const tsPiotAlerts = bot.tsPiotAlerts || {};

        this.alertSettings = {
            isEnabled: bot.isEnabled || false,
            chatId: bot.chatId || 0,
            botToken: bot.botToken || "",
            provider: bot.provider || "telegram",
            offlineNodeAlertInterval: bot.offlineNodeAlertInterval || 0,
            localModuleVersionAlert: localModuleAlerts.versionAlert
                ?? bot.localModuleVersionAlert
                ?? "",
            localModuleDaysWithoutSynchronization: localModuleAlerts.daysWithoutSynchronization
                ?? bot.localModuleDaysWithoutSynchronization
                ?? 3,
            tsPiotStatusAlertEnabled: tsPiotAlerts.statusAlertEnabled
                ?? bot.tsPiotStatusAlertEnabled
                ?? false,
            tsPiotLicenseAlertEnabled: tsPiotAlerts.licenseAlertEnabled
                ?? bot.tsPiotLicenseAlertEnabled
                ?? false,
            tsPiotLicenseAlertDays: tsPiotAlerts.licenseAlertDays
                ?? bot.tsPiotLicenseAlertDays
                ?? 7,
            tsPiotVersionAlert: tsPiotAlerts.versionAlert
                ?? bot.tsPiotVersionAlert
                ?? "",
            scheduler: this._prepareScheduler(bot.scheduler)
        };

        return this;
    }

    renderView() {
        $$("toolbarLabel").setValue(this.labels.title);

        const alertSettings = {
            id: this.alertSettingsElementsId,
            disabled: !this.alertSettings.isEnabled,
            rows: [],
        };

        const enaledCheckBox = CheckBox(this.labels.isEnabled, "isEnabled", {
            value: this.alertSettings.isEnabled,
            on: {
                onChange: (enabled) => {
                    if (enabled) {
                        $$(this.alertSettingsElementsId).enable();
                    } else {
                        $$(this.alertSettingsElementsId).disable();
                    }
                }
            }
        });

        alertSettings.rows.push(
            {
                view: "richselect",
                label: this.labels.botProtocol,
                name: "provider",
                value: this.alertSettings.provider,
                options: [
                    { id: "telegram", value: "telegram" },
                    { id: "max", value: "max" },
                    { id: "ntfy", value: "ntfy" }
                ]
            },

            Number(this.labels.chatId, "chatId", this.alertSettings.chatId),

            Text(this.labels.botToken, "botToken", this.alertSettings.botToken),

            {
                rows: [
                    { view: "label", label: this.labels.scheduler },

                    {
                        cols: [
                            {
                                view: "button",
                                value: this.labels.addScheduleTime,
                                width: 180,
                                click: () => {
                                    const grid = $$("alertSchedulerGrid");
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
                                    const grid = $$("alertSchedulerGrid");
                                    this._removeSchedulerRow(grid, grid.getSelectedId());
                                }
                            },
                            {}
                        ]
                    }

                    {
                        view: "datatable",
                        id: "alertSchedulerGrid",
                        height: 220,
                        editable: true,
                        editaction: "click",
                        select: "row",
                        data: this.alertSettings.scheduler,
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

                ]
            },

            Number(this.labels.offlineNodeAlertInterval, "offlineNodeAlertInterval", this.alertSettings.offlineNodeAlertInterval),

            Text(this.labels.localModuleVersionAlert, "localModuleVersionAlert", this.alertSettings.localModuleVersionAlert),

            Number(this.labels.localModuleDaysWithoutSynchronization, "localModuleDaysWithoutSynchronization", this.alertSettings.localModuleDaysWithoutSynchronization),

            CheckBox(this.labels.tsPiotStatusAlertEnabled, "tsPiotStatusAlertEnabled", {
                value: this.alertSettings.tsPiotStatusAlertEnabled
            }),

            CheckBox(this.labels.tsPiotLicenseAlertEnabled, "tsPiotLicenseAlertEnabled", {
                value: this.alertSettings.tsPiotLicenseAlertEnabled
            }),

            Number(this.labels.tsPiotLicenseAlertDays, "tsPiotLicenseAlertDays", this.alertSettings.tsPiotLicenseAlertDays),

            Text(this.labels.tsPiotVersionAlert, "tsPiotVersionAlert", this.alertSettings.tsPiotVersionAlert)
        );

        const info = {
            view: "template",
            template: `<div>
                <strong>Проверяются следующие параметры:</strong><br>
                 - связь с нодами<br>
                 - статус локальных модулей нод (если не ready)<br>
                 - версия локальных модулей нод<br>
                 - дата-время синхронизации локальных модулей<br>
                 - состояние ТС ПИоТ (offline)<br>
                 - истечение лицензии ТС ПИоТ<br>
                 - версия ТС ПИоТ
            </div>`,
            height: 160,
            borderless: true,
        };

        let elements = [
            enaledCheckBox,
            alertSettings,
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

        const alertSettingsForm = {
            view: "form",
            elements: elements
        };

        return {
            id: this.id,
            rows: [
                alertSettingsForm,
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
            const schedulerGrid = $$("alertSchedulerGrid");
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
                    webix.message({ type: "error", text: `Некорректное время в строке №${invalidRow.id}` });
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

                if (values.tsPiotLicenseAlertEnabled && values.tsPiotLicenseAlertDays <= 0) {
                    webix.message({ type: "error", text: "Количество дней до оповещения о лицензии ТС ПИоТ должно быть больше 0" });
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
                    localModuleAlerts: {
                        versionAlert: values.localModuleVersionAlert || "",
                        daysWithoutSynchronization: parseInt(values.localModuleDaysWithoutSynchronization) || 3
                    },
                    tsPiotAlerts: {
                        statusAlertEnabled: !!values.tsPiotStatusAlertEnabled,
                        licenseAlertEnabled: !!values.tsPiotLicenseAlertEnabled,
                        licenseAlertDays: parseInt(values.tsPiotLicenseAlertDays) || 7,
                        versionAlert: values.tsPiotVersionAlert || ""
                    },
                    scheduler: schedulerRows.map((row) => ({
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
                text: "Настройки оповещений сохранены. Необходимо перезапустить службу для применения изменений."
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

export default async function createAlertSettingsView(id) {
    const view = new AlertSettingsView(id);
    await view.loadData();
    return view.renderView();
}
