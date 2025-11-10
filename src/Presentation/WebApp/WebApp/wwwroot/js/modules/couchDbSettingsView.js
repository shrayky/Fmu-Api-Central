// js/modules/couchDbSettingsView.js

import { CheckBox, Text, Label, PasswordBox, Number } from '../utils/ui.js';
import { httpAddressValidation } from "../utils/validators.js";
import { loadConfiguration, saveConfigurationSection } from '../services/ConfigurationService.js';

class CouchDbSettingsView {
    constructor(id) {
        this.id = id;
        this.settingsId = "couchDbSettingsView";
        this.labels = {
            title: "Fmu-Api-Central: Настройка базы данных",
            enable: "Использовать CouchDB",
            netAddress: "Адрес сервера",
            userName: "Имя пользователя",
            password: "Пароль",
            bulk: "Параметры bulk операций",
            bulkBatchSize: "Размер пакета для bulk операций",
            bulkParallelTasks: "Количество параллельных задач",
            queryLimit: "Максимальное количество документов для запроса",
            queryTimeout: "Таймаут запроса (секунд)",
        }
    }

    async loadData() {
        const requestResult = await loadConfiguration();

        if (!requestResult.result) {
            console.error(requestResult);
            webix.message({ type: "error", text: requestResult.error });
            return this;
        }

        let configuration = requestResult.value.Content;
        
        if (!configuration) {
            console.error(configuration);
            webix.message({ type: "error", text: "Пустая конфигурация" });
            return this;
        }
        
        const settings = configuration.databaseConnection;
        
        this.enable = settings.enable;
        this.netAddress = settings.netAddress;
        this.userName = settings.userName;
        this.password = settings.password;
        this.bulkBatchSize = settings.bulkBatchSize;
        this.bulkParallelTasks = settings.bulkParallelTasks;
        this.queryLimit = settings.queryLimit;
        this.queryTimeout = settings.queryTimeout;
        return this;
    }

    renderView() {
        $$("toolbarLabel").setValue(this.labels.title);

        let elements = [];

        const checkBox = CheckBox(this.labels.enable, "enable", {
            value: this.enable,
            on: {
                onChange: (enabled) => {
                    if (enabled) {
                        $$(this.settingsId).enable();
                    }
                    else {
                        $$(this.settingsId).disable();
                    }
                }
            }
        });

        const settingsFields = {
            id: this.settingsId,
            disabled: !this.enable,
            rows: []
        }

        settingsFields.rows.push(
            {
                cols: [
                    Text(this.labels.netAddress, "netAddress", this.netAddress, httpAddressValidation),
                    {
                        rows:
                            [
                                {},
                                this._fauxtonButton
                            ]
                    }
                ]
            },

            {
                cols: [
                    Text(this.labels.userName, "userName", this.userName, webix.rules.isNotEmpty),
                    PasswordBox(this.labels.password, "password", { value: this.password })
                ]
            },

            Label("bulkOperationsTitle", this.labels.bulk),
            {
                cols: [
                    Number(this.labels.bulkBatchSize, "bulkBatchSize", this.bulkBatchSize),
                    Number(this.labels.bulkParallelTasks, "bulkParallelTasks", this.bulkParallelTasks),
                    Number(this.labels.queryTimeout, "queryTimeout", this.queryTimeout),
                ]
            },
            
            Number(this.labels.queryLimit, "queryLimit", this.queryLimit),

        )
        
        const buttons  = {
            cols: [
                this._saveButton,
                {}
            ]
        };

        elements.push(
            checkBox,
            settingsFields,
            buttons,
            {}
        )

        const couchDbSettingsForm = {
            view: "form",
            elements: elements
        };

        return {
            id:
                this.id,
            rows: [
                couchDbSettingsForm,
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
    
            const saveResult = await saveConfigurationSection('databaseConnection', _prev => ({
                enable: !!values.enable,
                netAddress: values.netAddress,
                userName: values.userName,
                password: values.password,
                bulkBatchSize: parseInt(values.bulkBatchSize),
                bulkParallelTasks: parseInt(values.bulkParallelTasks),
                queryLimit: values.queryLimit,
                queryTimeout: values.queryTimeout,
            }));
    
            if (!saveResult.result) {
                webix.message({ type: "error", text: saveResult.error });
                return;
            }
    
            webix.message({
                type: "success",
                text: "Настройки сохранены. Необходимо перезапустить службу для применения изменений."
            });
        }
    }

    _fauxtonButton = {
        view: "button",
        value: "Открыть Fauxton",
        width: 150,
        align: "center",
        click: _ => {
            let netAddress = $$("netAddress").getValue();
            if (netAddress) {
                const fauxtonUrl = `${netAddress}/_utils/`;
                window.open(fauxtonUrl, '_blank');
            } else {
                webix.message({ type: "error", text: "Сначала укажите адрес сервера" });
            }
        }
    }
}

export default async function createCouchDbSettingsView(id) {
    const view = new CouchDbSettingsView(id);
    await view.loadData();
    return view.renderView();
}