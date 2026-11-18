import { test } from "node:test";
import assert from "node:assert/strict";
import organizationService from "../../Presentation/WebApp/WebApp/wwwroot/js/services/organizationService.js";

// Модуль работает с Webix и полями по id: в тесте они подменяются.
const fields = new Map();
const messages = [];

globalThis.$$ = (id) => fields.get(id) ?? null;
globalThis.webix = {
    message: (options) => messages.push(options),
    ui: () => ({ show() {} })
};
globalThis.window = { innerWidth: 1200, innerHeight: 800 };
globalThis.document = {
    addEventListener() {},
    removeEventListener() {}
};

const { applyDadataHouse, loadDadataAddress, openDadataWindow } = await import(
    "../../Presentation/WebApp/WebApp/wwwroot/js/modules/organization/organizationSalePoints.js"
);

function createView() {
    return {
        NAMES: {
            salePointFias: "fias",
            salePointAddress: "address",
            salePointFiasLoad: "fiasLoad",
            salePointFiasSpinner: "fiasSpinner",
            salePointDadataWindow: "dadataWindow",
            salePointDadataList: "dadataList"
        },
        LABELS: {
            salePointDadataTitle: "Выбор дома",
            salePointDadataSelect: "Выбрать",
            cancelButton: "Отмена"
        }
    };
}

function field(initial = "") {
    return {
        value: initial,
        getValue() { return this.value; },
        setValue(value) { this.value = value; }
    };
}

test("дом заполняет ФИАС и адрес", () => {
    fields.clear();
    fields.set("fias", field());
    fields.set("address", field());

    const applied = applyDadataHouse(createView(), { fiasId: "f1", address: "Москва, Ленина, 1" });

    assert.equal(applied, true);
    assert.equal(fields.get("fias").value, "f1");
    assert.equal(fields.get("address").value, "Москва, Ленина, 1");
});

test("дом без текста адреса пишет только ФИАС", () => {
    fields.clear();
    fields.set("fias", field("old-fias"));
    fields.set("address", field("как ввели"));

    const applied = applyDadataHouse(createView(), { fiasId: "f1", address: "" });

    assert.equal(applied, true);
    assert.equal(fields.get("fias").value, "f1");
    assert.equal(fields.get("address").value, "как ввели");
});

test("длинный адрес оставляет поля как были", () => {
    fields.clear();
    fields.set("fias", field("old-fias"));
    fields.set("address", field("old-address"));
    messages.length = 0;

    const applied = applyDadataHouse(createView(), { fiasId: "f1", address: "а".repeat(251) });

    assert.equal(applied, false);
    assert.equal(fields.get("fias").value, "old-fias");
    assert.equal(fields.get("address").value, "old-address");
    assert.match(messages.at(-1).text, /250/);
});

test("в окне выбора есть кнопки «Выбрать» и «Отмена»", () => {
    let config = null;
    globalThis.webix.ui = (value) => {
        config = value;
        return { show() {} };
    };

    openDadataWindow(createView(), [{ fiasId: "f1", address: "Москва, Ленина, 1" }]);

    const buttons = config.body.rows[1].cols;
    assert.equal(buttons[0].value, "Выбрать");
    assert.equal(buttons[0].css, "webix_primary");
    assert.equal(buttons[1].value, "Отмена");
});

test("двойной клик по строке применяет дом", () => {
    fields.clear();
    fields.set("fias", field());
    fields.set("address", field());

    let config = null;
    globalThis.webix.ui = (value) => {
        config = value;
        return { show() {} };
    };

    const house = { fiasId: "f1", address: "Москва, Ленина, 1" };
    openDadataWindow(createView(), [house]);

    const list = config.body.rows[0];
    assert.equal(typeof list.on.onItemDblClick, "function");

    list.on.onItemDblClick.call({ getItem: () => house }, "house-0");

    assert.equal(fields.get("fias").value, "f1");
    assert.equal(fields.get("address").value, "Москва, Ленина, 1");
});

test("на время запроса кнопка скрыта, а спиннер показан", async () => {
    const calls = [];
    fields.clear();
    fields.set("address", field("Москва, Ленина, 1"));
    fields.set("fiasLoad", {
        disable() { calls.push("button-disable"); },
        enable() { calls.push("button-enable"); },
        hide() { calls.push("button-hide"); },
        show() { calls.push("button-show"); }
    });
    fields.set("fiasSpinner", {
        show() { calls.push("spinner-show"); },
        hide() { calls.push("spinner-hide"); }
    });

    organizationService.dadataAddress = async () => [];

    await loadDadataAddress(createView());

    assert.deepEqual(calls, [
        "button-disable",
        "button-hide",
        "spinner-show",
        "spinner-hide",
        "button-show",
        "button-enable"
    ]);
});
