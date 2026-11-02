import assert from "node:assert/strict";
import test from "node:test";
import {
    PERIOD_PRESETS,
    refreshPresetDates,
} from "../WebApp/wwwroot/js/modules/markCheckStatistics/markCheckStatisticsPeriodPresets.js";

test("сохранённый пресет сегодня получает текущую дату", () => {
    const filters = refreshPresetDates({
        periodPreset: PERIOD_PRESETS.today,
        dateFrom: "2020-01-01",
        dateTo: "2020-01-01",
        name: "Касса",
    }, new Date(2026, 10, 2));

    assert.equal(filters.dateFrom, "2026-11-02");
    assert.equal(filters.dateTo, "2026-11-02");
    assert.equal(filters.name, "Касса");
});

test("сохранённый пресет неделя получает последние 7 дней", () => {
    const filters = refreshPresetDates({
        periodPreset: PERIOD_PRESETS.week,
        dateFrom: "2020-01-01",
        dateTo: "2020-01-07",
    }, new Date(2026, 10, 2));

    assert.equal(filters.dateFrom, "2026-10-27");
    assert.equal(filters.dateTo, "2026-11-02");
});

test("произвольный период не меняет даты", () => {
    const filters = refreshPresetDates({
        periodPreset: PERIOD_PRESETS.custom,
        dateFrom: "2020-01-01",
        dateTo: "2020-01-05",
    }, new Date(2026, 10, 2));

    assert.equal(filters.dateFrom, "2020-01-01");
    assert.equal(filters.dateTo, "2020-01-05");
});
