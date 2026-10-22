import { test } from "node:test";
import assert from "node:assert/strict";
import { instanceCardCancelTarget } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/instanceMonitoring/instanceCardCancel.js";

test("открытая команда установки закрывается вместо карточки", () => {
    assert.equal(instanceCardCancelTarget(true), "command");
});

test("без окна команды Esc закрывает карточку", () => {
    assert.equal(instanceCardCancelTarget(false), "card");
});
