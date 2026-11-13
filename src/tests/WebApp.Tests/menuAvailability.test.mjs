import { test } from "node:test";
import assert from "node:assert/strict";
import { MENU_ITEMS } from "../../Presentation/WebApp/WebApp/wwwroot/js/config/menu.js";
import { visibleMenuItems, menuInsertIndex } from "../../Presentation/WebApp/WebApp/wwwroot/js/config/menuAvailability.js";

const ids = (items) => items.map((item) => item.id);

test("пункт Ветис скрыт при выключенной интеграции", () => {
    assert.ok(!ids(visibleMenuItems(MENU_ITEMS, false)).includes("vetis"));
});

test("пункт Ветис виден при включённой интеграции", () => {
    assert.ok(ids(visibleMenuItems(MENU_ITEMS, true)).includes("vetis"));
});

test("остальные пункты не зависят от интеграции", () => {
    const withVetis = ids(visibleMenuItems(MENU_ITEMS, true)).filter((id) => id !== "vetis");
    assert.deepEqual(ids(visibleMenuItems(MENU_ITEMS, false)), withVetis);
});

test("пункт Ветис вставляется на то же место, что и при начальной сборке меню", () => {
    const shown = ids(visibleMenuItems(MENU_ITEMS, true, true));
    const hidden = ids(visibleMenuItems(MENU_ITEMS, false, true));
    const index = menuInsertIndex(MENU_ITEMS, hidden, "vetis");
    assert.deepEqual([...hidden.slice(0, index), "vetis", ...hidden.slice(index)], shown);
});

test("без флага ЭТрН пункт скрыт", () => {
    assert.ok(!ids(visibleMenuItems(MENU_ITEMS, true)).includes("etrn"));
});

test("пункт ЭТрН скрыт при выключенной интеграции", () => {
    assert.ok(!ids(visibleMenuItems(MENU_ITEMS, true, false)).includes("etrn"));
});

test("пункт ЭТрН виден при включённой интеграции", () => {
    assert.ok(ids(visibleMenuItems(MENU_ITEMS, false, true)).includes("etrn"));
});

test("пункт ЭТрН вставляется на то же место, что и при начальной сборке меню", () => {
    const shown = ids(visibleMenuItems(MENU_ITEMS, true, true));
    const hidden = ids(visibleMenuItems(MENU_ITEMS, true, false));
    const index = menuInsertIndex(MENU_ITEMS, hidden, "etrn");
    assert.deepEqual([...hidden.slice(0, index), "etrn", ...hidden.slice(index)], shown);
});
