import etrnService from '../../services/etrnService.js';
import { etrnOperatorLabel, etrnStatusLabel, formatIsoMoment } from '../../utils/etrnFormat.js';
import { incomingWindowClosable } from '../../utils/vetisFormat.js';
import { infoTemplate, openVetisWindow } from '../vetis/vetisWindow.js';
import { salePointBar } from '../organization/salePointPicker.js';

const WINDOW_ID = "etrnIncomingWindow";
const TABLE_ID = "etrnIncomingLines";
const INFO_ID = "etrnIncomingInfo";
const COMMENT_ID = "etrnIncomingComment";
const ERROR_ID = "etrnIncomingError";
const ACCEPT_ID = "etrnIncomingAccept";
const REJECT_ID = "etrnIncomingReject";
const CORRECTION_ID = "etrnIncomingCorrect";
const PROMPT_ID = "etrnCorrectionPrompt";
const PROMPT_NUMBER_ID = "etrnCorrectionNumber";
const PROMPT_DATE_ID = "etrnCorrectionDate";

let openedId = null;
let busyId = null;

export async function openEtrnIncomingWindow(row, { onAccepted, onSalePointChanged } = {}) {
    let document;
    let matched = false;
    try {
        document = await etrnService.incomingDocument(row.id);
        // Пустое поле пробуем заполнить по адресу выгрузки: ошибка подбора карточку не блокирует.
        if (!document.salePointUid) {
            try {
                document = await etrnService.matchSalePoint(document.id);
                matched = !!document.salePointUid;
            } catch {
                // Ручной выбор остаётся доступен.
            }
        }
    } catch (error) {
        webix.message({ text: error.message, type: "error" });
        return;
    }

    openedId = document.id;
    busyId = null;
    let pendingCell = null;
    let promptOpen = false;

    const dialog = openVetisWindow({        id: WINDOW_ID,
        title: `Документ № ${document.number} от ${formatIsoMoment(document.date)}`,
        fill: true,
        canClose: () => incomingWindowClosable(openedId, busyId),
        body: {
            rows: [
                { view: "template", id: INFO_ID, borderless: true, height: 200, template: infoHtml(document) },
                salePointBar({
                    prefix: "etrnIncoming",
                    document,
                    assign: (uid) => etrnService.assignSalePoint(document.id, uid),
                    addAlias: (id) => etrnService.addSalePointAlias(id),
                    onChanged: onSalePointChanged
                }),
                {
                    view: "toolbar",
                    elements: [
                        {
                            view: "button",
                            id: ACCEPT_ID,
                            value: "Подписать приёмку",
                            width: 190,
                            css: "webix_primary",
                            disabled: !document.canDecide,
                            hotkey: document.canDecide ? "alt+enter" : "",
                            click: () => send("sign"),
                        },
                        {
                            view: "button",
                            id: REJECT_ID,
                            value: "Отказать",
                            width: 140,
                            disabled: !document.canDecide,
                            click: () => send("reject"),
                        },
                        {
                            view: "button",
                            id: CORRECTION_ID,
                            value: "Отправить исправление",
                            width: 220,
                            hidden: !document.canCorrect,
                            click: () => askCorrection(),
                        },
                        {},
                    ],
                },
                { view: "template", id: ERROR_ID, borderless: true, autoheight: true, template: "" },
                {
                    view: "datatable",
                    id: TABLE_ID,
                    editable: document.canDecide,
                    editaction: "click",
                    gravity: 1,
                    columns: [
                        { id: "name", header: "Наименование", fillspace: true },
                        { id: "places", header: "Мест", width: 80 },
                        { id: "factPlaces", header: "Факт мест", width: 110, editor: "text" },
                        { id: "grossWeight", header: "Масса брутто", width: 130 },
                        { id: "factWeight", header: "Факт массы", width: 120, editor: "text" },
                        { id: "comment", header: "Комментарий строки", width: 300, editor: "text" },
                    ],
                    data: rows(document),
                    on: {
                        onItemClick(cell) {
                            pendingCell = cell;
                        },
                        onBeforeEditStart(id) {
                            const cell = pendingCell;
                            pendingCell = null;
                            if (!document.canDecide)
                                return false;
                            if (!cell || cell.row !== id)
                                return true;

                            const line = this.getItem(id);
                            if (cell.column === "factPlaces")
                                return !!line?.editablePlaces;
                            if (cell.column === "factWeight")
                                return !!line?.editableWeight;
                            return true;
                        },
                    },
                },
                {
                    view: "textarea",
                    id: COMMENT_ID,
                    label: "Общий комментарий",
                    labelPosition: "top",
                    height: 78,
                    readonly: !document.canDecide,
                    value: document.generalComment || "",
                },
            ],
        },
    });

    if (matched && onSalePointChanged)
        onSalePointChanged();

    if (!document.canDecide)
        setError(document.lastError);

    bindCorrectHotkey(document.canCorrect);
    dialog.attachEvent("onDestruct", () => {
        webix.UIManager.removeHotKey("alt+enter", onCorrectEnter);
        $$(PROMPT_ID)?.destructor();
    });

    async function send(intent) {
        if (busyId)
            return;

        const table = $$(TABLE_ID);
        const lines = (table?.serialize() ?? []).map((line) => ({
            index: line.index,
            factPlaces: Number.isInteger(line.factPlaces) ? line.factPlaces : parseInt(line.factPlaces, 10),
            factWeight: String(line.factWeight ?? "").trim(),
            comment: String(line.comment ?? "").trim(),
        }));

        busyId = document.id;
        setError("");
        dialog.disable();
        showStage(dialog, `Отправка в ${etrnOperatorLabel(document.operatorName, "saby")}`);

        try {
            const updated = await etrnService.accept(document.id, {
                intent,
                comment: String($$(COMMENT_ID)?.getValue() ?? ""),
                lines: lines.map((line) => ({
                    ...line,
                    factPlaces: Number.isNaN(line.factPlaces) ? null : line.factPlaces,
                })),
            });
            webix.message(`Титул отправлен: ${etrnStatusLabel(updated)}`);
            applyDocument(updated);
            if (onAccepted)
                await onAccepted();
        } catch (error) {
            setError(errorText(error.message));
        } finally {
            busyId = null;
            hideStage(dialog);
            if ($$(WINDOW_ID))
                dialog.enable();
        }
    }

    function applyDocument(updated) {
        document = updated;
        const info = $$(INFO_ID);
        if (info) {
            info.define("template", infoHtml(updated));
            info.refresh();
        }

        const table = $$(TABLE_ID);
        if (table) {
            table.define("editable", !!updated.canDecide);
            table.clearAll();
            table.parse(rows(updated));
            table.refresh();
        }

        const comment = $$(COMMENT_ID);
        if (comment) {
            comment.setValue(updated.generalComment || "");
            comment.define("readonly", !updated.canDecide);
            comment.refresh();
        }

        if (!updated.canDecide) {
            $$(ACCEPT_ID)?.disable();
            $$(REJECT_ID)?.disable();
        }
        $$(ACCEPT_ID)?.define("hotkey", updated.canDecide ? "alt+enter" : "");

        const correction = $$(CORRECTION_ID);
        if (correction)
            updated.canCorrect ? correction.show() : correction.hide();
        bindCorrectHotkey(!!updated.canCorrect);

        setError(updated.lastError);
    }

    function onCorrectEnter() {
        if (promptOpen || !document.canCorrect)
            return false;

        askCorrection();
        return false;
    }

    function bindCorrectHotkey(enabled) {
        webix.UIManager.removeHotKey("alt+enter", onCorrectEnter);
        if (enabled)
            webix.UIManager.addHotKey("alt+enter", onCorrectEnter);
    }

    function askCorrection() {
        if (busyId || promptOpen)
            return;

        promptOpen = true;
        webix.UIManager.removeHotKey("alt+enter", onCorrectEnter);
        dialog.suspendEsc?.();
        const onKey = (event) => {
            if (event.key === "Escape") {
                event.preventDefault();
                event.stopPropagation();
                closePrompt();
            } else if (event.altKey && event.key === "Enter") {
                event.preventDefault();
                event.stopPropagation();
                submitPrompt();
            }
        };
        const closePrompt = () => {
            window.removeEventListener("keydown", onKey, true);
            $$(PROMPT_ID)?.close();
        };

        const prompt = webix.ui({
            view: "window",
            id: PROMPT_ID,
            modal: true,
            move: true,
            position: "center",
            escHide: false,
            width: Math.min(420, window.innerWidth - 40),
            head: {
                view: "toolbar",
                elements: [
                    { view: "label", label: "Исправление" },
                    { view: "icon", icon: "wxi-close", click: closePrompt },
                ],
            },
            body: {
                padding: 12,
                rows: [
                    {
                        view: "text",
                        id: PROMPT_NUMBER_ID,
                        label: "Номер исправления",
                        labelPosition: "top",
                        clear: true,
                        value: document.correctionNumber > 0 ? String(document.correctionNumber) : "",
                    },
                    {
                        view: "datepicker",
                        id: PROMPT_DATE_ID,
                        label: "Дата исправления",
                        labelPosition: "top",
                        editable: true,
                        format: "%d.%m.%Y",
                        value: storedCorrectionDate(document.correctionDate),
                    },
                    {
                        padding: { top: 8 },
                        cols: [
                            {
                                view: "button",
                                value: "Отправить",
                                width: 140,
                                css: "webix_primary",
                                click: () => submitPrompt(),
                            },
                            {},
                        ],
                    },
                ],
            },
        });
        prompt.attachEvent("onDestruct", () => {
            promptOpen = false;
            window.removeEventListener("keydown", onKey, true);
            if ($$(WINDOW_ID) && !dialog.$destructed) {
                dialog.resumeEsc?.();
                bindCorrectHotkey(!!document.canCorrect);
            }
        });
        window.addEventListener("keydown", onKey, true);
        prompt.show();
        $$(PROMPT_NUMBER_ID)?.focus();

        function submitPrompt() {
            const number = Number(String($$(PROMPT_NUMBER_ID)?.getValue() ?? "").trim());
            const date = readCorrectionDate();
            if (!Number.isInteger(number) || number < 1 || number > 999) {
                webix.message({ type: "error", text: "Номер исправления: целое число от 1 до 999" });
                return;
            }
            if (!/^\d{2}\.\d{2}\.\d{4}$/.test(date)) {
                webix.message({ type: "error", text: "Дата исправления: дата в формате ДД.ММ.ГГГГ" });
                return;
            }

            document.correctionNumber = number;
            document.correctionDate = date;
            closePrompt();
            sendCorrection(number, date);
        }
    }

    async function sendCorrection(number, date) {
        if (busyId)
            return;

        busyId = document.id;
        setError("");
        dialog.disable();
        showStage(dialog, "Отправка исправления");

        try {
            const updated = await etrnService.accept(document.id, {
                correction: true,
                correctionNumber: number,
                correctionDate: date,
            });
            webix.message("Исправление отправлено");
            applyDocument(updated);
            if (onAccepted)
                await onAccepted();
        } catch (error) {
            setError(errorText(error.message));
        } finally {
            busyId = null;
            hideStage(dialog);
            if ($$(WINDOW_ID))
                dialog.enable();
        }
    }

    function setError(text) {
        const view = $$(ERROR_ID);
        if (!view)
            return;

        const value = String(text ?? "").trim();
        view.define("template", value
            ? `<div style="color:#9b2335;white-space:normal;line-height:1.4;">${webix.template.escape(value)}</div>`
            : "");
        view.refresh();
    }
}

