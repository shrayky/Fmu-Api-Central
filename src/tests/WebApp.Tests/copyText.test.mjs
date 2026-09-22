import { test } from "node:test";
import assert from "node:assert/strict";
import { copyText } from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/copyText.js";

function stubGlobal(name, value) {
    const previous = Object.getOwnPropertyDescriptor(globalThis, name);
    Object.defineProperty(globalThis, name, {
        configurable: true,
        enumerable: true,
        writable: true,
        value
    });
    return () => {
        if (previous) {
            Object.defineProperty(globalThis, name, previous);
            return;
        }

        delete globalThis[name];
    };
}

test("без Clipboard API копирует текст через execCommand", async () => {
    const textarea = {
        value: "",
        style: {},
        setAttribute() {},
        select() {}
    };
    const restoreNavigator = stubGlobal("navigator", {});
    const restoreDocument = stubGlobal("document", {
        createElement(tag) {
            assert.equal(tag, "textarea");
            return textarea;
        },
        body: {
            appendChild() {},
            removeChild() {}
        },
        execCommand(command) {
            assert.equal(command, "copy");
            return true;
        }
    });

    try {
        await copyText("secret-token");
        assert.equal(textarea.value, "secret-token");
    } finally {
        restoreNavigator();
        restoreDocument();
    }
});

test("при наличии Clipboard API вызывает writeText", async () => {
    let written = "";
    const restoreNavigator = stubGlobal("navigator", {
        clipboard: {
            async writeText(value) {
                written = value;
            }
        }
    });

    try {
        await copyText("token-1");
        assert.equal(written, "token-1");
    } finally {
        restoreNavigator();
    }
});
