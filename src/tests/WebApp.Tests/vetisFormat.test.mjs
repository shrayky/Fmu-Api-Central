import { test } from "node:test";
import assert from "node:assert/strict";
import {
    formatIsoMoment,
    formatUnixMoment,
    incomingDocumentLabel,
    incomingFromForSave,
    incomingReceivedLabel,
    incomingStatusLabel,
    incomingStatusCss,
    operationLabel,
    vsdStatusLabel,
    vsdCountText,
    vsdXmlFileName,
    requestStateText,
} from "../../Presentation/WebApp/WebApp/wwwroot/js/utils/vetisFormat.js";

test("статусы входящего документа показываются по-русски", () => {
    assert.equal(incomingStatusLabel("accepted"), "Принят");
    assert.equal(incomingStatusLabel("partial"), "Принят частично");
    assert.equal(incomingStatusLabel("notAccepted"), "Не принят");
    assert.equal(incomingStatusLabel("rejected"), "Отклонён");
});

test("неизвестный статус документа показывается как есть", () => {
    assert.equal(incomingStatusLabel("zzz"), "zzz");
    assert.equal(incomingStatusLabel(undefined), "");
});

test("у каждого статуса документа свой css-класс", () => {
    const classes = ["accepted", "partial", "notAccepted", "rejected"].map(incomingStatusCss);
    assert.equal(new Set(classes).size, 4);
    assert.equal(incomingStatusCss("zzz"), "");
});

test("операции Ветис показываются понятными названиями", () => {
    assert.equal(operationLabel("GetVetDocumentChangesList"), "Загрузка ВСД");
    assert.equal(operationLabel("GetBusinessEntityUser"), "Проверка соединения");
    assert.equal(operationLabel("GetActivityLocationList"), "Список предприятий");
});

test("неизвестная операция показывается как есть", () => {
    assert.equal(operationLabel("Other"), "Other");
    assert.equal(operationLabel(""), "");
});

test("статус ВСД показывается по-русски", () => {
    assert.equal(vsdStatusLabel("CONFIRMED"), "Оформлен");
    assert.equal(vsdStatusLabel("UTILIZED"), "Погашен");
    assert.equal(vsdStatusLabel("FINALIZED"), "Погашен");
    assert.equal(vsdStatusLabel("WITHDRAWN"), "Аннулирован");
    assert.equal(vsdStatusLabel("CANCELED"), "Аннулирован");
    assert.equal(vsdStatusLabel("NEW"), "NEW");
});

test("файл XML называется по номеру ВСД", () => {
    assert.equal(vsdXmlFileName({ issueNumber: "5960", uuid: "abc" }), "5960.xml");
});

test("без номера файл XML называется по идентификатору", () => {
    assert.equal(vsdXmlFileName({ issueNumber: "  ", uuid: "abc" }), "abc.xml");
    assert.equal(vsdXmlFileName({}), "vsd.xml");
});

test("в имени файла XML недопустимые символы заменяются", () => {
    assert.equal(vsdXmlFileName({ issueNumber: "a/b:c" }), "a_b_c.xml");
});

test("количество ВСД показывает принятые из всех", () => {
    assert.equal(vsdCountText({ acceptedCount: 2, vsdCount: 3 }), "2 из 3");
    assert.equal(vsdCountText({}), "0 из 0");
});

test("состояние запроса: ответ получен, ошибка или ожидание", () => {
    assert.equal(requestStateText({ hasResponse: true, error: "" }), "Получен");
    assert.equal(requestStateText({ hasResponse: false, error: "" }), "Нет ответа");
    assert.equal(requestStateText({ hasResponse: false, error: "Таймаут" }), "Ошибка");
    assert.equal(requestStateText({ hasResponse: true, error: "HTTP 500" }), "Ошибка");
});

test("момент из unix-секунд форматируется в локальное время", () => {
    const moment = new Date(2026, 9, 28, 9, 5);
    assert.equal(formatUnixMoment(moment.getTime() / 1000), "28.10.2026 09:05");
    assert.equal(formatUnixMoment(0), "");
    assert.equal(formatUnixMoment(undefined), "");
});

test("ISO-дата форматируется без перевода часового пояса", () => {
    assert.equal(formatIsoMoment("2026-10-28"), "28.10.2026");
    assert.equal(formatIsoMoment("2026-10-28T09:05:00"), "28.10.2026 09:05");
    assert.equal(formatIsoMoment("2026-10-28T09:05:00.000+03:00"), "28.10.2026 09:05");
});

test("список показывает ту же метку поступления, что и карточка", () => {
    const row = { receivedAt: "2026-10-25T09:30:00+07:00", sortAt: 0 };
    assert.equal(incomingReceivedLabel(row), formatIsoMoment(row.receivedAt));
    assert.equal(incomingReceivedLabel(row), "25.10.2026 09:30");
    assert.equal(incomingReceivedLabel({ sortAt: 1761363000 }), "");
});

test("номер накладной в списке экранируется, подпись без документа нет", () => {
    assert.equal(incomingDocumentLabel({ loose: false, number: `ТТН <5> & "a"` }), "ТТН &lt;5&gt; &amp; &quot;a&quot;");
    assert.equal(incomingDocumentLabel({ loose: true, number: "<b>" }), "Без документа");
});

test("неизменённый день карточки сохраняет момент курсора", () => {
    assert.equal(
        incomingFromForSave("2026-10-25T09:30:00+07:00", "2026-10-25"),
        "2026-10-25T09:30:00+07:00");
    assert.equal(incomingFromForSave("2026-10-25T09:30:00+07:00", "2026-10-20"), "2026-10-20");
    assert.equal(incomingFromForSave("2026-10-25T09:30:00+07:00", ""), "");
});

test("не-ISO значение даты возвращается как есть", () => {
    assert.equal(formatIsoMoment("вчера"), "вчера");
    assert.equal(formatIsoMoment(""), "");
    assert.equal(formatIsoMoment(null), "");
});
