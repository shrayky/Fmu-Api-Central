import { test } from "node:test";
import assert from "node:assert/strict";
import { formatInstancesCount } from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/formatInstancesCount.js";

test("показывает только общее количество инстансов", () => {
    assert.equal(formatInstancesCount({ instancesOnline: 0, instancesTotal: 5 }), "5");
});

test("без количества показывает 0", () => {
    assert.equal(formatInstancesCount({}), "0");
});
