import { Text, CheckBox, PasswordBox } from '../../utils/ui.js';
import organizationService from '../../services/organizationService.js';

class OrganizationElementView {
    constructor() {
        this.elementId = "";

        this.LABELS = {
            formTitle: "Организация",
            newOrg: "Новая организация",
            editOrg: "Организация",
            name: "Наименование",
            inn: "ИНН организации",
            enable: "Используется",
            DigitalSignature: "Сертификат ЭЦП",
            signPassword: "Пароль от ЭЦП",
            LoadToken: "Получить токен",
            cryptoProHint: "Для работы необходимо, чтобы на одном ПК с fmu-api-central был установлен КриптоПро, а также у пользователя, от которого запущена служба, был установлен сертификат ЭЦП.",
            invalidNameMessage: "заполните наименование",
            invalidInnMessage: "заполните ИНН",
            saveFirst: "Сначала сохраните организацию",
            createButton: "Сохранить",
            cancelButton: "Отмена"
        };

        this.NAMES = {
            windowId: "organizationWindow",
            formId: "organizationElement",
            name: "OrganizationName",
            inn: "OrganizationInn",
            enable: "TrueApiIntegrationEnable",
            digitalSignature: "TrueApiIntegrationDigitalSignature",
            password: "TrueApiIntegrationPassword",
            loadToken: "loadTrueApiToken",
            trueApiFields: "TrueApiIntegrationFields"
        };
    }

    async showDialog(editedData = {}, onSuccess, onClose) {
        this.elementId = editedData.id || crypto.randomUUID();
        const isNew = !editedData.id;
        const trueApi = editedData.trueApiIntegrationSettings || {};

        if ($$(this.NAMES.windowId)) {
            $$(this.NAMES.windowId).destructor();
        }

        webix.ui({
            view: "window",
            id: this.NAMES.windowId,
            modal: true,
            move: false,
            resize: false,
            width: Math.min(window.innerWidth * 0.8, 900),
            position: "center",
            on: {
                onDestruct: () => {
                    if (onClose) {
                        onClose();
                    }
                }
            },
            head: {
                view: "toolbar",
                elements: [
                    { view: "label", label: isNew ? this.LABELS.newOrg : this.LABELS.editOrg },
                    {
                        view: "icon",
                        icon: "wxi-close",
                        click: () => $$(this.NAMES.windowId).close()
                    }
                ]
            },
            body: {
                view: "form",
                id: this.NAMES.formId,
                padding: 10,
                elements: [
                    Text(this.LABELS.name, this.NAMES.name, editedData.name || "", {
                        required: true,
                        invalidMessage: this.LABELS.invalidNameMessage
                    }),
                    Text(this.LABELS.inn, this.NAMES.inn, editedData.inn || "", {
                        required: true,
                        invalidMessage: this.LABELS.invalidInnMessage
                    }),
                    {
                        view: "tabview",
                        height: 300,
                        cells: [
                            {
                                header: "True api интеграция",
                                body: {
                                    padding: 10,
                                    rows: [
                                        CheckBox(this.LABELS.enable, this.NAMES.enable, {
                                            value: !!trueApi.enable,
                                            on: {
                                                onChange: (enabled) => this._setTrueApiFieldsEnabled(enabled)
                                            }
                                        }),
                                        {
                                            id: this.NAMES.trueApiFields,
                                            disabled: !trueApi.enable,
                                            rows: [
                                                {
                                                    view: "richselect",
                                                    id: this.NAMES.digitalSignature,
                                                    label: this.LABELS.DigitalSignature,
                                                    labelPosition: "top",
                                                    placeholder: "Выберите сертификат",
                                                    options: []
                                                },
                                                PasswordBox(this.LABELS.signPassword, this.NAMES.password, {
                                                    value: trueApi.password || ""
                                                }),
                                                {
                                                    cols: [
                                                        {
                                                            view: "button",
                                                            value: this.LABELS.LoadToken,
                                                            id: this.NAMES.loadToken,
                                                            width: 200,
                                                            click: () => this._loadToken()
                                                        },
                                                        {}
                                                    ]
                                                }
                                            ]
                                        },
                                        {
                                            view: "template",
                                            template: this.LABELS.cryptoProHint,
                                            css: {
                                                "white-space": "normal",
                                                "word-wrap": "break-word",
                                                "line-height": "1.4",
                                                "padding": "5px 0"
                                            },
                                            autoheight: true
                                        }
                                    ]
                                }
                            }
                        ]
                    },
                    this._createButtons(onSuccess, onClose)
                ]
            }
        }).show();

        await this._loadCertificates(trueApi.digitalSignature || "");
        this._setTrueApiFieldsEnabled(!!trueApi.enable);

        setTimeout(() => {
            const nameField = $$(this.NAMES.name);
            if (nameField) {
                nameField.focus();
            }
        }, 100);
    }

