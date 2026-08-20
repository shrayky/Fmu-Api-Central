import { AuthService } from './AuthService.js';

class DatabaseDumpService {
    constructor() {
        this.apiEndpoint = "/api/databaseDump";
        this.authService = AuthService;
    }

    /**
     * Скачивает zip-архив выгрузки узлов и статистики проверок.
     */
    async export() {
        const token = await this.authService.getValidToken();
        const response = await fetch(`${this.authService.getServerUrl()}${this.apiEndpoint}/export`, {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });

        if (response.status === 401) {
            this.authService.redirectToLogin();
            return { result: false, error: 'Unauthorized', value: null };
        }

        if (!response.ok) {
            const errorText = await response.text();
            return { result: false, error: errorText, value: null };
        }

        const blob = await response.blob();
        return {
            result: true,
            error: null,
            value: {
                blob,
                fileName: this._fileNameFromDisposition(response.headers.get('Content-Disposition'))
            }
        };
    }

    /**
     * Загружает zip-архив и импортирует JSON-пакеты.
     */
    async import(file) {
        const formData = new FormData();
        formData.append("file", file);

        return await this.authService.makeAuthenticatedRequest(`${this.apiEndpoint}/import`, {
            method: 'POST',
            body: formData
        });
    }

    _fileNameFromDisposition(disposition) {
        const fallback = "fmu-api-central-data.zip";
        if (!disposition)
            return fallback;

        const utfMatch = disposition.match(/filename\*=UTF-8''([^;]+)/i);
        if (utfMatch)
            return decodeURIComponent(utfMatch[1]);

        const match = disposition.match(/filename="?([^"]+)"?/i);
        if (match)
            return match[1];

        return fallback;
    }
}

export default new DatabaseDumpService();
