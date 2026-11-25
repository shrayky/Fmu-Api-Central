import test from "node:test";
import assert from "node:assert/strict";
import { nextBookmark, nextOffset, VetisPagedTable } from "../../Presentation/WebApp/WebApp/wwwroot/js/modules/vetis/vetisPagedTable.js";

test("обычная таблица сдвигает skip на число показанных строк", () => {
    assert.equal(nextOffset({ rows: [{}, {}, {}] }, 10), 13);
});

test("остатки продолжаются закладкой ответа", () => {
    assert.equal(nextBookmark({ rows: [{}, {}], bookmark: "g1AAA" }), "g1AAA");
    assert.equal(nextBookmark({ rows: [] }), "");
});

test("ошибка перезагрузки оставляет прежние строки и показывает сообщение", async () => {
    const rows = [];
    const messages = [];
    const table = {
        clearAll() {
            rows.length = 0;
        },
        parse(next) {
            rows.push(...next);
        },
        count() {
            return rows.length;
        },
        showProgress() {},
        hideProgress() {},
        showOverlay() {},
        hideOverlay() {},
        getScrollState() {
            return { y: 0 };
        },
        $height: 200,
    };

    globalThis.webix = {
        message(options) {
            messages.push(options);
        },
        extend() {},
        ProgressBar: {},
        OverlayBox: {},
    };
    globalThis.$$ = () => table;

    let fail = false;
    const view = new VetisPagedTable({
        id: "gisMtDocuments",
        fetchPage: async () => {
            if (fail)
                throw new Error("БД недоступна");

            return { rows: [{ id: "u1", number: "u1" }], hasMore: false };
        },
        onOpen() {},
    });

    try {
        await view.reload({});
        fail = true;
        await view.reload({});
    }
    finally {
        delete globalThis.webix;
        delete globalThis.$$;
    }

    assert.equal(rows.length, 1);
    assert.equal(rows[0].id, "u1");
    assert.equal(messages.at(-1).type, "error");
    assert.equal(messages.at(-1).text, "БД недоступна");
});

test("ошибка перезагрузки оставляет закладку и hasMore", async () => {
    const rows = [];
    const failedCursors = [];
    const table = tableStub(rows);

    globalThis.webix = webixStub();
    globalThis.$$ = () => table;

    let fail = false;
    const view = new VetisPagedTable({
        id: "gisMtMarks",
        bookmark: true,
        fetchPage: async (_filters, cursor) => {
            if (fail) {
                failedCursors.push(cursor);
                throw new Error("БД недоступна");
            }

            return {
                rows: Array.from({ length: 6 }, (_, index) => ({ id: "u" + index })),
                bookmark: "bm-1",
                hasMore: true,
            };
        },
        onOpen() {},
    });

    try {
        await view.reload({});
        fail = true;
        await view.reload({ search: "x" });
    }
    finally {
        delete globalThis.webix;
        delete globalThis.$$;
    }

    assert.deepEqual(failedCursors, [""]);
    assert.equal(view._bookmark, "bm-1");
    assert.equal(view._hasMore, true);
    assert.equal(rows.length, 6);
    assert.equal(rows[0].id, "u0");
});

test("ошибка перезагрузки оставляет skip и hasMore", async () => {
    const rows = [];
    const failedCursors = [];
    const table = tableStub(rows);

    globalThis.webix = webixStub();
    globalThis.$$ = () => table;

    let fail = false;
    const view = new VetisPagedTable({
        id: "gisMtCatalog",
        fetchPage: async (_filters, cursor) => {
            if (fail) {
                failedCursors.push(cursor);
                throw new Error("БД недоступна");
            }

            return {
                rows: Array.from({ length: 6 }, (_, index) => ({ id: "u" + index })),
                hasMore: true,
            };
        },
        onOpen() {},
    });

    try {
        await view.reload({});
        fail = true;
        await view.reload({ gtin: "046" });
    }
    finally {
        delete globalThis.webix;
        delete globalThis.$$;
    }

    assert.deepEqual(failedCursors, [0]);
    assert.equal(view._skip, 6);
    assert.equal(view._hasMore, true);
    assert.equal(rows.length, 6);
});

function tableStub(rows) {
    return {
        clearAll() {
            rows.length = 0;
        },
        parse(next) {
            rows.push(...next);
        },
        count() {
            return rows.length;
        },
        showProgress() {},
        hideProgress() {},
        showOverlay() {},
        hideOverlay() {},
        getScrollState() {
            return { y: 0 };
        },
        $height: 0,
    };
}

function webixStub() {
    return {
        message() {},
        extend() {},
        ProgressBar: {},
        OverlayBox: {},
    };
}
