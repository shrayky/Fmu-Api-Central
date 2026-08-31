import { AuthService } from './AuthService.js';

class OrganizationService {
    constructor() {
        this.apiEndpoint = "/api/organization";
        this.authService = AuthService;
    }

    async list(pageNumber = 1, pageSize = 50) {
        const endpoint = `${this.apiEndpoint}?page=${pageNumber}&pageSize=${pageSize}`;
        const data = await this.authService.makeAuthenticatedRequest(endpoint);

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;
    }

    async create(payload) {
        const data = await this.authService.makeAuthenticatedRequest(this.apiEndpoint, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(payload)
        });

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;
    }

    async delete(id) {
        const data = await this.authService.makeAuthenticatedRequest(`${this.apiEndpoint}/${id}`, {
            method: "DELETE"
        });

        if (!data.result) {
            throw new Error(data.error);
        }

        return true;
    }

    async certificates() {
        const data = await this.authService.makeAuthenticatedRequest("/api/digitalSignature");

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value || [];
    }

    async getToken(inn) {
        const endpoint = `/api/ts/token/inn?inn=${encodeURIComponent(inn)}`;
        const data = await this.authService.makeAuthenticatedRequest(endpoint);

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;
    }
}

export default new OrganizationService();
