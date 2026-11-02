import { incomingFromForSave, positiveSetting } from '../../utils/vetisFormat.js';
import { Text, CheckBox, PasswordBox, Number as NumberField } from '../../utils/ui.js';
import { newUuid } from '../../utils/uuid.js';
import { copyText } from '../../utils/copyText.js';
import { loadConfiguration } from '../../services/ConfigurationService.js';
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
            cryptoProHint: "Сертификат ставится на вкладке «Сервис». Здесь выбирается уже установленный.",
            gisMtTab: "Подключенные группы",
            gisMtGroupCode: "Код",
            gisMtGroupName: "Название",
            gisMtGroupTitle: "Название группы",
            markingTab: "Маркировка",
            vetisTab: "Ветис",
            vetisSettingsTab: "Настройка",
            vetisEnterprisesTab: "Предприятия",
            apiKey: "API-ключ",
            apiUser: "Пользователь API",
            apiPassword: "Пароль API",
            issuerId: "IssuerId",
            operatorLogin: "Логин оператора",
            passwordHint: "Пустое поле не стирает уже сохранённые API-ключ и пароль. На экран приходит маска.",
            incomingFrom: "Дата начала получения",
            incomingHint: "Если дата задана, включение галки подставляет её в пустую дату площадки.",
            autoQuench: "Автогашение",
            autoQuenchDelay: "Задержка перед автогашением (часов)",
            positiveNumber: "Нужно целое число больше нуля",
            presentation: "Представление",
            address: "Адрес",
            rowDate: "Дата начала",
            check: "Проверить соединение",
            enterprises: "Получить площадки",
            selectAll: "Выделить все",
            clearAll: "Снять все",
            pasteLetter: "Вставить из письма",
            letterTitle: "Реквизиты из письма",
            fillLetter: "Заполнить поля",
            letter: "Текст письма с реквизитами",
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
            saveButton: "organizationSaveButton",
            cancelButton: "organizationCancelButton",
            trueApiFields: "TrueApiIntegrationFields",
            tabs: "organizationTabs",
            trueApiTab: "organizationTrueApiTab",
            gisMtTab: "organizationGisMtTab",
            gisMtGroupsTable: "organizationGisMtGroupsTable",
            vetisEnterprises: "organizationVetisEnterprises",
            vetisIncomingFrom: "vetisIncomingFrom",
            vetisLetterWindow: "vetisLetterWindow",
            vetisLetter: "vetisLetter",
            vetisLetterStatus: "vetisLetterStatus",
            vetisEnable: "OrganizationVetisEnable",
            vetisFields: "OrganizationVetisFields",
            vetisSettingsScroll: "organizationVetisSettingsScroll",
            vetisTabs: "organizationVetisTabs",
            vetisEnterprisesTab: "organizationVetisEnterprisesTab",
            vetisStatus: "vetisProbeStatus",
            apiKey: "vetisApiKey",
            apiUser: "vetisApiUser",
            apiPassword: "vetisApiPassword",
            issuerId: "vetisIssuerId",
            operatorLogin: "vetisOperatorLogin",
            autoQuench: "organizationVetisAutoQuench",
            autoQuenchDelay: "organizationVetisAutoQuenchDelay"
        };
    }

    async showDialog(editedData = {}, onSuccess, onClose) {
        this.elementId = editedData.id || newUuid();
        const isNew = !editedData.id;
        const trueApi = editedData.trueApiIntegrationSettings || {};
        const groups = editedData.gisMtProductGroups || [];
        const vetis = editedData.vetisSettings || {};
        this._loadedVetis = editedData.vetisSettings || null;
        const vetisOn = await this._vetisEnabled();

        if ($$(this.NAMES.windowId)) {
            $$(this.NAMES.windowId).destructor();
        }

        webix.ui({
            view: "window",
            id: this.NAMES.windowId,
            modal: true,
            move: false,
            resize: false,
            width: Math.min(window.innerWidth * 0.9, 1040),
            height: Math.min(window.innerHeight * 0.9, 820),
            position: "center",
            on: {
                onDestruct: () => {
                    this._closeLetter();
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
                scroll: true,
                elements: [
                    Text(this.LABELS.name, this.NAMES.name, editedData.name || "", {
                        required: true,
                        invalidMessage: this.LABELS.invalidNameMessage
                    }),
                    Text(this.LABELS.inn, this.NAMES.inn, editedData.inn || "", {
                        required: true,
                        invalidMessage: this.LABELS.invalidInnMessage
                    }),
                    vetisOn ? this._tabs(trueApi, groups, vetis) : this._markingBody(trueApi, groups),
                    this._createButtons(onSuccess, onClose)
                ]
            }
        }).show();

        await this._loadCertificates(trueApi.digitalSignature || "");
        this._setTrueApiFieldsEnabled(!!trueApi.enable);
        if (vetisOn) {
            this._showEnterprises(vetis.enterprises || []);
            this._setVetisFieldsEnabled(!!vetis.enable);
        }

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

            await copyText(token);
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

    _tabs(trueApi, groups, vetis) {
        return {
            view: "tabview",
            id: this.NAMES.tabs,
            height: 560,
            cells: [
                { header: this.LABELS.markingTab, body: this._markingBody(trueApi, groups) },
                { header: this.LABELS.vetisTab, body: this._vetisBody(vetis) }
            ]
        };
    }

    _markingBody(trueApi, groups) {
        return {
            id: this.NAMES.trueApiTab,
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
                },
                { view: "label", label: this.LABELS.gisMtTab },
                this._groupsTable(groups)
            ]
        };
    }

    _vetisBody(vetis) {
        const view = this;
        return {
            rows: [
                {
                    view: "tabview",
                    id: this.NAMES.vetisTabs,
                    cells: [
                        { header: this.LABELS.vetisSettingsTab, body: this._vetisSettingsBody(vetis) },
                        { header: this.LABELS.vetisEnterprisesTab, body: this._vetisEnterprises() }
                    ]
                },
                {
                    view: "template",
                    id: this.NAMES.vetisStatus,
                    height: 36,
                    borderless: true,
                    template: ""
                }
            ],
            on: {
                onViewShow() {
                    $$(view.NAMES.vetisSettingsScroll)?.resize();
                    $$(view.NAMES.vetisEnterprises)?.resize();
                }
            }
        };
    }

    _vetisSettingsBody(vetis) {
        return {
            view: "scrollview",
            id: this.NAMES.vetisSettingsScroll,
            scroll: "y",
            body: {
                padding: 10,
                rows: [
                    CheckBox(this.LABELS.enable, this.NAMES.vetisEnable, {
                        value: !!vetis.enable,
                        on: {
                            onChange: (enabled) => this._setVetisFieldsEnabled(enabled)
                        }
                    }),
                    {
                        id: this.NAMES.vetisFields,
                        disabled: !vetis.enable,
                        rows: [
                    {
                        cols: [
                            {
                                view: "button",
                                value: this.LABELS.pasteLetter,
                                width: 200,
                                click: () => this._openLetter()
                            },
                            {}
                        ]
                    },
                    Text(this.LABELS.apiKey, this.NAMES.apiKey, vetis.apiKey || ""),
                    Text(this.LABELS.apiUser, this.NAMES.apiUser, vetis.basicLogin || ""),
                    PasswordBox(this.LABELS.apiPassword, this.NAMES.apiPassword, {
                        value: vetis.basicPassword || ""
                    }),
                    {
                        view: "template",
                        template: this.LABELS.passwordHint,
                        autoheight: true,
                        borderless: true
                    },
                    Text(this.LABELS.issuerId, this.NAMES.issuerId, vetis.issuerId || ""),
                    Text(this.LABELS.operatorLogin, this.NAMES.operatorLogin, vetis.initiatorLogin || ""),
                    {
                        view: "datepicker",
                        id: this.NAMES.vetisIncomingFrom,
                        label: this.LABELS.incomingFrom,
                        labelPosition: "top",
                        editable: true,
                        format: "%d.%m.%Y",
                        value: this._toDate(vetis.incomingFrom)
                    },
                    {
                        view: "template",
                        template: this.LABELS.incomingHint,
                        autoheight: true,
                        borderless: true
                    },
                    CheckBox(this.LABELS.autoQuench, this.NAMES.autoQuench, {
                        value: !!vetis.autoQuench
                    }),
                    NumberField(this.LABELS.autoQuenchDelay, this.NAMES.autoQuenchDelay, vetis.autoQuenchDelayHours ?? 1, undefined, {
                        validate: (input) => positiveSetting(input),
                        invalidMessage: this.LABELS.positiveNumber
                    }),
                    {
                        cols: [
                            { view: "button", value: this.LABELS.check, width: 210, click: () => this._probe("check") },
                            {}
                        ]
                    }
                        ]
                    }
                ]
            }
        };
    }

    _setVetisFieldsEnabled(enabled) {
        const fields = $$(this.NAMES.vetisFields);
        if (fields) {
            if (enabled)
                fields.enable();
            else
                fields.disable();
        }

        const tabs = $$(this.NAMES.vetisTabs);
        const tabbar = tabs?.getTabbar?.();
        if (!tabbar)
            return;

        if (enabled) {
            tabbar.enableOption(this.NAMES.vetisEnterprisesTab);
            return;
        }

        if (tabs.getValue() === this.NAMES.vetisEnterprisesTab)
            tabs.setValue(this.NAMES.vetisSettingsScroll);
        tabbar.disableOption(this.NAMES.vetisEnterprisesTab);
    }

    _vetisEnterprises() {
        const view = this;
        return {
            id: this.NAMES.vetisEnterprisesTab,
            padding: 10,
            rows: [
                {
                    cols: [
                        { view: "button", value: this.LABELS.enterprises, width: 190, click: () => this._probe("enterprises") },
                        { view: "button", value: this.LABELS.selectAll, width: 150, click: () => this._setEnterpriseSelection(true) },
                        { view: "button", value: this.LABELS.clearAll, width: 140, click: () => this._setEnterpriseSelection(false) },
                        {}
                    ]
                },
                this._enterprisesTable(),
            ],
            on: {
                onViewShow() {
                    const table = $$(view.NAMES.vetisEnterprises);
                    if (table)
                        table.resize();
                }
            }
        };
    }

    _enterprisesTable() {
        const view = this;
        return {
            view: "datatable",
            id: this.NAMES.vetisEnterprises,
            gravity: 1,
            scroll: "y",
            editable: true,
            editaction: "click",
            columns: [
                {
                    id: "participates",
                    header: "",
                    template: "{common.checkbox()}",
                    width: 40,
                    checkValue: true,
                    uncheckValue: false
                },
                { id: "presentation", header: this.LABELS.presentation, fillspace: 1, editor: "text" },
                { id: "address", header: this.LABELS.address, fillspace: 1.4 },
                {
                    id: "incomingFrom",
                    header: this.LABELS.rowDate,
                    width: 140,
                    editor: "date",
                    format: webix.Date.dateToStr("%d.%m.%Y")
                },
                {
                    id: "remove",
                    header: "",
                    width: 40,
                    template: "<span class='webix_icon wxi-trash' title='Удалить'></span>"
                }
            ],
            on: {
                onCheck(rowId, column, state) {
                    if (column !== "participates" || !state)
                        return;

                    view._fillEmptyDate(this.getItem(rowId), this);
                },
                onItemClick(cell) {
                    if (!cell)
                        return;

                    if (cell.column === "remove") {
                        view._confirmRemoveEnterprise(this, cell.row);
                        return;
                    }

                    if (cell.column !== "address")
                        return;

                    const row = this.getItem(cell.row);
                    if (row?.guid)
                        view._copyGuid(row.guid);
                }
            }
        };
    }

    _confirmRemoveEnterprise(table, rowId) {
        const row = table.getItem(rowId);
        const caption = this._confirmText(row?.presentation || "площадку");
        this._setOrganizationHotkeys(false);
        webix.confirm({
            title: "Вы уверены?",
            text: `Удалить «${caption}» из списка?`,
            ok: "Да",
            cancel: "Нет"
        }).then(() => {
            if (table.exists(rowId))
                table.remove(rowId);
        }).catch(() => {
            // «Нет» отклоняет промис webix.confirm.
        }).finally(() => {
            this._setOrganizationHotkeys(true);
        });
    }

    _confirmText(value) {
        return String(value || "площадку").trim()
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;") || "площадку";
    }

    async _vetisEnabled() {
        const configuration = await loadConfiguration();
        return !!configuration?.value?.Content?.vetisSettings?.enable;
    }

    _openLetter() {
        const opened = $$(this.NAMES.vetisLetterWindow);
        if (opened) {
            opened.show();
            $$(this.NAMES.vetisLetter)?.focus();
            return;
        }

        // Иначе Esc и Alt+Enter карточки срабатывают вместе с окном письма.
        this._setOrganizationHotkeys(false);
        const view = this;
        webix.ui({
            view: "window",
            id: this.NAMES.vetisLetterWindow,
            modal: true,
            move: true,
            position: "center",
            width: Math.min(window.innerWidth * 0.8, 640),
            head: {
                view: "toolbar",
                elements: [
                    { view: "label", label: this.LABELS.letterTitle },
                    {
                        view: "icon",
                        icon: "wxi-close",
                        click: () => view._closeLetter()
                    }
                ]
            },
            on: {
                onDestruct() {
                    view._setOrganizationHotkeys(true);
                }
            },
            body: {
                padding: 10,
                rows: [
                    {
                        view: "textarea",
                        id: this.NAMES.vetisLetter,
                        label: this.LABELS.letter,
                        labelPosition: "top",
                        height: 220
                    },
                    {
                        view: "template",
                        id: this.NAMES.vetisLetterStatus,
                        autoheight: true,
                        borderless: true,
                        hidden: true,
                        template: ""
                    },
                    {
                        padding: { top: 10 },
                        cols: [
                            {
                                view: "button",
                                value: this.LABELS.fillLetter,
                                width: 180,
                                css: "webix_primary",
                                hotkey: "alt+enter",
                                click: () => view._applyLetter()
                            },
                            {
                                view: "button",
                                value: this.LABELS.cancelButton,
                                width: 140,
                                hotkey: "esc",
                                click: () => view._closeLetter()
                            },
                            {}
                        ]
                    }
                ]
            }
        }).show();
        webix.delay(() => $$(this.NAMES.vetisLetter)?.focus());
    }

    _closeLetter() {
        const windowView = $$(this.NAMES.vetisLetterWindow);
        if (windowView)
            windowView.close();
    }

    _setOrganizationHotkeys(enabled) {
        const save = $$(this.NAMES.saveButton);
        const cancel = $$(this.NAMES.cancelButton);
        if (save)
            save.define("hotkey", enabled ? "alt+enter" : "");
        if (cancel)
            cancel.define("hotkey", enabled ? "esc" : "");
    }

    async _applyLetter() {
        const text = String($$(this.NAMES.vetisLetter)?.getValue() || "");
        try {
            const parsed = await organizationService.parseVetisLetter(text);
            this._setIfPresent(this.NAMES.apiUser, parsed.login);
            this._setIfPresent(this.NAMES.apiPassword, parsed.password);
            this._setIfPresent(this.NAMES.apiKey, parsed.apiKey);
            this._setIfPresent(this.NAMES.issuerId, parsed.issuerId);
            const found = ["login", "password", "apiKey", "issuerId"].filter(name => parsed[name]);
            if (!found.length) {
                this._setLetterStatus("В письме нет Login, Password, APIKey и IssuerID.", true);
                return;
            }

            this._setStatus("Заполнено из письма. Логин оператора в письме не приходит.", false);
            this._closeLetter();
        } catch (error) {
            this._setLetterStatus(error.message || "Не удалось разобрать письмо", true);
        }
    }

    _setLetterStatus(text, failed) {
        const status = $$(this.NAMES.vetisLetterStatus);
        if (!status)
            return;

        status.define("template", `<div style="color:${failed ? "#9b2335" : "#0f6b4c"};white-space:normal;">${this._escape(text)}</div>`);
        status.show();
        status.resize();
    }

    _setIfPresent(id, value) {
        if (!value)
            return;

        const field = $$(id);
        if (field)
            field.setValue(value);
    }

    async _probe(action) {
        this._setStatus("Запрос к Ветис...", false);
        try {
            const payload = this._vetisPayload();
            const result = action === "enterprises"
                ? await organizationService.listVetisEnterprises(this.elementId, payload)
                : await organizationService.checkVetis(this.elementId, payload);
            if (action === "enterprises" && result.ok) {
                this._showEnterprises(result.enterprises || []);
                const tabs = $$(this.NAMES.vetisTabs);
                if (tabs)
                    tabs.setValue(this.NAMES.vetisEnterprisesTab);
            }

            this._setStatus(result.message || "", !result.ok);
        } catch (error) {
            this._setStatus(this._errorText(error.message), true);
        }
    }

    _vetisPayload() {
        return {
            apiKey: String($$(this.NAMES.apiKey)?.getValue() || ""),
            basicLogin: String($$(this.NAMES.apiUser)?.getValue() || ""),
            basicPassword: String($$(this.NAMES.apiPassword)?.getValue() || ""),
            issuerId: String($$(this.NAMES.issuerId)?.getValue() || ""),
            initiatorLogin: String($$(this.NAMES.operatorLogin)?.getValue() || "")
        };
    }

    _showEnterprises(rows) {
        const table = $$(this.NAMES.vetisEnterprises);
        if (!table)
            return;

        table.clearAll();
        table.parse((rows || []).map(row => ({
            id: row.guid,
            guid: row.guid || "",
            name: row.name || "",
            address: row.address || "",
            presentation: row.presentation || "",
            incomingFrom: this._toDate(row.incomingFrom),
            loadedIncomingFrom: row.incomingFrom || "",
            participates: !!row.participates
        })));
    }

    _setEnterpriseSelection(selected) {
        const table = $$(this.NAMES.vetisEnterprises);
        if (!table)
            return;

        table.data.each(row => {
            row.participates = selected;
            if (selected && !row.incomingFrom) {
                const general = $$(this.NAMES.vetisIncomingFrom)?.getValue();
                if (general)
                    row.incomingFrom = general;
            }
        });
        table.refresh();
    }

    _fillEmptyDate(row, table) {
        if (!row || row.incomingFrom)
            return;

        const general = $$(this.NAMES.vetisIncomingFrom)?.getValue();
        if (!general)
            return;

        row.incomingFrom = general;
        if (table && row.id)
            table.updateItem(row.id, row);
    }

    async _copyGuid(guid) {
        try {
            await copyText(guid);
            webix.message("GUID скопирован");
        } catch (error) {
            webix.message({ text: error.message || "Не удалось скопировать GUID", type: "error" });
        }
    }

    _setStatus(text, failed) {
        const status = $$(this.NAMES.vetisStatus);
        if (!status)
            return;

        status.setHTML(`<div style="color:${failed ? "#9b2335" : "#0f6b4c"};white-space:normal;">${this._escape(text)}</div>`);
    }

    _escape(text) {
        return String(text || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;");
    }

    _errorText(raw) {
        const text = String(raw || "").trim();
        if (text.startsWith("\"") && text.endsWith("\"")) {
            try {
                return JSON.parse(text);
            } catch {
                return text;
            }
        }

        return text;
    }

    _toDate(value) {
        if (!value)
            return "";

        if (value instanceof Date)
            return value;

        const match = String(value).match(/^(\d{4})-(\d{2})-(\d{2})/);
        if (!match)
            return "";

        return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
    }

    _fromDate(value) {
        if (!value)
            return "";

        const date = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(date.getTime()))
            return "";

        const month = String(date.getMonth() + 1).padStart(2, "0");
        const day = String(date.getDate()).padStart(2, "0");
        return `${date.getFullYear()}-${month}-${day}`;
    }

    _groupsTable(groups) {
        return {
            view: "datatable",
            id: this.NAMES.gisMtGroupsTable,
            select: "row",
            columns: [
                { id: "code", header: this.LABELS.gisMtGroupCode, width: 80, sort: "int" },
                { id: "name", header: this.LABELS.gisMtGroupName, width: 160, sort: "string" },
                { id: "groupName", header: this.LABELS.gisMtGroupTitle, fillspace: true, sort: "string" }
            ],
            data: (groups || []).map((item, index) => ({
                id: item.name || item.code || index + 1,
                code: item.code,
                name: item.name || "",
                groupName: item.groupName || ""
            }))
        };
    }

    _createButtons(onSuccess, onClose) {
        return {
            padding: { top: 10 },
            cols: [
                {
                    view: "button",
                    id: this.NAMES.saveButton,
                    value: this.LABELS.createButton,
                    width: 200,
                    click: () => this._send(onSuccess),
                    hotkey: "alt+enter"
                },
                {
                    view: "button",
                    id: this.NAMES.cancelButton,
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
        const data = {
            id: this.elementId,
            name: $$(this.NAMES.name).getValue(),
            inn: String($$(this.NAMES.inn).getValue() || "").trim(),
            trueApiIntegrationSettings: {
                enable: !!$$(this.NAMES.enable).getValue(),
                password: $$(this.NAMES.password).getValue(),
                digitalSignature: $$(this.NAMES.digitalSignature).getValue()
            }
        };

        if ($$(this.NAMES.apiKey))
            data.vetisSettings = this._vetisSettings();
        else if (this._loadedVetis)
            // Вкладка не построена — отдаём настройки, с которыми открыли карточку.
            data.vetisSettings = this._loadedVetis;

        return data;
    }

    _vetisSettings() {
        const table = $$(this.NAMES.vetisEnterprises);
        const enterprises = table ? table.serialize().map(row => ({
            guid: row.guid || "",
            name: row.name || "",
            address: row.address || "",
            presentation: String(row.presentation || "").trim(),
            incomingFrom: incomingFromForSave(row.loadedIncomingFrom, this._fromDate(row.incomingFrom)),
            participates: !!row.participates
        })) : [];

        return {
            enable: !!$$(this.NAMES.vetisEnable)?.getValue(),
            apiKey: String($$(this.NAMES.apiKey)?.getValue() || ""),
            basicLogin: String($$(this.NAMES.apiUser)?.getValue() || ""),
            basicPassword: String($$(this.NAMES.apiPassword)?.getValue() || ""),
            issuerId: String($$(this.NAMES.issuerId)?.getValue() || ""),
            initiatorLogin: String($$(this.NAMES.operatorLogin)?.getValue() || ""),
            incomingFrom: this._fromDate($$(this.NAMES.vetisIncomingFrom)?.getValue()),
            autoQuench: !!$$(this.NAMES.autoQuench)?.getValue(),
            autoQuenchDelayHours: parseInt($$(this.NAMES.autoQuenchDelay)?.getValue(), 10),
            enterprises
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

        if ($$(this.NAMES.apiKey) && !positiveSetting(data.vetisSettings?.autoQuenchDelayHours)) {
            webix.message({ text: this.LABELS.positiveNumber, type: "error" });
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
