import { test } from "node:test";
import assert from "node:assert/strict";
import { createServerTab } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/serverTab.js";
import { createVetisTab } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/vetisTab.js";

function outline(node, found = []) {
    if (!node || typeof node !== "object")
        return found;

    if (node.view === "label")
        found.push(["section", node.label]);
    else if (node.name)
        found.push(["field", node.name, node.value]);

    if (node.body)
        outline(node.body, found);

    for (const key of ["elements", "rows", "cols", "cells"]) {
        for (const child of node[key] ?? [])
            outline(child, found);
    }

    return found;
}

test("вкладка настроек сервера делится на секции", () => {
    const tab = createServerTab({
        apiIpPort: 2579,
        publicAddress: "https://central.example:2579",
        corsOrigins: "https://app.example",
        trustedProxies: "10.0.0.2",
        allowLegacyAgentApi: false,
        isEnabled: true,
        logDepth: 14,
        logLevel: "Information",
    });

    assert.equal(tab.header, "Настройки сервера");
    assert.equal(
        find(tab.body, (node) => node.name === "publicAddress").label,
        "Адрес этого сервера для клиента"
    );
    assert.deepEqual(outline(tab.body), [
        ["section", "Настройка API сервера"],
        ["field", "apiIpPort", 2579],
        ["section", "Дистрибутив клиента"],
        ["field", "publicAddress", "https://central.example:2579"],
        ["section", "Безопасность"],
        ["field", "corsOrigins", "https://app.example"],
        ["field", "trustedProxies", "10.0.0.2"],
        ["field", "allowLegacyAgentApi", false],
        ["section", "Логирование"],
        ["field", "isEnabled", true],
        ["field", "logDepth", 14],
        ["field", "logLevel", "Information"],
    ]);
});

function find(node, predicate) {
    if (!node || typeof node !== "object")
        return null;

    if (predicate(node))
        return node;

    const nested = node.body ? find(node.body, predicate) : null;
    if (nested)
        return nested;

    for (const key of ["elements", "rows", "cols", "cells"]) {
        for (const child of node[key] ?? []) {
            const match = find(child, predicate);
            if (match)
                return match;
        }
    }

    return null;
}

test("при выключенном логировании глубина и уровень недоступны", () => {
    const tab = createServerTab({
        apiIpPort: 2579,
        publicAddress: "",
        corsOrigins: "",
        trustedProxies: "",
        allowLegacyAgentApi: true,
        isEnabled: false,
        logDepth: 14,
        logLevel: "Warning",
    });

    const fields = find(tab.body, (node) => node.id === "loggerSettingsFields");

    assert.equal(fields.disabled, true);
    assert.deepEqual(
        fields.rows.map((row) => row.name),
        ["logDepth", "logLevel"]
    );
});

test("переключатель логирования открывает глубину и уровень", () => {
    const tab = createServerTab({
        apiIpPort: 2579,
        publicAddress: "",
        corsOrigins: "",
        trustedProxies: "",
        allowLegacyAgentApi: true,
        isEnabled: false,
        logDepth: 14,
        logLevel: "Warning",
    });
    const checkbox = find(tab.body, (node) => node.name === "isEnabled");
    const calls = [];
    globalThis.$$ = (id) => id === "loggerSettingsFields"
        ? {
            enable() { calls.push("enable"); },
            disable() { calls.push("disable"); },
        }
        : null;

    try {
        checkbox.on.onChange(true);
        checkbox.on.onChange(false);
    }
    finally {
        delete globalThis.$$;
    }

    assert.deepEqual(calls, ["enable", "disable"]);
});

