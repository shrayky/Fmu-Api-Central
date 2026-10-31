import { test } from "node:test";
import assert from "node:assert/strict";
import { VetisFilterBar } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisFilterBar.js";

test("входящие открываются с отбором «Не принят»", () => {
    const bar = new VetisFilterBar({
        prefix: "vetisIncoming",
        organizations: [],
        withStatus: true,
        onChange() {},
    });
    const status = bar.config().elements.find((item) => item.id === "vetisIncomingStatusFilter");

    assert.equal(status.value, "notAccepted");
});
