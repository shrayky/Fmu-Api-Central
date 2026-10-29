const MARGIN = 40;

export function openVetisWindow({ id, title, width, height, body, fill }) {
    $$(id)?.destructor();

    const dialog = webix.ui({
        view: "window",
        id,
        modal: true,
        move: true,
        resize: true,
        position: fill ? placeOverWorkArea : "center",
        width: fill ? undefined : Math.min(width, window.innerWidth - MARGIN),
        height: fill ? undefined : Math.min(height, window.innerHeight - MARGIN),
        head: {
            view: "toolbar",
            elements: [
                { view: "label", label: title },
                { view: "icon", icon: "wxi-close", click: () => $$(id).close() },
            ],
        },
        body,
    });

    // Обработчик без view вызывается при любом фокусе. С view — только если фокус на этом элементе, а он остаётся в таблице.
    const closeOnEsc = () => {
        const opened = $$(id);
        if (!opened)
            return false;

        opened.close();
        return false;
    };
    webix.UIManager.addHotKey("esc", closeOnEsc, null);
    dialog.attachEvent("onDestruct", () => webix.UIManager.removeHotKey("esc", closeOnEsc));

    dialog.show();
    return dialog;
}

function placeOverWorkArea(state) {
    const node = $$("body")?.getNode?.();
    const rect = node ? node.getBoundingClientRect() : { left: 0, top: 0, width: window.innerWidth, height: window.innerHeight };
    state.left = rect.left;
    state.top = rect.top;
    state.width = rect.width;
    state.height = rect.height;
}

export function infoTemplate(lines, height) {
    const rows = lines
        .filter(([, value]) => value)
        .map(([name, value]) => `<div><b>${webix.template.escape(name)}:</b> ${webix.template.escape(value)}</div>`)
        .join("");

    return { view: "template", borderless: true, height, template: rows };
}
