import { AuthService } from '../services/AuthService.js';
import { PasswordBox } from '../utils/ui.js';

const defaultPassword = "admin";

export default function createChangePasswordView({ serverUrl, login, currentPassword, onDone }) {
    return {
        view: "window",
        id: "changePasswordWindow",
        modal: true,
        move: false,
        position: "center",
        width: 550,
        css: "webix_dark",
        head: {
            view: "toolbar",
            cols: [
                { view: "label", label: "Смените пароль по умолчанию" }
            ]
        },
        body: {
            view: "form",
            id: "changePasswordForm",
            elements: [
                {
                    view: "label",
                    label: "После сохранения войдите с новым паролем."
                },
                PasswordBox("Новый пароль", "newPassword", { required: true }),
                PasswordBox("Повторите пароль", "confirmPassword", { required: true }),
                {
                    view: "button",
                    value: "Сохранить",
                    css: "webix_primary",
                    hotkey: "enter",
                    click: async function () {
                        const form = this.getFormView();
                        const values = form.getValues();
                        const newPassword = (values.newPassword || "").trim();
                        const confirmPassword = (values.confirmPassword || "").trim();

                        if (!newPassword) {
                            webix.message({ type: "error", text: "Укажите новый пароль" });
                            return;
                        }

                        if (newPassword === defaultPassword) {
                            webix.message({ type: "error", text: "Нельзя оставить пароль по умолчанию" });
                            return;
                        }

                        if (newPassword !== confirmPassword) {
                            webix.message({ type: "error", text: "Пароли не совпадают" });
                            return;
                        }

                        webix.extend(form, webix.ProgressBar);
                        form.showProgress({ type: "icon" });
                        form.disable();

                        try {
                            await AuthService.changePassword(serverUrl, login, currentPassword, newPassword);
                            webix.message({ type: "success", text: "Пароль изменён. Войдите с новым паролем" });
                            $$("changePasswordWindow").close();
                            if (onDone) {
                                onDone();
                            }
                        } catch (error) {
                            webix.message({ type: "error", text: error.message || "Не удалось сменить пароль" });
                            form.enable();
                            form.hideProgress();
                        }
                    }
                }
            ]
        }
    };
}