    async _loadCertificates(selectedNumber) {
        const control = $$(this.NAMES.digitalSignature);
        if (!control) {
            return;
        }

        try {
            const certificates = await organizationService.certificates();
            const options = certificates.map((certificate) => ({
                id: certificate.number,
                value: certificate.presentation
            }));

            const popup = control.getPopup();
            popup.getList().clearAll();
            popup.getList().parse(options);

            if (selectedNumber) {
                control.setValue(selectedNumber);
            }
        } catch (error) {
            webix.message({ text: error.message || "Не удалось загрузить сертификаты", type: "error" });
        }
    }

    async _loadToken() {
        const inn = String($$(this.NAMES.inn).getValue() || "").trim();
        if (!inn) {
            webix.message({ text: this.LABELS.invalidInnMessage, type: "error" });
            return;
        }

        const button = $$(this.NAMES.loadToken);
        if (button) {
            button.disable();
        }

        try {
            const data = await organizationService.getToken(inn);
            const token = data.token ?? data.Token ?? "";
            if (!token) {
                webix.message({ text: "Токен не получен", type: "error" });
                return;
            }

            await navigator.clipboard.writeText(token);
            webix.message("Токен получен и скопирован в буфер обмена");
        } catch (error) {
            const message = error.message || "Ошибка при получении токена";
            if (String(message).includes("Не найдена организация")) {
                webix.message({ text: this.LABELS.saveFirst, type: "error" });
                return;
            }

            webix.message({ text: message, type: "error" });
        } finally {
            this._setTrueApiFieldsEnabled(!!$$(this.NAMES.enable).getValue());
        }
    }

    _setTrueApiFieldsEnabled(enabled) {
        const fields = $$(this.NAMES.trueApiFields);
        if (!fields) {
            return;
        }

        if (enabled) {
            fields.enable();
            return;
        }

        fields.disable();
    }

    _createButtons(onSuccess, onClose) {
        return {
            padding: { top: 10 },
            cols: [
                {
                    view: "button",
                    value: this.LABELS.createButton,
                    width: 200,
                    click: () => this._send(onSuccess),
                    hotkey: "alt+enter"
                },
                {
                    view: "button",
                    value: this.LABELS.cancelButton,
                    width: 200,
                    click: () => $$(this.NAMES.windowId).close(),
                    hotkey: "esc"
                },
                {}
            ]
        };
    }

    _collect() {
        return {
            id: this.elementId,
            name: $$(this.NAMES.name).getValue(),
            inn: String($$(this.NAMES.inn).getValue() || "").trim(),
            trueApiIntegrationSettings: {
                enable: !!$$(this.NAMES.enable).getValue(),
                password: $$(this.NAMES.password).getValue(),
                digitalSignature: $$(this.NAMES.digitalSignature).getValue()
            }
        };
    }

    _validate(data) {
        if (!data.name) {
            webix.message({ text: this.LABELS.invalidNameMessage, type: "error" });
            return false;
        }

        if (!data.inn) {
            webix.message({ text: this.LABELS.invalidInnMessage, type: "error" });
            return false;
        }

        return true;
    }

    async _send(onSuccess) {
        const form = $$(this.NAMES.formId);
        const data = this._collect();
        if (!this._validate(data)) {
            return;
        }

        webix.extend(form, webix.ProgressBar);
        form.showProgress({ type: "icon" });
        form.disable();

        try {
            await organizationService.create(data);

            if (onSuccess) {
                onSuccess({
                    ...data,
                    trueApiEnabled: !!data.trueApiIntegrationSettings.enable
                });
            }

            $$(this.NAMES.windowId).close();
        } catch (error) {
            webix.message({ text: error.message, type: "error" });
            form.enable();
            form.hideProgress();
        }
    }
}

export default new OrganizationElementView();