test("вкладка Ветис содержит параметры сервиса", () => {
    const tab = createVetisTab({
        enable: false,
        endpoint: "https://api.vetrf.ru/platform/services/3.0/ApplicationManagementService",
        incomingFetchIntervalMinutes: 5,
        incomingFetchPageSize: 100,
        applicationRetentionDays: 1,
        failedApplicationRetentionDays: 7,
    });

    assert.equal(tab.header, "Ветис");
    assert.deepEqual(
        outline(tab.body).filter((item) => item[0] === "field"),
        [
            ["field", "vetisEnable", false],
            ["field", "vetisEndpoint", "https://api.vetrf.ru/platform/services/3.0/ApplicationManagementService"],
            ["field", "vetisIncomingInterval", 5],
            ["field", "vetisIncomingPageSize", 100],
            ["field", "vetisApplicationRetention", 1],
            ["field", "vetisFailedApplicationRetention", 7],
        ]
    );

    for (const name of ["vetisEndpoint", "vetisIncomingInterval", "vetisIncomingPageSize", "vetisApplicationRetention", "vetisFailedApplicationRetention"])
        assert.equal(find(tab.body, (node) => node.name === name).disabled, true);

    const hint = find(tab.body, (node) => node.view === "template");
    assert.match(hint.template, /вкладка «Ветис» в организации/);
});

test("флажок Ветис открывает адрес и параметры загрузки", () => {
    const tab = createVetisTab({
        enable: false,
        endpoint: "https://api.vetrf.ru/platform/services/3.0/ApplicationManagementService",
        incomingFetchIntervalMinutes: 5,
        incomingFetchPageSize: 100,
        applicationRetentionDays: 1,
        failedApplicationRetentionDays: 7,
    });
    const checkbox = find(tab.body, (node) => node.name === "vetisEnable");
    const calls = [];
    globalThis.$$ = (id) => ({
        enable() { calls.push(["enable", id]); },
        disable() { calls.push(["disable", id]); },
    });

    try {
        checkbox.on.onChange(true);
        checkbox.on.onChange(false);
    }
    finally {
        delete globalThis.$$;
    }

    assert.deepEqual(calls, [
        ["enable", "vetisEndpoint"],
        ["enable", "vetisIncomingInterval"],
        ["enable", "vetisIncomingPageSize"],
        ["enable", "vetisApplicationRetention"],
        ["enable", "vetisFailedApplicationRetention"],
        ["disable", "vetisEndpoint"],
        ["disable", "vetisIncomingInterval"],
        ["disable", "vetisIncomingPageSize"],
        ["disable", "vetisApplicationRetention"],
        ["disable", "vetisFailedApplicationRetention"],
    ]);
});

test("параметры Ветис требуют адрес http и числа больше нуля", () => {
    const tab = createVetisTab({
        enable: true,
        endpoint: "https://api.vetrf.ru/platform/services/3.0/ApplicationManagementService",
        incomingFetchIntervalMinutes: 5,
        incomingFetchPageSize: 100,
        applicationRetentionDays: 1,
        failedApplicationRetentionDays: 7,
    });
    const endpoint = find(tab.body, (node) => node.name === "vetisEndpoint");
    const interval = find(tab.body, (node) => node.name === "vetisIncomingInterval");

    assert.equal(endpoint.validate("ftp://vetis"), false);
    assert.equal(endpoint.validate("https://api.vetrf.ru/platform"), true);
    assert.equal(interval.validate("0"), false);
    assert.equal(interval.validate("3"), true);
    assert.equal(interval.invalidMessage, "Нужно целое число больше нуля");
});

test("кнопка сохранения стоит над вкладками сервера и Ветис", async () => {
    const { createServerSettingsRows } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/serverSettingsView.js");
    const rows = createServerSettingsRows(
        { header: "Настройки сервера", body: {} },
        { header: "Ветис", body: {} }
    );

    assert.equal(rows[0].cols[0].value, "Сохранить");
    assert.equal(rows[0].cols[0].css, "webix_primary");
    assert.equal(rows[1].view, "tabview");
    assert.deepEqual(rows[1].cells.map((cell) => cell.header), ["Настройки сервера", "Ветис"]);
});

test("кнопка сохранения вызывает переданный обработчик", async () => {
    const { createServerSettingsRows } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/serverSettingsView.js");
    let called = false;
    const rows = createServerSettingsRows(
        { header: "Настройки сервера", body: {} },
        { header: "Ветис", body: {} },
        () => { called = true; }
    );

    rows[0].cols[0].click();

    assert.equal(called, true);
});

