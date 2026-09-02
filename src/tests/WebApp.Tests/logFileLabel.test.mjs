import { test } from "node:test";
import assert from "node:assert/strict";
import { formatLogFileLabel } from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/logFileLabel.js";

test("суффикс yyyyMMdd показывается как yyyy-MM-dd", () => {
    assert.equal(formatLogFileLabel("20260902"), "2026-09-02");
});

test("ролл по размеру показывается номером в скобках", () => {
    assert.equal(formatLogFileLabel("20260828001"), "2026-08-28 (001)");
});

test("нестандартный суффикс остаётся как есть", () => {
    assert.equal(formatLogFileLabel("now"), "now");
});
