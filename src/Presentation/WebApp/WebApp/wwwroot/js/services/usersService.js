import { AuthService } from './AuthService.js';

class UsersService {
    constructor() {
        this.apiEndpoint = "/api/users";
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

    async update(payload) {
        const data = await this.authService.makeAuthenticatedRequest(this.apiEndpoint, {
            method: "PUT",
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

    async changePassword(id, password) {
        const data = await this.authService.makeAuthenticatedRequest(`${this.apiEndpoint}/${id}/password`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ password })
        });

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;
    }
}

export default new UsersService();