test("сохранение раскладывает значения по секциям конфигурации", async () => {
    const { buildConfigurationUpdaters, VETIS_DEFAULT_ENDPOINT } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/saveSettings.js");
    const values = {
        apiIpPort: "2580",
        publicAddress: " https://central.example ",
        corsOrigins: " https://a \n\n https://b \r\n",
        trustedProxies: " 10.0.0.2 \n ",
        allowLegacyAgentApi: 0,
        isEnabled: 1,
        logDepth: "30",
        logLevel: "Warning",
        vetisEnable: 1,
        vetisEndpoint: "  ",
        vetisIncomingInterval: "6",
        vetisIncomingPageSize: "50",
        vetisApplicationRetention: "2",
        vetisFailedApplicationRetention: "8",
    };
    const updaters = buildConfigurationUpdaters(values);

    assert.deepEqual(updaters.serverSettings({}), {
        apiIpPort: 2580,
        publicAddress: "https://central.example",
        corsOrigins: ["https://a", "https://b"],
        trustedProxies: ["10.0.0.2"],
    });
    assert.deepEqual(updaters.loggerSettings({}), {
        isEnabled: true,
        logDepth: 30,
        logLevel: "Warning",
    });
    assert.deepEqual(updaters.security({ keep: true }), {
        keep: true,
        allowLegacyAgentApi: false,
    });
    assert.deepEqual(updaters.vetisSettings({ timeoutSeconds: 60 }), {
        timeoutSeconds: 60,
        enable: true,
        endpoint: VETIS_DEFAULT_ENDPOINT,
        incomingFetchIntervalMinutes: 6,
        incomingFetchPageSize: 50,
        applicationRetentionDays: 2,
        failedApplicationRetentionDays: 8,
    });

    const kept = buildConfigurationUpdaters({
        ...values,
        vetisEndpoint: " https://api.vetrf.ru/custom ",
    }).vetisSettings({});
    assert.equal(kept.endpoint, "https://api.vetrf.ru/custom");
});

test("загрузка подставляет адрес Ветис и разрешает старый API, если их нет в конфигурации", async () => {
    const { mapServerSettings } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/loadedSettings.js");
    const { VETIS_DEFAULT_ENDPOINT } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/saveSettings.js");
    const mapped = mapServerSettings({
        serverSettings: {
            apiIpPort: 2579,
            publicAddress: "",
            corsOrigins: ["https://a", "https://b"],
            trustedProxies: ["10.0.0.2"],
        },
        loggerSettings: {
            isEnabled: false,
            logLevel: "Error",
            logDepth: 3,
        },
    });

    assert.equal(mapped.server.allowLegacyAgentApi, true);
    assert.equal(mapped.server.corsOrigins, "https://a\nhttps://b");
    assert.equal(mapped.server.trustedProxies, "10.0.0.2");
    assert.equal(mapped.server.isEnabled, false);
    assert.equal(mapped.vetis.enable, false);
    assert.equal(mapped.vetis.endpoint, VETIS_DEFAULT_ENDPOINT);
    assert.equal(mapped.vetis.incomingFetchIntervalMinutes, 5);
    assert.equal(mapped.vetis.incomingFetchPageSize, 100);
    assert.equal(mapped.vetis.applicationRetentionDays, 1);
    assert.equal(mapped.vetis.failedApplicationRetentionDays, 7);

    const explicit = mapServerSettings({
        serverSettings: { apiIpPort: 1, corsOrigins: [], trustedProxies: [] },
        loggerSettings: { isEnabled: true, logLevel: "Debug", logDepth: 1 },
        security: { allowLegacyAgentApi: false },
        vetisSettings: {
            enable: true,
            endpoint: " https://api.vetrf.ru/custom ",
            incomingFetchIntervalMinutes: 0,
            incomingFetchPageSize: 10,
            applicationRetentionDays: 2,
            failedApplicationRetentionDays: 4,
        },
    });
    assert.equal(explicit.server.allowLegacyAgentApi, false);
    assert.equal(explicit.vetis.endpoint, "https://api.vetrf.ru/custom");
    assert.equal(explicit.vetis.incomingFetchIntervalMinutes, 0);
});

