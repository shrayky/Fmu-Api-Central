import test from "node:test";
import assert from "node:assert/strict";
import { etrnTabState } from "./organizationEtrnAccess.js";
import { markingBody, tokenStatusText } from "./organizationMarkingTab.js";
import { etrnBody } from "./organizationEtrnTab.js";

const marking = "organizationTrueApiTab";
const etrn = "organizationEtrnTab";

test("без сертификата ЭТрН недоступна и открытая вкладка уходит на маркировку", () => {
    const state = etrnTabState("", etrn, marking, etrn);
    assert.equal(state.enabled, false);
    assert.equal(state.tab, marking);
});

test("пустой сертификат не переключает другую вкладку", () => {
    const state = etrnTabState("   ", "vetis", marking, etrn);
    assert.equal(state.enabled, false);
    assert.equal(state.tab, "vetis");
});

test("выбранный сертификат оставляет ЭТрН доступной", () => {
    const state = etrnTabState("serial-1", etrn, marking, etrn);
    assert.equal(state.enabled, true);
    assert.equal(state.tab, etrn);
});

test("выключенная ЭТрН убирает вкладку организации", async () => {
    const { organizationTabCells } = await import("./organizationEtrnAccess.js");
    const cells = organizationTabCells(
        { header: "Маркировка" },
        { header: "Ветис" },
        { header: "ЭТрН" },
        true,
        false
    );
    assert.deepEqual(cells.map((cell) => cell.header), ["Маркировка", "Ветис"]);
});

test("включённая ЭТрН оставляет вкладку организации", async () => {
    const { organizationTabCells } = await import("./organizationEtrnAccess.js");
    const cells = organizationTabCells(
        { header: "Маркировка" },
        { header: "Ветис" },
        { header: "ЭТрН" },
        false,
        true
    );
    assert.deepEqual(cells.map((cell) => cell.header), ["Маркировка", "ЭТрН"]);
});

test("на маркировке блок True API и группы, без сертификата", () => {
    const body = markingBody(
        markingView(),
        [{ code: 1, name: "milk", groupName: "Молоко" }],
        { enable: true },
        { received: false }
    );
    const text = JSON.stringify(body);
    assert.match(text, /True API/);
    assert.match(text, /Использовать/);
    assert.doesNotMatch(text, /fieldset/);
    assert.equal(body.rows[1].padding.left, 20);
    assert.match(text, /Получить токен/);
    assert.match(text, /Токен не получен/);
    assert.match(text, /organizationGisMtGroupsTable/);
    assert.equal(findButton(body, "loadTrueApiToken").disabled, false);
    assert.doesNotMatch(text, /TrueApiIntegrationDigitalSignature|Пароль от ЭЦП/);
});

test("кнопка токена выключена, пока True API не используется", () => {
    const body = markingBody(markingView(), [], { enable: false }, { received: false });
    assert.equal(findButton(body, "loadTrueApiToken").disabled, true);
});

test("статус токена без получения", () => {
    assert.equal(tokenStatusText(false, null), "Токен не получен");
});

test("статус токена показывает срок", () => {
    const text = tokenStatusText(true, "2026-11-12T08:30:00");
    assert.match(text, /^Получен, до /);
    assert.match(text, /2026/);
    assert.match(text, /12/);
    assert.match(text, /08:30|8:30/);
});

test("у ЭТрН нет отсылки к сертификату на вкладке маркировки", () => {
    globalThis.webix = { Date: { dateToStr: () => () => "" } };
    const body = etrnBody(etrnView());
    assert.doesNotMatch(JSON.stringify(body), /вкладки «Маркировка»/);
});

test("иконка адреса API подставляет боевой адрес", () => {
    globalThis.webix = { Date: { dateToStr: () => () => "" } };
    const body = etrnBody(etrnView());
    const written = {};
    globalThis.$$ = (id) => ({ setValue(value) { written[id] = value; } });
    findButton(body, "etrnOfdApi").click();
    assert.equal(written.etrnOfdApi, "https://api.platformaofd.ru");
    assert.equal(findButton(body, "etrnOfdApiKey"), null);
});

function markingView() {
    return {
        LABELS: {
            enable: "Используется",
            trueApiEnable: "Использовать",
            LoadToken: "Получить токен",
            trueApiSection: "True API",
            tokenMissing: "Токен не получен",
            tokenReceived: "Получен, до",
            gisMtTab: "Подключенные группы",
            gisMtGroupCode: "Код",
            gisMtGroupName: "Название",
            gisMtGroupTitle: "Название группы"
        },
        NAMES: {
            trueApiTab: marking,
            enable: "TrueApiIntegrationEnable",
            trueApiSection: "organizationTrueApiSection",
            loadToken: "loadTrueApiToken",
            trueApiTokenStatus: "organizationTrueApiTokenStatus",
            gisMtGroupsTable: "organizationGisMtGroupsTable"
        }
    };
}

function findButton(config, id) {
    if (!config || typeof config !== "object")
        return null;
    if (config.id === id)
        return config;
    for (const value of Object.values(config)) {
        const found = Array.isArray(value)
            ? value.map(item => findButton(item, id)).find(Boolean)
            : findButton(value, id);
        if (found)
            return found;
    }
    return null;
}

function etrnView() {
    const labels = new Proxy({}, { get: (_target, key) => String(key) });
    const names = new Proxy({}, { get: (_target, key) => String(key) });
    return { LABELS: labels, NAMES: names, _loadedEtrn: [] };
}
