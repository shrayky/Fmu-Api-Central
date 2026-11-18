import { test } from "node:test";
import assert from "node:assert/strict";
import { serverClockText } from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/serverClock.js";

test("часы показывают серверное время, а не пояс браузера", () => {
    assert.equal(serverClockText("2026-11-18T13:02:05+00:00"), "18.11.2026 13:02:05");
    assert.equal(serverClockText("2026-11-18T13:02:05+07:00", 3000), "18.11.2026 13:02:08");
    assert.equal(serverClockText("2026-11-18T23:59:59-05:00", 2000), "19.11.2026 00:00:01");
    assert.equal(serverClockText(""), "");
    assert.equal(serverClockText("не дата"), "");
});
