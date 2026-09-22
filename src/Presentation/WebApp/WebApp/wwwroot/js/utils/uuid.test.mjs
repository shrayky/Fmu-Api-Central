import assert from "node:assert/strict";
import { newUuid } from "./uuid.js";

const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

const original = globalThis.crypto.randomUUID;
delete globalThis.crypto.randomUUID;

const id = newUuid();

assert.match(id, UUID_V4, "newUuid должен дать UUID v4 без crypto.randomUUID");

globalThis.crypto.randomUUID = original;
