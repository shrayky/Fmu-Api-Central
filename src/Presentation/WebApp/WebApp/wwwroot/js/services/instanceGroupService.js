import { AuthService } from './AuthService.js';

class InstanceGroupService {
    constructor() {
        this.apiEndpoint = "/api/instanceGroup";
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

    async allLinks() {
        const data = await this.authService.makeAuthenticatedRequest(`${this.apiEndpoint}/links/all`);

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value || [];
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

    async assignForcedUpdate(groupIds, updateId) {
        const data = await this.authService.makeAuthenticatedRequest(`${this.apiEndpoint}/force-update`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ groupIds, updateId })
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
}

export default new InstanceGroupService();