function storedCorrectionDate(text) {
    const match = /^(\d{2})\.(\d{2})\.(\d{4})$/.exec(String(text ?? "").trim());
    if (!match)
        return null;

    const date = new Date(Number(match[3]), Number(match[2]) - 1, Number(match[1]));
    return Number.isNaN(date.getTime()) ? null : date;
}

function readCorrectionDate() {
    const value = $$(PROMPT_DATE_ID)?.getValue();
    if (value instanceof Date && !Number.isNaN(value.getTime()))
        return webix.Date.dateToStr("%d.%m.%Y")(value);

    return String(value ?? "").trim();
}

function rows(document) {
    return (document.lines || []).map((line) => {
        const decided = document.status !== "Waiting";
        return {
            id: line.index,
            index: line.index,
            name: line.name,
            places: line.places ?? "",
            factPlaces: line.factPlaces ?? (decided ? "" : line.places ?? ""),
            grossWeight: line.grossWeight ?? "",
            factWeight: line.factWeight || (decided ? "" : line.grossWeight ?? ""),
            comment: line.comment || "",
            editablePlaces: !!line.editablePlaces,
            editableWeight: !!line.editableWeight,
        };
    });
}

function infoHtml(document) {
    return infoLines(document)
        .filter(([, value]) => value)
        .map(([name, value]) => `<div><b>${webix.template.escape(name)}:</b> ${webix.template.escape(value)}</div>`)
        .join("");
}

