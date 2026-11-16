import { test } from "node:test";
import assert from "node:assert/strict";
import {
    salePointFilterOptions,
    salePointPickerOptions
} from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/salePointFormat.js";

const points = [
    { uid: "p2", name: "Старый", closed: true },
    { uid: "p1", name: "Склад", closed: false }
];

test("отбор точки: все, без точки, открытые, затем закрытые", () => {
    const options = salePointFilterOptions(points);

    assert.deepEqual(options, [
        { id: "", value: "Все точки" },
        { id: "none", value: "Без точки" },
        { id: "p1", value: "Склад" },
        { id: "p2", value: "Старый (закрыта)" }
    ]);
});

test("без точек в отборе остаются только все и без точки", () => {
    assert.deepEqual(salePointFilterOptions([]), [
        { id: "", value: "Все точки" },
        { id: "none", value: "Без точки" }
    ]);
});

test("в списке выбора только открытые и очистка", () => {
    assert.deepEqual(salePointPickerOptions(points), [
        { id: "clear", uid: "", value: "Очистить" },
        { id: "p1", uid: "p1", value: "Склад" }
    ]);
});
