import { AuthService } from './AuthService.js';

class MarkCheckStatisticsService {
    constructor() {
        this.apiEndpoint = "/api/markCheckStatistics";
        this.authService = AuthService;
    }

    async list(pageNumber = 1, pageSize = 50, filters = {}) {
        const params = new URLSearchParams({
            page: pageNumber,
            pageSize: pageSize
        });

        if (filters.name) {
            params.append("name", filters.name);
        }

        if (filters.successRateMin != null && filters.successRateMin !== "") {
            params.append("successRateMin", filters.successRateMin);
        }

        if (filters.offlineRateMin != null && filters.offlineRateMin !== "") {
            params.append("offlineRateMin", filters.offlineRateMin);
        }

        if (filters.dateFrom) {
            params.append("dateFrom", filters.dateFrom);
        }

        if (filters.dateTo) {
            params.append("dateTo", filters.dateTo);
        }

        const endpoint = `${this.apiEndpoint}?${params.toString()}`;

        const data = await this.authService.makeAuthenticatedRequest(endpoint);

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;
    }
}

export default new MarkCheckStatisticsService();
