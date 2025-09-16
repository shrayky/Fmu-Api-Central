// js/modules/authView.js

import { AuthService } from '../services/AuthService.js';

export default function createAuthView(onLoginSuccess) {
    const savedServer = localStorage.getItem('serverUrl') || 'http://localhost:2579';
    return {
        view: "window",
        id: "authWindow",
        width: 550,
        position: "center",
        modal: true,
        css: "webix_dark",
        head: "Авторизация на сервере",
        body: {
            view: "form",
            id: "authForm",
            elements: [
                { view: "text", label: "Адрес сервера", name: "server", value: savedServer, required: true, labelPosition: "top", placeholder: "http://hostname:port" },
                { view: "text", label: "Логин", name: "login", required: true, labelPosition: "top" },
                { view: "text", type: "password", label: "Пароль", name: "password", required: true, labelPosition: "top" },
                {
                    margin: 10,
                    cols: [
                        {
                            view: "button", value: "Войти", hotkey: "enter", css: "webix_primary", click: async function () {
                                const form = this.getFormView();
                                if (!form.validate()) return;

                                webix.extend(form, webix.ProgressBar);

                                form.showProgress({ type: "icon" });

                                const values = form.getValues();
                                let result = {};

                                try {
                                    result = await AuthService.login(values.server, values.login, values.password);
                                }
                                catch (error) {
                                    const errorMessage = error.message || "Ошибка соединения или авторизации";
                                    webix.message({ type: "error", text: errorMessage });
                                    
                                    form.hideProgress();    
                                    return;
                                }

                                form.hideProgress();

                                if (result && result.accessToken) {
                                    localStorage.setItem('serverUrl', values.server);
                                    webix.message({ type: "success", text: "Успешный вход" });
                                                                        
                                    $$('authWindow').close();

                                    if (onLoginSuccess) {
                                        onLoginSuccess(values.server, values.login, values.password);
                                    }
                                } else {
                                    webix.message({ type: "error", text: "Неверный формат ответа сервера" });
                                }
                            }
                        }
                    ]
                }
            ],
            rules: {
                server: webix.rules.isNotEmpty,
                login: webix.rules.isNotEmpty,
                password: webix.rules.isNotEmpty
            }
        }
    };
}