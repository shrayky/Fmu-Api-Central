import { AuthService } from './AuthService.js';

class InstanceMonitoringService {
    constructor() {
        this.apiEndpoint = "/api/fmuApiInstance";
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

        if (filters.localModuleVersion) {
            params.append("localModuleVersion", filters.localModuleVersion);
        }

        if (filters.tsPiotVersion) {
            params.append("tsPiotVersion", filters.tsPiotVersion);
        }

        if (filters.tsPiotLicense) {
            params.append("tsPiotLicense", filters.tsPiotLicense);
        }

        const endpoint = `${this.apiEndpoint}?${params.toString()}`;
        
        const data = await this.authService.makeAuthenticatedRequest(endpoint);

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;  
    }
            
    async create(data) {
        const endpoint = this.apiEndpoint;
        
        const answer = await this.authService.makeAuthenticatedRequest(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(data)
        });

        if (!answer.result) {
            throw new Error(answer.error);
        }

        return answer.value;
    }

    async delete(recordId) {
        const endpoint = `${this.apiEndpoint}/${recordId}`;
        
        const data = await this.authService.makeAuthenticatedRequest(endpoint, {
            method: "DELETE"
        });

        if (!data.result) {
            throw new Error(data.error);
        }

        return true;
    }

}

export default new InstanceMonitoringService();