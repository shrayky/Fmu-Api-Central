import { test } from "node:test";
import assert from "node:assert/strict";
import { estimateInstanceRowHeight } from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/estimateInstanceRowHeight.js";

const currentUnderestimate = 68 + 16;

test("блок ТСП с длинным ФИО выше текущей оценки 84px", () => {
    const height = estimateInstanceRowHeight({
        TsPiots: [{ name: "Сидиченко Павел Сергеевич", address: "https://localhost:51401", version: "1.6.2.1" }]
    }, 36);

    assert.ok(height > currentUnderestimate, `ожидали > ${currentUnderestimate}, получили ${height}`);
});

test("два модуля ТСП выше одного", () => {
    const one = estimateInstanceRowHeight({
        TsPiots: [{ name: "Сидиченко Павел Сергеевич" }]
    }, 36);
    const two = estimateInstanceRowHeight({
        TsPiots: [
            { name: "Сидиченко Павел Сергеевич" },
            { name: "Сидиченко Павел Сергеевич" }
        ]
    }, 36);

    assert.ok(two > one, `ожидали два блока выше одного: ${two} > ${one}`);
});

test("высота строки — максимум локальных и ТСП", () => {
    const twoLocal = estimateInstanceRowHeight({
        localModules: [{ address: "http://localhost:59955" }, { address: "http://localhost:59956" }],
        TsPiots: [{ name: "Иванов" }]
    }, 36);
    const twoTsp = estimateInstanceRowHeight({
        localModules: [{ address: "http://localhost:59955" }],
        TsPiots: [{ name: "Сидиченко Павел Сергеевич" }, { name: "Сидиченко Павел Сергеевич" }]
    }, 36);

    assert.equal(
        estimateInstanceRowHeight({
            localModules: [{ address: "http://localhost:59955" }, { address: "http://localhost:59956" }],
            TsPiots: [{ name: "Иванов" }]
        }, 36),
        twoLocal
    );
    assert.ok(twoTsp > twoLocal);
});

test("не опускается ниже minHeight", () => {
    assert.equal(estimateInstanceRowHeight({ localModules: [], TsPiots: [] }, 36), 36);
});
