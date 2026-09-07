import { Text, PasswordBox } from '../../utils/ui.js';
import usersService from '../../services/usersService.js';

class UserElementView {
    constructor() {
        this.elementId = "";

        this.LABELS = {
            name: "Имя",
            password: "Пароль",
            changePassword: "Сменить пароль",
            newPassword: "Новый пароль",
            confirmPassword: "Повторите пароль",
            passwordsMismatch: "Пароли не совпадают",
            passwordChanged: "Пароль изменён",
            invalidNameMessage: "укажите имя",
            invalidPasswordMessage: "укажите пароль",
            formTitle: "Пользователь",
            createButton: "Сохранить",
            cancelButton: "Отмена"
        };

        this.NAMES = {
            windowId: "userElementWindow",
            formId: "userElementForm",
            name: "userName",
            password: "userPassword",
            changePasswordBtn: "userChangePasswordBtn",
            passwordWindowId: "userPasswordWindow",
            passwordFormId: "userPasswordForm",
            newPassword: "userNewPassword",
            confirmPassword: "userConfirmPassword"
        };
    }

    showDialog(editedData = {}, onSuccess, onClose) {
        const isNew = !editedData.id;
        this.elementId = editedData.id || crypto.randomUUID();

        if ($$(this.NAMES.windowId)) {
            $$(this.NAMES.windowId).destructor();
        }

        const elements = [
            Text(
                this.LABELS.name,
                this.NAMES.name,
                editedData.name || "",
                { required: true, invalidMessage: this.LABELS.invalidNameMessage }
            )
        ];

        if (isNew) {
            elements.push(PasswordBox(
                this.LABELS.password,
                this.NAMES.password,
                { required: true, invalidMessage: this.LABELS.invalidPasswordMessage }
            ));
        } else {
            elements.push({
                view: "button",
                id: this.NAMES.changePasswordBtn,
                value: this.LABELS.changePassword,
                click: () => this._showChangePasswordDialog()
            });
        }

        elements.push(this._createButtons(isNew, onSuccess));

        webix.ui({
            view: "window",
            id: this.NAMES.windowId,
            modal: true,
            width: 480,
            position: "center",
            on: {
                onDestruct: () => {
                    if (onClose) {
                        onClose();
                    }
                }
            },
            head: this.LABELS.formTitle,
            body: {
                view: "form",
                id: this.NAMES.formId,
                elements
            }
        }).show();

        setTimeout(() => {
            const nameField = $$(this.NAMES.name);
            if (nameField) {
                nameField.focus();
            }
        }, 100);
    }

    _createButtons(isNew, onSuccess) {
        return {
            cols: [
                {},
                {
                    view: "button",
                    value: this.LABELS.createButton,
                    click: () => this._send(isNew, onSuccess),
                    hotkey: "alt+enter"
                },
                {
                    view: "button",
                    value: this.LABELS.cancelButton,
                    click: () => $$(this.NAMES.windowId).close(),
                    hotkey: "esc"
                }
            ]
        };
    }

    async _send(isNew, onSuccess) {
        const form = $$(this.NAMES.formId);
        if (!this._validate(isNew)) {
            return;
        }

        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        const data = {
            id: this.elementId,
            name: $$(this.NAMES.name).getValue(),
            password: isNew ? $$(this.NAMES.password).getValue() : ""
        };

        try {
            if (isNew) {
                await usersService.create(data);
            } else {
                await usersService.update(data);
            }

            if (onSuccess) {
                onSuccess({
                    id: data.id,
                    name: data.name
                });
            }

            $$(this.NAMES.windowId).close();
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            form.enable();
            form.hideProgress();
        }
    }

    _showChangePasswordDialog() {
        if ($$(this.NAMES.passwordWindowId)) {
            $$(this.NAMES.passwordWindowId).close();
        }

        webix.ui({
            view: "window",
            id: this.NAMES.passwordWindowId,
            modal: true,
            width: 400,
            position: "center",
            head: this.LABELS.changePassword,
            body: {
                view: "form",
                id: this.NAMES.passwordFormId,
                elements: [
                    PasswordBox(this.LABELS.newPassword, this.NAMES.newPassword, { required: true }),
                    PasswordBox(this.LABELS.confirmPassword, this.NAMES.confirmPassword, { required: true }),
                    {
                        cols: [
                            {},
                            {
                                view: "button",
                                value: this.LABELS.createButton,
                                click: () => this._sendPassword()
                            },
                            {
                                view: "button",
                                value: this.LABELS.cancelButton,
                                click: () => $$(this.NAMES.passwordWindowId).close()
                            }
                        ]
                    }
                ]
            }
        }).show();
    }

    async _sendPassword() {
        const form = $$(this.NAMES.passwordFormId);
        const password = ($$(this.NAMES.newPassword).getValue() || "").trim();
        const confirm = ($$(this.NAMES.confirmPassword).getValue() || "").trim();

        if (!password) {
            webix.message({ text: this.LABELS.invalidPasswordMessage, type: "error" });
            return;
        }

        if (password !== confirm) {
            webix.message({ text: this.LABELS.passwordsMismatch, type: "error" });
            return;
        }

        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        try {
            await usersService.changePassword(this.elementId, password);
            webix.message({ text: this.LABELS.passwordChanged, type: "success" });
            $$(this.NAMES.passwordWindowId).close();
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            form.enable();
            form.hideProgress();
        }
    }

    _validate(isNew) {
        const name = $$(this.NAMES.name).getValue();
        if (!name) {
            webix.message({ text: this.LABELS.invalidNameMessage, type: "error" });
            return false;
        }

        if (isNew) {
            const password = $$(this.NAMES.password).getValue();
            if (!password) {
                webix.message({ text: this.LABELS.invalidPasswordMessage, type: "error" });
                return false;
            }
        }

        return true;
    }
}

export default new UserElementView();
