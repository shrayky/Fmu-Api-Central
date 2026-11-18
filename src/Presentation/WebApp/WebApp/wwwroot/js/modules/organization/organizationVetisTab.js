import { Text, CheckBox, PasswordBox, Number as NumberField } from "../../utils/ui.js";
import { incomingFromForSave, positiveSetting } from "../../utils/vetisFormat.js";
import { fromDate, toDate } from "./organizationFormat.js";
import { enterprisesBody, openLetter, probe } from "./organizationVetisEnterprises.js";

export function vetisBody(view, vetis) {
    return {
        rows: [
            {
                view: "tabview",
                id: view.NAMES.vetisTabs,
                cells: [
                    { header: view.LABELS.vetisSettingsTab, body: vetisSettingsBody(view, vetis) },
                    { header: view.LABELS.vetisEnterprisesTab, body: enterprisesBody(view) }
                ]
            },
            {
                view: "template",
                id: view.NAMES.vetisStatus,
                height: 36,
                borderless: true,
                template: ""
            }
        ],
        on: {
            onViewShow() {
                $$(view.NAMES.vetisSettingsScroll)?.resize();
                $$(view.NAMES.vetisEnterprises)?.resize();
            }
        }
    };
}

export function setVetisFieldsEnabled(view, enabled) {
    const fields = $$(view.NAMES.vetisFields);
    if (fields) {
        if (enabled)
            fields.enable();
        else
            fields.disable();
    }

    const tabs = $$(view.NAMES.vetisTabs);
    const tabbar = tabs?.getTabbar?.();
    if (!tabbar)
        return;

    if (enabled) {
        tabbar.enableOption(view.NAMES.vetisEnterprisesTab);
        return;
    }

    if (tabs.getValue() === view.NAMES.vetisEnterprisesTab)
        tabs.setValue(view.NAMES.vetisSettingsScroll);
    tabbar.disableOption(view.NAMES.vetisEnterprisesTab);
}

export function vetisSettings(view) {
    const table = $$(view.NAMES.vetisEnterprises);
    const enterprises = table ? table.serialize().map(row => ({
        guid: row.guid || "",
        name: row.name || "",
        address: row.address || "",
        presentation: String(row.presentation || "").trim(),
        incomingFrom: incomingFromForSave(row.loadedIncomingFrom, fromDate(row.incomingFrom)),
        participates: !!row.participates
    })) : [];

    return {
        enable: !!$$(view.NAMES.vetisEnable)?.getValue(),
        apiKey: String($$(view.NAMES.apiKey)?.getValue() || ""),
        basicLogin: String($$(view.NAMES.apiUser)?.getValue() || ""),
        basicPassword: String($$(view.NAMES.apiPassword)?.getValue() || ""),
        issuerId: String($$(view.NAMES.issuerId)?.getValue() || ""),
        initiatorLogin: String($$(view.NAMES.operatorLogin)?.getValue() || ""),
        incomingFrom: fromDate($$(view.NAMES.vetisIncomingFrom)?.getValue()),
        autoQuench: !!$$(view.NAMES.autoQuench)?.getValue(),
        autoQuenchDelayHours: parseInt($$(view.NAMES.autoQuenchDelay)?.getValue(), 10),
        autoQuenchFrom: fromTime($$(view.NAMES.autoQuenchFrom)?.getValue()),
        autoQuenchTo: fromTime($$(view.NAMES.autoQuenchTo)?.getValue()),
        enterprises
    };
}

function vetisSettingsBody(view, vetis) {
    return {
        view: "scrollview",
        id: view.NAMES.vetisSettingsScroll,
        scroll: "y",
        body: {
            padding: 10,
            rows: [
                CheckBox(view.LABELS.enable, view.NAMES.vetisEnable, {
                    value: !!vetis.enable,
                    on: {
                        onChange: (enabled) => setVetisFieldsEnabled(view, enabled)
                    }
                }),
                {
                    id: view.NAMES.vetisFields,
                    disabled: !vetis.enable,
                    rows: [
                        {
                            cols: [
                                {
                                    view: "button",
                                    value: view.LABELS.pasteLetter,
                                    width: 200,
                                    click: () => openLetter(view)
                                },
                                {}
                            ]
                        },
                        Text(view.LABELS.apiKey, view.NAMES.apiKey, vetis.apiKey || ""),
                        Text(view.LABELS.apiUser, view.NAMES.apiUser, vetis.basicLogin || ""),
                        PasswordBox(view.LABELS.apiPassword, view.NAMES.apiPassword, {
                            value: vetis.basicPassword || ""
                        }),
                        {
                            view: "template",
                            template: view.LABELS.passwordHint,
                            autoheight: true,
                            borderless: true
                        },
                        Text(view.LABELS.issuerId, view.NAMES.issuerId, vetis.issuerId || ""),
                        Text(view.LABELS.operatorLogin, view.NAMES.operatorLogin, vetis.initiatorLogin || ""),
                        {
                            view: "datepicker",
                            id: view.NAMES.vetisIncomingFrom,
                            label: view.LABELS.incomingFrom,
                            labelPosition: "top",
                            editable: true,
                            format: "%d.%m.%Y",
                            value: toDate(vetis.incomingFrom)
                        },
                        {
                            view: "template",
                            template: view.LABELS.incomingHint,
                            autoheight: true,
                            borderless: true
                        },
                        CheckBox(view.LABELS.autoQuench, view.NAMES.autoQuench, {
                            value: !!vetis.autoQuench
                        }),
                        NumberField(view.LABELS.autoQuenchDelay, view.NAMES.autoQuenchDelay, vetis.autoQuenchDelayHours ?? 1, undefined, {
                            validate: (input) => positiveSetting(input),
                            invalidMessage: view.LABELS.positiveNumber
                        }),
                        {
                            cols: [
                                timeField(view.LABELS.autoQuenchFrom, view.NAMES.autoQuenchFrom, vetis.autoQuenchFrom, 9, 0),
                                timeField(view.LABELS.autoQuenchTo, view.NAMES.autoQuenchTo, vetis.autoQuenchTo, 17, 0),
                                {}
                            ]
                        },
                        {
                            cols: [
                                { view: "button", value: view.LABELS.check, width: 210, click: () => probe(view, "check") },
                                {}
                            ]
                        }
                    ]
                }
            ]
        }
    };
}

function timeField(label, id, value, fallbackHour, fallbackMinute) {
    return {
        view: "datepicker",
        type: "time",
        id,
        label,
        labelPosition: "top",
        editable: true,
        format: "%H:%i",
        width: 160,
        value: toTime(value, fallbackHour, fallbackMinute),
        suggest: {
            type: "calendar",
            body: {
                type: "time",
                calendarTime: "%H:%i"
            }
        }
    };
}

function toTime(value, fallbackHour, fallbackMinute) {
    const match = /^(\d{1,2}):(\d{2})$/.exec(String(value || "").trim());
    const hours = match ? Number(match[1]) : fallbackHour;
    const minutes = match ? Number(match[2]) : fallbackMinute;
    return new Date(2000, 0, 1, hours, minutes, 0);
}

function fromTime(value) {
    if (typeof value === "string") {
        const match = /^(\d{1,2}):(\d{2})/.exec(value.trim());
        if (!match)
            return "";

        const hours = Number(match[1]);
        const minutes = Number(match[2]);
        if (hours > 23 || minutes > 59)
            return "";

        return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
    }

    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime()))
        return "";

    return `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
}
