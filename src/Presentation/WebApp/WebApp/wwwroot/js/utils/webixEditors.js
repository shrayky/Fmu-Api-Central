let accurateTimeEditorRegistered = false;

export function registerWebixEditors() {
    if (accurateTimeEditorRegistered) return;

    webix.editors.$popup.accurateTimePopup = {
        view: "popup",
        point: true,
        width: 220,
        body: {
            view: "form",
            elementsConfig: { labelWidth: 70 },
            elements: [
                { view: "counter", name: "hour", min: 0, max: 23, label: "Час" },
                { view: "counter", name: "min", min: 0, max: 59, label: "Минута" },
                { view: "counter", name: "sec", min: 0, max: 59, label: "Секунда" },
                {
                    cols: [
                        { view: "button", value: "Cancel", name: "cancel" },
                        { view: "button", value: "OK", css: "webix_primary", name: "ok" }
                    ]
                }
            ]
        }
    };

    webix.editors.dateTime = webix.extend({
        getInputNode: function () {
            return this.getPopup().getBody();
        },
        getValue: function () {
            const v = this.getInputNode().getValues();
            const date = new Date();
            date.setHours(v.hour || 0);
            date.setMinutes(v.min || 0);
            date.setSeconds(v.sec || 0);
            date.setMilliseconds(0);
            return date;
        },
        setValue: function (value) {
            this.master = $$(this.node);
            this.getPopup().show(this.node);

            const source = value instanceof Date ? value : new Date();
            this.getInputNode().setValues({
                hour: source.getHours(),
                min: source.getMinutes(),
                sec: source.getSeconds()
            });
        },
        popupType: "accurateTimePopup",
        popupInit: function (popup) {
            const editor = this;
            popup.getBody().elements.ok.attachEvent("onItemClick", function () {
                if (editor.master) editor.master.editStop();
            });
            popup.getBody().elements.cancel.attachEvent("onItemClick", function () {
                if (editor.master) editor.master.editCancel();
            });
        }
    }, webix.editors.popup);

    accurateTimeEditorRegistered = true;
}