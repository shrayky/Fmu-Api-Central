const SIDEBAR_COLLAPSED_KEY = "sidebar_collapsed";

export class Sidebar {
    constructor({ items, onSelect, logo, logoText }) {
        this.items = items;
        this.onSelect = onSelect;
        this.logo = logo;
        this.logoText = logoText;
        this.sidebarId = "mainSidebar";
        this.logoId = "logoText";
    }

    /**
     * Возвращает конфиг Webix для вставки в layout.
     */
    getView() {
        const collapsed = this._getCollapsed();

        return {
            rows: [
                {
                    id: "toolbarLabel",
                    view: "label",
                    hidden: true
                },
                {
                    padding: 2,
                    border: 1,
                    height: 60,
                    cols: [
                        {
                            view: "template",
                            id: this.logoId,
                            borderless: true,
                            css: "webix_primary",
                            minWidth: 200,
                            template: `<div style="text-align:left;line-height:56px;"><img src="${this.logo}" style="height:28px;vertical-align:middle;margin-right:8px;"/>${this.logoText}</div>`,
                            hidden: collapsed
                        },
                        {},
                        {
                            view: "icon",
                            icon: "mdi mdi-menu",
                            click: () => this._toggle()
                        }
                    ]
                },
                {
                    view: "sidebar",
                    id: this.sidebarId,
                    width: 220,
                    collapsed: collapsed,
                    position: this._isMobile() ? "right" : "left",
                    data: this.items,
                    on: {
                        onAfterSelect: this.onSelect
                    },
                    borderless: true
                }
            ]
        };
    }

    /**
     * Переключает свёрнутость сайдбара и синхронизирует логотип.
     */
    _toggle() {
        const sidebar = $$(this.sidebarId);
        sidebar.toggle();

        const isCollapsed = sidebar.getState().collapsed;
        this._saveCollapsed(isCollapsed);

        const logo = $$(this.logoId);
        if (isCollapsed) {
            logo.hide();
        } else {
            logo.show();
        }
    }

    /**
     * Определяет, открыто ли приложение на мобильном устройстве.
     */
    _isMobile() {
        return window.innerWidth <= 768 || /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
    }

    /**
     * Возвращает сохранённое состояние сайдбара. На мобильном всегда свёрнут.
     */
    _getCollapsed() {
        if (this._isMobile()) {
            return true;
        }

        return localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === "true";
    }

    /**
     * Сохраняет состояние сайдбара для следующей сессии (только десктоп).
     */
    _saveCollapsed(isCollapsed) {
        if (this._isMobile()) {
            return;
        }

        localStorage.setItem(SIDEBAR_COLLAPSED_KEY, isCollapsed.toString());
    }
}
