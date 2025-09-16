import { AuthService } from './AuthService.js';

class SoftwareUpdatesService {
    constructor() {
        this.apiEndpoint = "/api/softwareUpdates";
        this.authService = AuthService;
    }

    async loadUpdates(pageNumber = 1, pageSize = 50) {
        const endpoint = `${this.apiEndpoint}?page=${pageNumber}&pageSize=${pageSize}`;
        
        const data = await this.authService.makeAuthenticatedRequest(endpoint);

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;  
    }
            
    async createUpdate(updateData) {
        const endpoint = this.apiEndpoint;
        
        const data = await this.authService.makeAuthenticatedRequest(endpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(updateData)
        });

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value;
    }

    async attachFile(recordId, file) {
        const formData = new FormData();
        formData.append("file", file);
        
        const endpoint = `${this.apiEndpoint}/${recordId}/attach`;

        const data = await this.authService.makeAuthenticatedRequest(endpoint, {
            method: 'POST',
            body: formData
        });

        if (!data.result) {
            throw new Error(data.error);
        }

        return true;
    }

    async deleteUpdate(recordId) {
        const endpoint = `${this.apiEndpoint}/${recordId}`;
        
        const data = await this.authService.makeAuthenticatedRequest(endpoint, {
            method: "DELETE"
        });

        if (!data.result) {
            throw new Error(data.error);
        }

        return true;
    }

    async calculateFileHash(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();

            reader.onload = async (e) => {
                try {
                    const arrayBuffer = e.target.result;
                    const hashBuffer = await crypto.subtle.digest('SHA-256', arrayBuffer);
                    const hashArray = Array.from(new Uint8Array(hashBuffer));
                    const hashHex = hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
                    resolve(hashHex);
                } catch (error) {
                    reject(error);
                }
            };

            reader.onerror = () => reject(new Error('Ошибка чтения файла'));
            reader.readAsArrayBuffer(file);
        });
    }
}

export default new SoftwareUpdatesService();