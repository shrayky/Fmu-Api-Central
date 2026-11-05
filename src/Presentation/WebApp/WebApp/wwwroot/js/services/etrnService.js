import { AuthService } from './AuthService.js';

class EtrnService {
    constructor() {
        this.apiEndpoint = "/api/etrn";
        this.authService = AuthService;
    }

    filters() {
        return this._get("filters");
    }

    jobs() {
        return this._get("jobs");
    }

    incoming(filters, skip, limit) {
        return this._get("incoming", { ...filters, skip, limit });
    }

    incomingDocument(id) {
        return this._get(`incoming/${encodeURIComponent(id)}`);
    }

    accept(id, payload) {
        return this._send(`incoming/${encodeURIComponent(id)}/accept`, payload);
    }

    requests(filters, skip, limit) {
        return this._get("requests", { ...filters, skip, limit });
    }

    request(id) {
        return this._get(`requests/${encodeURIComponent(id)}`);
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

    async _send(path, payload) {
        const data = await this.authService.makeAuthenticatedRequest(`${this.apiEndpoint}/${path}`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload),
        });

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;
    }
}

export default new EtrnService();
