import { AuthService } from './AuthService.js';

class VetisService {
    constructor() {
        this.apiEndpoint = "/api/vetis";
        this.authService = AuthService;
    }

    filters() {
        return this._get("filters");
    }

    incoming(filters, skip, limit) {
        return this._get("incoming", { ...filters, skip, limit });
    }

    incomingDocument(id) {
        return this._get(`incoming/${encodeURIComponent(id)}`);
    }

    applications(filters, skip, limit) {
        return this._get("applications", { ...filters, skip, limit });
    }

    application(id) {
        return this._get(`applications/${encodeURIComponent(id)}`);
    }

    async _get(path, query = {}) {
        const params = new URLSearchParams();
        for (const [key, value] of Object.entries(query)) {
            if (value !== undefined && value !== null && value !== "") {
                params.append(key, value);
            }
        }

        const suffix = params.size > 0 ? `?${params.toString()}` : "";
        const data = await this.authService.makeAuthenticatedRequest(`${this.apiEndpoint}/${path}${suffix}`);

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;
    }
}

export default new VetisService();
