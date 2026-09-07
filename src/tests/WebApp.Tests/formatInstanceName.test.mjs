import { test } from "node:test";
import assert from "node:assert/strict";
import { formatInstanceName } from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/formatInstanceName.js";

test("назначенная выгрузка настроек показывает шестерёнку рядом с именем", () => {
    const html = formatInstanceName({ name: "Касса-1", settingsModified: true });

    assert.match(html, /^Касса-1/);
    assert.match(html, /title="Выгрузка настроек"/);
    assert.match(html, /⚙/);
});

test("без назначения показывает только имя", () => {
    assert.equal(formatInstanceName({ name: "Касса-1" }), "Касса-1");
});

test("назначенное принудительное обновление показывает иконку загрузки", () => {
    const html = formatInstanceName({ name: "Касса-1", forcedUpdateId: "upd-1" });

    assert.match(html, /wxi-download/);
    assert.match(html, /title="Принудительная установка"/);
    assert.doesNotMatch(html, /⚙/);
});

test("обе иконки рядом, если назначены выгрузка и обновление", () => {
    const html = formatInstanceName({
        name: "Касса-1",
        settingsModified: true,
        forcedUpdateId: "upd-1"
    });

    assert.match(html, /⚙/);
    assert.match(html, /wxi-download/);
});

test("экранирует HTML в имени инстанса", () => {
    const html = formatInstanceName({ name: "<b>Касса</b>" });

    assert.equal(html, "&lt;b&gt;Касса&lt;/b&gt;");
});
