import { AuthService } from './AuthService.js';

class CrptViolationsService {
    constructor() {
        this.apiEndpoint = "/api/crptViolations";
        this.authService = AuthService;
    }

    async list(pageNumber = 1, pageSize = 50, filters = {}) {
        const params = new URLSearchParams({
            page: pageNumber,
            pageSize: pageSize
        });

        if (filters.inn) {
            params.append("inn", filters.inn);
        }

        if (filters.dateFrom) {
            params.append("dateFrom", filters.dateFrom);
        }

        if (filters.dateTo) {
            params.append("dateTo", filters.dateTo);
        }

        const data = await this.authService.makeAuthenticatedRequest(
            `${this.apiEndpoint}?${params.toString()}`
        );

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;
    }

    async load(inn) {
        const params = inn ? `?inn=${encodeURIComponent(inn)}` : "";
        const data = await this.authService.makeAuthenticatedRequest(
            `${this.apiEndpoint}/load${params}`,
            { method: "POST" }
        );

        if (!data.result) {
            throw new Error(data.error);
        }

        return true;
    }
}

export default new CrptViolationsService();
