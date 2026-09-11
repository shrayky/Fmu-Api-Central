import { test } from "node:test";
import assert from "node:assert/strict";
import { formatGisMtStatus } from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/formatGisMtStatus.js";

const empty = "—";

test("без кода показывает прочерк", () => {
    assert.equal(formatGisMtStatus({}, empty), empty);
    assert.equal(formatGisMtStatus({ gisMtLastStatus: {} }, empty), empty);
});

test("успешный код показывает только код", () => {
    const html = formatGisMtStatus({
        trueApiEnabled: true,
        gisMtLastStatus: { code: 200, description: "OK" }
    }, empty);

    assert.match(html, />200</);
    assert.doesNotMatch(html, /OK/);
    assert.doesNotMatch(html, /E74C3C/);
});

test("ошибка ЧЗ при включённой интеграции показывает код и описание", () => {
    const html = formatGisMtStatus({
        trueApiEnabled: true,
        gisMtLastStatus: { code: 401, description: "Unauthorized" }
    }, empty);

    assert.match(html, /401 Unauthorized/);
    assert.match(html, /E74C3C/);
});

test("ошибка нет связи показывает прочерк", () => {
    assert.equal(formatGisMtStatus({
        trueApiEnabled: true,
        gisMtLastStatus: {
            code: 0,
            description: "The request was cancelled due to the connection being closed"
        }
    }, empty), empty);
});

test("таймаут соединения показывает прочерк", () => {
    assert.equal(formatGisMtStatus({
        trueApiEnabled: true,
        gisMtLastStatus: {
            code: 0,
            description: "The request was canceled due to the configured HttpClient.Timeout"
        }
    }, empty), empty);
});

test("интеграция выключена — прочерк вместо ошибки", () => {
    assert.equal(formatGisMtStatus({
        trueApiEnabled: false,
        gisMtLastStatus: { code: 401, description: "Unauthorized" }
    }, empty), empty);
});

test("экранирует HTML в описании", () => {
    const html = formatGisMtStatus({
        trueApiEnabled: true,
        gisMtLastStatus: { code: 500, description: "<b>fail</b>" }
    }, empty);

    assert.match(html, /&lt;b&gt;fail&lt;\/b&gt;/);
    assert.doesNotMatch(html, /<b>/);
});
