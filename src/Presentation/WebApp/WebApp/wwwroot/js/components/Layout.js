export const createLayout = (config) => ({
    container: "app",
    type: "space",
    id: "root",
    responsive: true,
    ...config
});

export const createToolbar = (label) => ({
    view: "toolbar",
    padding: 5,
    height: 60,
    elements: [
        {
            view: "label",
            id: "toolbarLabel",
            label
        }
    ]
});

export const createToolbarWithLogout = (label, onLogout, serverUrl) => {
    const toolbar = createToolbar(label);
    toolbar.elements = [
        {
            view: "label",
            id: "toolbarLabel",
            label
        },
        {},
        {
            view: "label",
            label: `${serverUrl.replace('http://', '')}`,
            css: "webix_secondary"
        },
        {},
        {
            view: "button",
            value: "Выход",
            width: 80,
            height: 40,
            css: "webix_primary",
            on: {
                onItemClick: onLogout
            }
        }
    ];
    return toolbar;
};

export const createSidebar = (items, onSelect) => ({
    view: "sidebar",
    id: "sidebar",
    width: 200,
    collapsed: false,
    data: items,
    on: {
        onAfterSelect: onSelect
    }
});