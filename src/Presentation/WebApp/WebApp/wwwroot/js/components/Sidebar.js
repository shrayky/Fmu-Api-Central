import { loadServerTime } from "../services/ConfigurationService.js";
import { serverClockText } from "../utils/serverClock.js";

const SIDEBAR_COLLAPSED_KEY = "sidebar_collapsed";
const CLOCK_SYNC_MS = 5 * 60 * 1000;

export class Sidebar {
    constructor({ items, onSelect, logo, logoText }) {
        this.items = items;
        this.onSelect = onSelect;
        this.logo = logo;
        this.logoText = logoText;
        this.sidebarId = "mainSidebar";
        this.logoId = "logoText";
        this.collapsedLogoId = "collapsedLogo";
        this.clockId = "serverClock";
        this._clockStarted = false;
        this._iso = "";
        this._syncedAt = 0;
    }

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
                            template: `<div style="text-align:left;line-height:56px;"><img src="${this.logo}" class="app-logo" alt=""/>${this.logoText}</div>`,
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
                },
                {
                    view: "template",
                    id: this.clockId,
                    borderless: true,
                    height: 36,
                    css: "sidebar-server-clock",
                    template: "",
                    hidden: collapsed,
                    on: {
                        onAfterRender: () => this._ensureClock(),
                        onDestruct: () => this._stopClock()
                    }
                },
                {
                    view: "template",
                    id: this.collapsedLogoId,
                    borderless: true,
                    height: 44,
                    css: "sidebar-collapsed-logo",
                    template: `<img src="${this.logo}" class="app-logo-collapsed" alt=""/>`,
                    hidden: !collapsed
                }
            ]
        };
    }

    /**
     * Переключает свёрнутость сайдбара и синхронизирует логотипы.
     */
    _toggle() {
        const sidebar = $$(this.sidebarId);
        sidebar.toggle();

        const isCollapsed = sidebar.getState().collapsed;
        this._saveCollapsed(isCollapsed);
        this._syncLogos(isCollapsed);
    }

    _syncLogos(isCollapsed) {
        const logo = $$(this.logoId);
        const collapsedLogo = $$(this.collapsedLogoId);
        const clock = $$(this.clockId);

        if (isCollapsed) {
            logo.hide();
            clock?.hide();
            collapsedLogo.show();
        } else {
            logo.show();
            clock?.show();
            collapsedLogo.hide();
        }
    }

    _ensureClock() {
        if (this._clockStarted)
            return;

        this._clockStarted = true;
        this._syncClock();
        this._tickTimer = setInterval(() => this._paint(), 1000);
        this._syncTimer = setInterval(() => this._syncClock(), CLOCK_SYNC_MS);
    }

    _stopClock() {
        clearInterval(this._tickTimer);
        clearInterval(this._syncTimer);
    }

    async _syncClock() {
        try {
            const result = await loadServerTime();
            if (result?.result && result.value?.local) {
                this._iso = String(result.value.local);
                this._syncedAt = Date.now();
            } else if (!this._iso) {
                this._iso = "";
            }
        } catch {
            if (!this._iso)
                this._iso = "";
        }

        this._paint();
    }

    _paint() {
        const view = $$(this.clockId);
        if (!view)
            return;

        const elapsed = this._iso ? Date.now() - this._syncedAt : 0;
        view.define("template", serverClockText(this._iso, elapsed));
        view.refresh();
    }

    _isMobile() {
        return window.innerWidth <= 768 || /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
    }

    _getCollapsed() {
        if (this._isMobile()) {
            return true;
        }

        return localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === "true";
    }

    _saveCollapsed(isCollapsed) {
        if (this._isMobile()) {
            return;
        }

        localStorage.setItem(SIDEBAR_COLLAPSED_KEY, isCollapsed.toString());
    }
}
