import { test } from "node:test";
import assert from "node:assert/strict";
import { createDadataTab } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/dadataTab.js";

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

test("вкладка «Прочие интеграции» содержит флаг, ключи и подсказку", () => {
    const tab = createDadataTab({ enable: false, apiKey: "api-key", secret: "secret" });

    assert.equal(tab.header, "Прочие интеграции");
    assert.deepEqual(
        outline(tab.body).filter((item) => item[0] === "field"),
        [
            ["field", "dadataEnable", false],
            ["field", "dadataApiKey", "api-key"],
            ["field", "dadataSecret", "secret"]
        ]
    );

    assert.equal(find(tab.body, (node) => node.name === "dadataApiKey").disabled, true);
    assert.equal(find(tab.body, (node) => node.name === "dadataSecret").disabled, true);
    assert.equal(find(tab.body, (node) => node.name === "dadataApiKey").clear, true);
    assert.equal(find(tab.body, (node) => node.name === "dadataSecret").clear, true);
    assert.match(find(tab.body, (node) => node.view === "template").template, /кнопка «Получить»/);
});

test("флажок открывает API-ключ и секрет", () => {
    const tab = createDadataTab({ enable: false });
    const checkbox = find(tab.body, (node) => node.name === "dadataEnable");
    const calls = [];
    globalThis.$$ = (id) => ({
        enable() { calls.push(["enable", id]); },
        disable() { calls.push(["disable", id]); }
    });

    try {
        checkbox.on.onChange(true);
        checkbox.on.onChange(false);
    }
    finally {
        delete globalThis.$$;
    }

    assert.deepEqual(calls, [
        ["enable", "dadataApiKey"],
        ["enable", "dadataSecret"],
        ["disable", "dadataApiKey"],
        ["disable", "dadataSecret"]
    ]);
});

test("включённая интеграция оставляет поля открытыми", () => {
    const tab = createDadataTab({ enable: true, apiKey: "api-key", secret: "secret" });

    assert.equal(find(tab.body, (node) => node.name === "dadataEnable").value, true);
    assert.equal(find(tab.body, (node) => node.name === "dadataApiKey").disabled, false);
    assert.equal(find(tab.body, (node) => node.name === "dadataSecret").disabled, false);
});

test("вкладка читает и отдаёт на сохранение флаг, API-ключ и секрет", async () => {
    const { buildConfigurationUpdaters } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/saveSettings.js");
    const { mapServerSettings } = await import("../../Presentation/WebApp/WebApp/wwwroot/js/modules/serverSettings/loadedSettings.js");

    const updaters = buildConfigurationUpdaters({
        dadataEnable: 1,
        dadataApiKey: " api-key ",
        dadataSecret: " secret "
    });

    assert.deepEqual(updaters.dadataSettings({ keep: true }), {
        keep: true,
        enable: true,
        apiKey: "api-key",
        secret: "secret"
    });

    // Поля вкладки могли не отрисоваться: сохранённые ключи не стираются.
    assert.deepEqual(
        buildConfigurationUpdaters({}).dadataSettings({ enable: true, apiKey: "api-key", secret: "secret" }),
        { enable: true, apiKey: "api-key", secret: "secret" }
    );

    const mapped = mapServerSettings({
        dadataSettings: { enable: true, apiKey: "api-key", secret: "secret" }
    });
    assert.deepEqual(mapped.dadata, { enable: true, apiKey: "api-key", secret: "secret" });

    assert.deepEqual(mapServerSettings({}).dadata, { enable: false, apiKey: "", secret: "" });
});
