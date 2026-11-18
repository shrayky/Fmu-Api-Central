import { test } from "node:test";
import assert from "node:assert/strict";
import {
    DADATA_MESSAGES,
    dadataEnabled,
    dadataHouses,
    dadataHouseValues,
    dadataSelection,
    salePointFiasRow
} from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/organization/dadataSuggest.js";

test("выключенный флаг скрывает кнопку «Получить»", () => {
    const disabled = salePointFiasRow({
        enabled: false,
        fiasId: "f1",
        label: "ФИАС",
        name: "fias",
        buttonId: "load",
        buttonLabel: "Получить"
    });

    assert.equal(disabled.view, "text");
    assert.equal(disabled.value, "f1");
    assert.equal(disabled.clear, true);

    const enabled = salePointFiasRow({
        enabled: true,
        fiasId: "",
        label: "ФИАС",
        name: "fias",
        buttonId: "load",
        spinnerId: "spinner",
        buttonLabel: "Получить"
    });

    assert.equal(enabled.cols[0].view, "text");
    assert.equal(enabled.cols[0].name, undefined);
    assert.equal(enabled.cols[0].id, "fias");
    assert.equal(enabled.cols[0].clear, true);
    // Кнопка стоит в нижней строке колонки: её высота равна полю ввода, а не подписи.
    assert.equal(enabled.cols[1].rows[0].gravity, 1);
    assert.equal(enabled.cols[1].rows[1].view, "button");
    assert.equal(enabled.cols[1].rows[1].id, "load");
    assert.equal(enabled.cols[1].rows[1].value, "Получить");
    // Вместо кнопки на время запроса показывается индикатор ожидания.
    assert.equal(enabled.cols[1].rows[2].view, "template");
    assert.equal(enabled.cols[1].rows[2].id, "spinner");
    assert.equal(enabled.cols[1].rows[2].hidden, true);
    assert.match(enabled.cols[1].rows[2].template, /webix_spin/);
});

test("флаг интеграции берётся из настроек", () => {
    assert.equal(dadataEnabled({ dadataSettings: { enable: true } }), true);
    assert.equal(dadataEnabled({ dadataSettings: { enable: "true" } }), false);
    assert.equal(dadataEnabled({}), false);
    assert.equal(dadataEnabled(undefined), false);
});

test("один дом заполняет ФИАС и адрес", () => {
    const selection = dadataSelection([{ fiasId: " f1 ", address: " Москва, Ленина, 1 " }]);

    assert.equal(selection.error, "");
    assert.deepEqual(selection.values, { fiasId: "f1", address: "Москва, Ленина, 1" });
    assert.deepEqual(selection.houses, []);
});

test("несколько домов показывают список", () => {
    const selection = dadataSelection([
        { fiasId: "f1", address: "Москва, Ленина, 1" },
        { fiasId: "f2", address: "Москва, Ленина, 3" }
    ]);

    assert.equal(selection.error, "");
    assert.equal(selection.values, null);
    assert.equal(selection.houses.length, 2);
});

test("выбор строки списка заполняет оба поля", () => {
    assert.deepEqual(dadataHouseValues({ fiasId: "f2", address: "Москва, Ленина, 3" }), {
        fiasId: "f2",
        address: "Москва, Ленина, 3"
    });
});

test("длинный дом не прячет короткий", () => {
    const selection = dadataSelection([
        { fiasId: "f1", address: "а".repeat(251) },
        { fiasId: "f2", address: "Москва, Ленина, 1" }
    ]);

    assert.equal(selection.error, "");
    assert.deepEqual(selection.values, { fiasId: "f2", address: "Москва, Ленина, 1" });
});

test("дом без текста адреса оставляет ФИАС", () => {
    const selection = dadataSelection([{ fiasId: "f1", address: "" }]);

    assert.equal(selection.error, "");
    assert.deepEqual(selection.values, { fiasId: "f1", address: "" });
});

test("длинный адрес оставляет поля как были", () => {
    const longAddress = "а".repeat(251);
    const selection = dadataSelection([{ fiasId: "f1", address: longAddress }]);

    assert.equal(selection.error, DADATA_MESSAGES.longAddress);
    assert.equal(selection.values, null);
    assert.equal(dadataHouseValues({ fiasId: "f1", address: longAddress }).error, DADATA_MESSAGES.longAddress);
});

test("дом не найден оставляет поля как были", () => {
    const selection = dadataSelection([]);

    assert.equal(selection.error, DADATA_MESSAGES.houseNotFound);
    assert.equal(selection.values, null);
});

test("строка без ФИАС в список не попадает", () => {
    const loaded = dadataHouses([
        { fiasId: "", address: "Москва" },
        { fiasId: "f1", address: "Москва, Ленина, 1" }
    ]);

    assert.equal(loaded.houses.length, 1);
    assert.equal(loaded.houses[0].fiasId, "f1");
    assert.equal(loaded.error, "");
});
