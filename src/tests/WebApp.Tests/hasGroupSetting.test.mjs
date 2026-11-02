import { test } from "node:test";
import assert from "node:assert/strict";
import { hasGroupSetting } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/settingsSchema/hasGroupSetting.js";

const flags = [
    "checkSmp",
    "checkMrp",
    "checkExpireDate",
    "haveExpireDate",
    "forbidSaleByExpireDate"
];

test("строка без флажков не считается настройкой", () => {
    assert.equal(hasGroupSetting({
        checkSmp: false,
        checkMrp: false,
        checkExpireDate: false,
        haveExpireDate: false,
        forbidSaleByExpireDate: false
    }), false);
    assert.equal(hasGroupSetting({}), false);
    assert.equal(hasGroupSetting(null), false);
});

test("любой включённый флажок товарной группы — это настройка", () => {
    for (const flag of flags) {
        assert.equal(hasGroupSetting({ [flag]: true }), true, flag);
    }
});
