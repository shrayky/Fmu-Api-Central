export class RouterService {
    constructor() {
        this.currentPage = "";
        this.routes = new Map();
    }

    register(id, viewFactory) {
        this.routes.set(id, viewFactory);
    }

    async navigate(id, bodyId) {
        if (id === this.currentPage) 
            return;

        const viewFactory = this.routes.get(id);
        if (!viewFactory) 
            return;

        const body = $$(bodyId);
        if (!body) 
            return;

        webix.extend(body, webix.ProgressBar);
        body.showProgress({ type: "icon", icon:"wxi-sync" });

        try {
            let view = viewFactory(bodyId);
            
            if (view instanceof Promise) {
                view = await view;
                webix.ui(view, $$(bodyId));
            }
            else {
                webix.ui(view(bodyId), $$(bodyId));
            }
            
            //body.hideProgress();
         
            $$(bodyId).show();
            this.currentPage = id;

        } catch (error) {
            //body.hideProgress();
            console.error("Ошибка при навигации:", error);
        }
    }
}