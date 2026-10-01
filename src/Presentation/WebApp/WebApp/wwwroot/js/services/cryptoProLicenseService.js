import { AuthService } from './AuthService.js';

class CryptoProLicenseService {
    constructor() {
        this.apiEndpoint = "/api/cryptoProLicense";
        this.authService = AuthService;
    }

    async view() {
        const data = await this.authService.makeAuthenticatedRequest(this.apiEndpoint);
        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value?.text || "";
    }

    async set(serial) {
        const data = await this.authService.makeAuthenticatedRequest(this.apiEndpoint, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ serial })
        });

        if (!data.result) {
            throw new Error(data.error);
        }

        return data.value?.text || "";
    }
}

export default new CryptoProLicenseService();