test("у полей вкладок id совпадает с именем", () => {
    const server = createServerTab({
        apiIpPort: 2579,
        publicAddress: "",
        corsOrigins: "",
        trustedProxies: "",
        allowLegacyAgentApi: true,
        isEnabled: true,
        logDepth: 1,
        logLevel: "Information",
    });
    const vetis = createVetisTab({
        enable: true,
        endpoint: "https://api.vetrf.ru/platform",
        incomingFetchIntervalMinutes: 5,
        incomingFetchPageSize: 100,
        applicationRetentionDays: 1,
        failedApplicationRetentionDays: 7,
    });

    for (const tab of [server, vetis]) {
        const names = outline(tab.body)
            .filter((item) => item[0] === "field")
            .map((item) => item[1]);

        for (const name of names)
            assert.equal(find(tab.body, (node) => node.name === name).id, name, name);
    }
});

test("значения вкладок читаются по id, пропущенное поле не затирает секцию", async () => {
    const { readFieldValues } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/tabFields.js");
    globalThis.$$ = (id) => id === "apiIpPort" ? { getValue: () => "2579" } : null;

    try {
        assert.deepEqual(readFieldValues(["apiIpPort", "vetisEndpoint"]), { apiIpPort: "2579" });
    }
    finally {
        delete globalThis.$$;
    }
});

test("невалидная форма не сохраняет настройки", async () => {
    const { persistServerSettings } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/serverSettingsView.js");
    const { VETIS_DEFAULT_ENDPOINT } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/saveSettings.js");
    const field = {
        value: " ",
        getValue() { return this.value; },
        setValue(value) { this.value = value; },
    };
    let saved = false;

    const result = await persistServerSettings({
        endpointField: field,
        formsValid: () => false,
        readValues: () => ({}),
        saveSections: async () => { saved = true; return { result: true }; },
        showMenu() { saved = true; },
    });

    assert.equal(field.getValue(), VETIS_DEFAULT_ENDPOINT);
    assert.equal(saved, false);
    assert.equal(result.saved, false);
});

test("успешное сохранение показывает пункт меню Ветис", async () => {
    const { persistServerSettings } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/serverSettingsView.js");
    let menu = null;
    const result = await persistServerSettings({
        endpointField: null,
        formsValid: () => true,
        readValues: () => ({
            apiIpPort: "2579",
            publicAddress: "",
            corsOrigins: "",
            trustedProxies: "",
            allowLegacyAgentApi: true,
            isEnabled: false,
            logDepth: "7",
            logLevel: "Information",
            vetisEnable: true,
            vetisEndpoint: "https://api.vetrf.ru/custom",
            vetisIncomingInterval: "5",
            vetisIncomingPageSize: "100",
            vetisApplicationRetention: "1",
            vetisFailedApplicationRetention: "7",
        }),
        saveSections: async (updaters) => {
            assert.equal(updaters.vetisSettings({}).enable, true);
            assert.equal(updaters.loggerSettings({}).isEnabled, false);
            return { result: true };
        },
        showMenu(enabled) { menu = enabled; },
    });

    assert.equal(result.saved, true);
    assert.equal(menu, true);
});

test("ошибка сохранения не меняет пункт меню Ветис", async () => {
    const { persistServerSettings } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/serverSettingsView.js");
    let menu = "unchanged";
    const result = await persistServerSettings({
        endpointField: null,
        formsValid: () => true,
        readValues: () => ({
            apiIpPort: "2579",
            publicAddress: "",
            corsOrigins: "",
            trustedProxies: "",
            allowLegacyAgentApi: true,
            isEnabled: true,
            logDepth: "7",
            logLevel: "Information",
            vetisEnable: false,
            vetisEndpoint: "https://api.vetrf.ru/custom",
            vetisIncomingInterval: "5",
            vetisIncomingPageSize: "100",
            vetisApplicationRetention: "1",
            vetisFailedApplicationRetention: "7",
        }),
        saveSections: async () => ({ result: false, error: "нет связи" }),
        showMenu(enabled) { menu = enabled; },
    });

    assert.equal(result.saved, false);
    assert.equal(result.error, "нет связи");
    assert.equal(menu, "unchanged");
});