function infoLines(document) {
    return [
        ["Номер", document.number],
        ["Дата", formatIsoMoment(document.date)],
        ["Организация", document.organizationName],
        ["Оператор", document.operatorName],
        ["Грузоотправитель", document.consignor],
        ["Адрес выгрузки", document.deliveryAddress],
        ["УИД ГИС ЭПД", document.uid],
        ["Статус", etrnStatusLabel(document)],
        ["Ошибка", document.lastError],
    ];
}

function showStage(dialog, text) {
    const host = dialog.getNode();
    let stage = host.querySelector(".vetis-quench-stage");
    if (!stage) {
        stage = window.document.createElement("div");
        stage.className = "vetis-quench-stage";
        stage.innerHTML = "<div class='vetis-quench-card'><span class='vetis-quench-spin'></span><div class='vetis-quench-text'></div></div>";
        host.appendChild(stage);
    }

    stage.querySelector(".vetis-quench-text").textContent = text;
}

function hideStage(dialog) {
    dialog?.getNode()?.querySelector(".vetis-quench-stage")?.remove();
}

function errorText(raw) {
    const text = String(raw ?? "").trim();
    if (text.startsWith("\"") && text.endsWith("\"")) {
        try {
            return JSON.parse(text);
        } catch {
            return text;
        }
    }

    return text;
}
