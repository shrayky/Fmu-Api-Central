// js/views/index.js
import "../utils/customComponents.js";
import { InitProxy } from '../utils/proxy.js';
import { AuthService } from '../services/AuthService.js';
import { RouterService } from '../services/RouterService.js';
import { createLayout, createToolbarWithLogout, createSidebar } from '../components/Layout.js';
import { MENU_ITEMS } from '../config/menu.js';

import informationView from '../modules/informationView.js';
import createAuthView from '../modules/authView.js';
import createCouchDbSettingsView from '../modules/couchDbSettingsView.js';
import createServerSettingsView from '../modules/serverSettingsView.js';
import createLogsView from '../modules/logsView.js';
import createSoftwareUpdatesListView from '../modules/softwareUpdates/softwareUpdatesListView.js';
import createInstanceListView from '../modules/instanceMonitoring/instanceListView.js';
import createTelegramBotSettingsView from '../modules/telegramBotSettingsView.js';

class App {
    constructor() {
        this.router = new RouterService();
        this.bodyId = "body";
        this.initRoutes();
    }

    initRoutes() {
        this.router.register("information", () => informationView);
        this.router.register("couchDbSettings", async (id) => await createCouchDbSettingsView(id));
        this.router.register("serverSettings", async (id) => await createServerSettingsView(id));
        this.router.register("logs", async (id) => await createLogsView(id));
        this.router.register("softwareUpdates", async (id) => await createSoftwareUpdatesListView(id));
        this.router.register("instanceMonitoring", async (id) => await createInstanceListView(id));
        this.router.register("telegramBotSettings", async (id) => await createTelegramBotSettingsView(id));
    }

    createBlankLayout() {
        return createLayout({
            rows: [
                { id: this.bodyId }
            ]
        });
    }

    createMainLayout() {
        return createLayout({
            rows: [
                createToolbarWithLogout("Fmu-Api-Central", this.handleLogout.bind(this), AuthService.getServerUrl()),
                {
                    cols: [
                        createSidebar(
                            Object.values(MENU_ITEMS),
                            (id) => this.router.navigate(id, this.bodyId)
                        ),
                        { id: this.bodyId }
                    ]
                }
            ]
        });
    }

    async handleLogout() {
        await AuthService.logout();
        location.reload();
    }

    showAuthWindow() {
        webix.ui(createAuthView(async (serverUrl, login, password) => {
            try {
                await AuthService.login(serverUrl, login, password);
                this.switchToMainApp();
            } catch (error) {
                webix.message({ text: error.message, type: "error" });
            }
        })).show();
    }

    switchToMainApp() {
        const rootLayout = $$('root');
        if (rootLayout) {
            rootLayout.destructor();
        }
        this.initMainApp();
    }

    initMainApp() {
        webix.ui(this.createMainLayout());
        this.router.navigate("informationView", "body");
        AuthService.startTokenRefreshTimer();
    }

    async checkAndRefreshToken() {
        if (!AuthService.getToken()) {
            return false;
        }

        if (AuthService.isTokenExpired()) {
            try {
                await AuthService.refreshToken();
            } catch (error) {
                console.error('Ошибка обновления токена:', error);
                return false;
            }
        }

        return true;
    }

    showLoginScreen() {
        webix.ui(this.createBlankLayout());
        this.showAuthWindow();
    }

    async init() {
        InitProxy();
        webix.ready(async () => {
            const isAuthenticated = await this.checkAndRefreshToken();
            
            if (isAuthenticated) {
                this.initMainApp();
            } else {
                this.showLoginScreen();
            }

            this.setupWindowResizeHandler();
        });
    }

    setupWindowResizeHandler() {
        webix.event(window, "resize", () => {
            const root = $$("root");
            const body = $$(this.bodyId);
            root.$setSize(window.innerWidth, window.innerHeight);
        });
    }
}

const app = new App();
app.init();