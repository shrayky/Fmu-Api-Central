import { test } from "node:test";
import assert from "node:assert/strict";
import {
    ALL_GROUPS_VALUE,
    WITHOUT_GROUP_VALUE,
    toGroupFilter,
    toGroupSelectValue
} from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/groupFilter.js";

test("сохранённая группа открывается выбранной", () => {
    assert.equal(toGroupSelectValue({ groupId: "g1" }), "g1");
});

test("отбор без группы открывается пунктом «без группы»", () => {
    assert.equal(toGroupSelectValue({ withoutGroup: true }), WITHOUT_GROUP_VALUE);
});

test("пустой отбор открывается пунктом «все»", () => {
    assert.equal(toGroupSelectValue({}), ALL_GROUPS_VALUE);
    assert.equal(toGroupSelectValue({ groupId: "" }), ALL_GROUPS_VALUE);
});

test("пункт «без группы» превращается во флаг без группы", () => {
    assert.deepEqual(toGroupFilter(WITHOUT_GROUP_VALUE), { groupId: "", withoutGroup: true });
});

test("выбранная группа превращается в идентификатор группы", () => {
    assert.deepEqual(toGroupFilter("g1"), { groupId: "g1", withoutGroup: false });
});

test("пункт «все» очищает отбор по группе", () => {
    assert.deepEqual(toGroupFilter(ALL_GROUPS_VALUE), { groupId: "", withoutGroup: false });
    assert.deepEqual(toGroupFilter(""), { groupId: "", withoutGroup: false });
});
