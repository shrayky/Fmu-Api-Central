import { Text, PasswordBox } from "../../utils/ui.js";
import { newUuid } from "../../utils/uuid.js";
import { positiveSetting, quenchWindow } from "../../utils/vetisFormat.js";
import { loadConfiguration } from "../../services/ConfigurationService.js";
import organizationService from "../../services/organizationService.js";
import { etrnTabState, organizationTabCells } from "./organizationEtrnAccess.js";
import { markingBody } from "./organizationMarkingTab.js";
import { setVetisFieldsEnabled, vetisBody, vetisSettings } from "./organizationVetisTab.js";
import { closeLetter, showEnterprises } from "./organizationVetisEnterprises.js";
import { etrnBody, etrnConnectionsFromTable, loadEtrnErrors, showEtrnConnections } from "./organizationEtrnTab.js";
import { closeImportWindow, closeSalePointWindow, salePointsBody, salePointsFromTable, showSalePoints } from "./organizationSalePoints.js";

class OrganizationElementView {
    constructor() {
        this.elementId = "";
        // Кнопка «Получить» появляется только при включённой интеграции с DaData.
        this._dadataEnabled = false;

        this.LABELS = {
            formTitle: "Организация",
            newOrg: "Новая организация",
            editOrg: "Организация",
            name: "Наименование",
            inn: "ИНН организации",
            enable: "Используется",
            trueApiEnable: "Использовать",
            DigitalSignature: "Сертификат ЭЦП",
            signPassword: "Пароль от ЭЦП",
            LoadToken: "Получить токен",
            trueApiSection: "True API",
            cryptoProHint: "Сертификат загружается на вкладке «Сервис». Здесь выбирается уже загруженный.",
            gisMtTab: "Подключенные группы",
            gisMtGroupCode: "Код",
            gisMtGroupName: "Название",
            gisMtGroupTitle: "Название группы",
            markingTab: "Маркировка",
            vetisTab: "Ветис",
            etrnTab: "ЭТрН",
            salePointsTab: "Торговые точки",
            salePointAdd: "Добавить",
            salePointEdit: "Торговая точка",
            salePointName: "Название",
            salePointAddress: "Адрес",
            salePointFias: "ФИАС",
            salePointFiasLoad: "Получить",
            salePointDadataTitle: "Выбор дома",
            salePointDadataSelect: "Выбрать",
            salePointAliases: "Синонимы адреса",
            salePointAliasesHeader: "Синонимы адреса",
            salePointAliasAdd: "Добавить синоним",
            salePointImport: "Загрузить",
            salePointImportTitle: "Загрузка торговых точек",
            salePointImportAddresses: "Адреса, по одному в строке",
            salePointImportHint: "Каждая строка — новая торговая точка. Название подставится из адреса.",
            salePointImportEmpty: "Вставьте хотя бы один адрес",
            salePointImportDuplicate: "Такие адреса уже есть в справочнике",
            salePointClosed: "Закрыта",
            salePointOk: "ОК",
            etrnType: "Тип",
            etrnName: "Название",
            etrnEnable: "Включено",
            etrnError: "Последняя ошибка",
            etrnAdd: "Добавить",
            etrnRemove: "Удалить",
            etrnCheck: "Проверить подключение",
            etrnIncomingFrom: "Дата начала",
            etrnEdoId: "Идентификатор ЭДО",
            etrnSaby: "Saby",
            etrnOfd: "Платформа ЭТрН",
            etrnSubscriber: "Абонент ЭДО",
            etrnApiKey: "API-ключ",
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
            autoQuenchFrom: "Автогашение с",
            autoQuenchTo: "Автогашение по",
            autoQuenchWindow: "Рабочий день автогашения: начало должно быть раньше конца",
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

        this.SIZES = {
            maxWidth: 1280,
            maxHeight: 1080,
            viewportMargin: 24,
            minTabview: 360,
            // Шапка, наименование, ИНН, сертификат, пароль, подсказка, кнопки и отступы.
            tabviewChrome: 450
        };

        this.NAMES = {
            windowId: "organizationWindow",
            formId: "organizationElement",
            name: "OrganizationName",
            inn: "OrganizationInn",
            enable: "TrueApiIntegrationEnable",
            digitalSignature: "TrueApiIntegrationDigitalSignature",
            password: "TrueApiIntegrationPassword",
            trueApiSection: "organizationTrueApiSection",
            loadToken: "loadTrueApiToken",
            trueApiTokenStatus: "organizationTrueApiTokenStatus",
            saveButton: "organizationSaveButton",
            cancelButton: "organizationCancelButton",
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
            autoQuenchDelay: "organizationVetisAutoQuenchDelay",
            autoQuenchFrom: "organizationVetisAutoQuenchFrom",
            autoQuenchTo: "organizationVetisAutoQuenchTo",
            etrnTab: "organizationEtrnTab",
            salePointsTab: "organizationSalePointsTab",
            salePointsTable: "organizationSalePointsTable",
            salePointWindow: "organizationSalePointWindow",
            salePointName: "organizationSalePointName",
            salePointAddress: "organizationSalePointAddress",
            salePointFias: "organizationSalePointFias",
            salePointFiasLoad: "organizationSalePointFiasLoad",
            salePointFiasSpinner: "organizationSalePointFiasSpinner",
            salePointDadataWindow: "organizationSalePointDadataWindow",
            salePointDadataList: "organizationSalePointDadataList",
            salePointAliases: "organizationSalePointAliases",
            salePointClosed: "organizationSalePointClosed",
            salePointImportWindow: "organizationSalePointImportWindow",
            salePointImport: "organizationSalePointImport",
            etrnConnections: "organizationEtrnConnections",
            etrnOfdForm: "organizationEtrnOfdForm",
            etrnSabyForm: "organizationEtrnSabyForm",
            etrnEdoId: "organizationEtrnEdoId",
            etrnOfdSubscriber: "organizationEtrnOfdSubscriber",
            etrnOfdApiKey: "organizationEtrnOfdApiKey",
            etrnOfdClientId: "organizationEtrnOfdClientId",
            etrnOfdClientSecret: "organizationEtrnOfdClientSecret",
            etrnOfdKeycloak: "organizationEtrnOfdKeycloak",
            etrnOfdRealm: "organizationEtrnOfdRealm",
            etrnOfdApi: "organizationEtrnOfdApi",
            etrnCheck: "organizationEtrnCheck",
            etrnProbeStatus: "organizationEtrnProbeStatus"
        };
    }

    async showDialog(editedData = {}, onSuccess, onClose) {
        this.elementId = editedData.id || newUuid();
        const isNew = !editedData.id;
        const windowHeight = this._windowHeight();
        const trueApi = editedData.trueApiIntegrationSettings || {};
        const groups = editedData.gisMtProductGroups || [];
        const vetis = editedData.vetisSettings || {};
        this._loadedVetis = editedData.vetisSettings || null;
        this._loadedEtrn = editedData.etrnConnections || [];
        this._loadedSalePoints = editedData.salePoints || [];
        const configuration = await loadConfiguration();
        const content = configuration?.value?.Content;
        const vetisOn = !!content?.vetisSettings?.enable;
        const etrnOn = content?.etrnSettings?.enable === true;
        this._dadataEnabled = content?.dadataSettings?.enable === true;

        if ($$(this.NAMES.windowId)) {
            $$(this.NAMES.windowId).destructor();
        }

        webix.ui({
            view: "window",
            id: this.NAMES.windowId,
            modal: true,
            move: false,
            resize: false,
            width: Math.min(window.innerWidth - this.SIZES.viewportMargin * 2, this.SIZES.maxWidth),
            height: windowHeight,
            position: "center",
            on: {
                onDestruct: () => {
                    closeLetter(this);
                    closeSalePointWindow(this);
                    closeImportWindow(this);
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
                    ...this._certificateFields(trueApi),
                    this._tabs(groups, vetis, windowHeight, vetisOn, trueApi, {
                        received: !!editedData.trueApiTokenReceived,
                        expired: editedData.trueApiTokenExpired || null
                    }, etrnOn),
                    this._createButtons(onSuccess, onClose)
                ]
            }
        }).show();

        this._setEtrnTabEnabled("");
        await this._loadCertificates(trueApi.digitalSignature || "");
        this._setEtrnTabEnabled($$(this.NAMES.digitalSignature)?.getValue());
        if (vetisOn) {
            showEnterprises(this, vetis.enterprises || []);
            setVetisFieldsEnabled(this, !!vetis.enable);
        }

        showEtrnConnections(this, this._loadedEtrn);
        showSalePoints(this, this._loadedSalePoints);
        loadEtrnErrors(this);

        setTimeout(() => {
            const nameField = $$(this.NAMES.name);
            if (nameField) {
                nameField.focus();
            }
        }, 100);
    }

    _certificateFields(trueApi) {
        return [
            {
                view: "richselect",
                id: this.NAMES.digitalSignature,
                label: this.LABELS.DigitalSignature,
                labelPosition: "top",
                placeholder: "Выберите сертификат",
                options: [],
                on: {
                    onChange: (value) => this._setEtrnTabEnabled(value)
                }
            },
            PasswordBox(this.LABELS.signPassword, this.NAMES.password, {
                value: trueApi.password || ""
            }),
            {
                view: "template",
                template: this.LABELS.cryptoProHint,
                autoheight: true,
                borderless: true,
                css: {
                    "white-space": "normal",
                    "word-wrap": "break-word",
                    "line-height": "1.4",
                    "padding": "5px 0"
                }
            }
        ];
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

    _setEtrnTabEnabled(certificateNumber) {
        const tabs = $$(this.NAMES.tabs);
        const tabbar = tabs?.getTabbar?.();
        if (!tabs || !tabbar)
            return;

        if (typeof tabbar.getOption === "function" && !tabbar.getOption(this.NAMES.etrnTab))
            return;

        const state = etrnTabState(
            certificateNumber,
            tabs.getValue(),
            this.NAMES.trueApiTab,
            this.NAMES.etrnTab
        );
        if (!state.enabled && tabs.getValue() === this.NAMES.etrnTab)
            tabs.setValue(state.tab);

        if (state.enabled)
            tabbar.enableOption(this.NAMES.etrnTab);
        else
            tabbar.disableOption(this.NAMES.etrnTab);
    }
    _windowHeight() {
        return Math.min(window.innerHeight - this.SIZES.viewportMargin, this.SIZES.maxHeight);
    }


    _tabviewHeight(windowHeight) {
        return Math.max(this.SIZES.minTabview, windowHeight - this.SIZES.tabviewChrome);
    }

    _tabs(groups, vetis, windowHeight, vetisOn = true, trueApi = {}, token = {}, etrnOn = true) {
        return {
            view: "tabview",
            id: this.NAMES.tabs,
            height: this._tabviewHeight(windowHeight),
            cells: organizationTabCells(
                { header: this.LABELS.salePointsTab, body: salePointsBody(this) },
                { header: this.LABELS.markingTab, body: markingBody(this, groups, trueApi, token) },
                { header: this.LABELS.vetisTab, body: vetisOn ? vetisBody(this, vetis) : {} },
                { header: this.LABELS.etrnTab, body: etrnOn ? etrnBody(this) : {} },
                vetisOn,
                etrnOn
            )
        };
    }

    _setOrganizationHotkeys(enabled) {
        const save = $$(this.NAMES.saveButton);
        const cancel = $$(this.NAMES.cancelButton);
        if (save)
            save.define("hotkey", enabled ? "alt+enter" : "");
        if (cancel)
            cancel.define("hotkey", enabled ? "esc" : "");
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
            },
            etrnConnections: etrnConnectionsFromTable(this),
            salePoints: $$(this.NAMES.salePointsTable)
                ? salePointsFromTable(this)
                : this._loadedSalePoints
        };

        if ($$(this.NAMES.apiKey))
            data.vetisSettings = vetisSettings(this);
        else if (this._loadedVetis)
            // Вкладка не построена — отдаём настройки, с которыми открыли карточку.
            data.vetisSettings = this._loadedVetis;

        return data;
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

        if ($$(this.NAMES.apiKey) && !quenchWindow(data.vetisSettings)) {
            webix.message({ text: this.LABELS.autoQuenchWindow, type: "error" });
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
