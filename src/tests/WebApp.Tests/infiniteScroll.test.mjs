import { test } from "node:test";
import assert from "node:assert/strict";
import { shouldLoadMore } from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/infiniteScroll.js";

const base = { scrollTop: 0, viewHeight: 400, rowHeight: 34, rowCount: 50, hasMore: true, loading: false };

test("в начале длинного списка догрузка не нужна", () => {
    assert.equal(shouldLoadMore(base), false);
});

test("рядом с концом списка нужна догрузка", () => {
    assert.equal(shouldLoadMore({ ...base, scrollTop: 50 * 34 - 400 - 10 }), true);
});

test("когда строки не заполняют окно, нужна догрузка", () => {
    assert.equal(shouldLoadMore({ ...base, rowCount: 5 }), true);
});

test("без продолжения догрузка не нужна", () => {
    assert.equal(shouldLoadMore({ ...base, scrollTop: 5000, hasMore: false }), false);
});

test("во время загрузки повторный запрос не нужен", () => {
    assert.equal(shouldLoadMore({ ...base, scrollTop: 5000, loading: true }), false);
});
