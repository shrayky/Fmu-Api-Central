import { test } from "node:test";
import assert from "node:assert/strict";
import {
    salePointLabel,
    salePointNameFromAddress,
    salePointFieldError,
    salePointsFromAddresses,
    salePointsToPayload,
    salePointsToRows
} from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/salePointFormat.js";

test("uid, поля и синонимы переживают круг строки и полезной нагрузки", () => {
    const points = [
        {
            uid: "p1",
            name: " Склад ",
            address: " Москва, Ленина, 1 ",
            fiasId: " f1 ",
            closed: true,
            aliases: [{ uid: "a1", address: " Мск, Ленинский 1 " }]
        }
    ];

    const payload = salePointsToPayload(salePointsToRows(points));

    assert.deepEqual(payload, [
        {
            uid: "p1",
            name: "Склад",
            address: "Москва, Ленина, 1",
            fiasId: "f1",
            closed: true,
            aliases: [{ uid: "a1", address: "Мск, Ленинский 1" }]
        }
    ]);
});

test("пустой синоним в сохранение не уходит", () => {
    const payload = salePointsToPayload([
        { uid: "p1", name: "Склад", address: "Москва, Ленина, 1", aliases: [{ uid: "", address: "  " }] }
    ]);

    assert.deepEqual(payload[0].aliases, []);
});

test("новая точка уходит без uid и с закрытой по умолчанию", () => {
    const payload = salePointsToPayload([{ uid: "", name: "Новая", address: "Москва, Мира, 1" }]);

    assert.equal(payload[0].uid, "");
    assert.equal(payload[0].closed, false);
    assert.deepEqual(payload[0].aliases, []);
});

test("закрытая точка помечена в подписи, пустое имя даёт пустую строку", () => {
    assert.equal(salePointLabel({ name: "Склад", closed: false }), "Склад");
    assert.equal(salePointLabel({ name: "Склад", closed: true }), "Склад (закрыта)");
    assert.equal(salePointLabel({ salePointName: "Магазин", salePointClosed: true }), "Магазин (закрыта)");
    assert.equal(salePointLabel({ salePointName: "", salePointClosed: false }), "");
});

test("короткое имя точки берётся из улицы и дома адреса", () => {
    assert.equal(
        salePointNameFromAddress("Красноярский край, г. Красноярск, Свердловская ул., д. 113 Д"),
        "Свердловская 113Д"
    );
    assert.equal(salePointNameFromAddress("Москва, Ленина, 1"), "Ленина 1");
    assert.equal(salePointNameFromAddress("Красноярский край, г. Красноярск, ул. Мира, д. 5"), "Мира 5");
});

test("вставленные адреса превращаются в точки, по одной на строку", () => {
    const rows = salePointsFromAddresses("Москва, Ленина, 1\n\n Казань, Баумана, 2 \n").points;

    assert.equal(rows.length, 2);
    assert.deepEqual(rows[0], {
        uid: "",
        name: "Ленина 1",
        address: "Москва, Ленина, 1",
        fiasId: "",
        closed: false,
        aliases: []
    });
    assert.equal(rows[1].address, "Казань, Баумана, 2");
    assert.equal(rows[1].name, "Баумана 2");
});

test("одинаковый адрес во вставке и уже занятый адрес пропускаются", () => {
    const rows = salePointsFromAddresses(
        "Москва, Ленина, 1\nмосква, ленина, 1\nКазань, Баумана, 2\nМск, склад",
        [" казань, баумана, 2 ", "мск, склад"]
    ).points;

    assert.equal(rows.length, 1);
    assert.equal(rows[0].address, "Москва, Ленина, 1");
});

test("слишком длинное название или адрес в загрузку не попадают", () => {
    const longName = `${"улица".repeat(40)}, 1`;
    const longAddress = `${"район, ".repeat(40)}Ленина, 1`;
    const loaded = salePointsFromAddresses(`${longName}\n${longAddress}\nМосква, Ленина, 1`);

    assert.equal(loaded.error, "Название торговой точки: не больше 150 символов");
    assert.equal(loaded.points.length, 1);
    assert.equal(loaded.points[0].address, "Москва, Ленина, 1");

    const addressOnly = salePointsFromAddresses(longAddress);
    assert.equal(addressOnly.error, "Адрес торговой точки: не больше 250 символов");
    assert.equal(addressOnly.points.length, 0);
});

test("длина названия, адреса, ФИАС и синонима ограничена", () => {
    assert.equal(salePointFieldError({ name: "", address: "Москва" }), "Укажите название торговой точки");
    assert.equal(salePointFieldError({ name: "Склад", address: "" }), "Укажите адрес торговой точки");
    assert.equal(
        salePointFieldError({ name: "а".repeat(151), address: "Москва" }),
        "Название торговой точки: не больше 150 символов"
    );
    assert.equal(
        salePointFieldError({ name: "Склад", address: "а".repeat(251) }),
        "Адрес торговой точки: не больше 250 символов"
    );
    assert.equal(
        salePointFieldError({ name: "Склад", address: "Москва", fiasId: "а".repeat(37) }),
        "ФИАС торговой точки: не больше 36 символов"
    );
    assert.equal(
        salePointFieldError({
            name: "Склад",
            address: "Москва",
            aliases: [{ address: "а".repeat(251) }]
        }),
        "Адрес синонима: не больше 250 символов"
    );
    assert.equal(salePointFieldError({ name: " Склад ", address: " Москва ", fiasId: " f " }), "");
});
