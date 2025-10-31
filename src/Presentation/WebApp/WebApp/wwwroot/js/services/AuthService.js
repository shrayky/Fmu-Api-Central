export const AuthService = {
    async login(serverUrl, login, password) {
        const url = `${serverUrl}/api/Auth/login`;
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ login, password })
        });
        if (!response.ok) {
            throw new Error('Ошибка авторизации');
        }
        const data = await response.json();
        
        localStorage.setItem('jwtToken', data.accessToken);
        localStorage.setItem('refreshToken', data.refreshToken);
        localStorage.setItem('tokenExpiresAt', data.expiresAt);
        localStorage.setItem('username', login);
        localStorage.setItem('serverUrl', serverUrl);
        
        return data;
    },

    async refreshToken() {
        const refreshToken = localStorage.getItem('refreshToken');
        const serverUrl = this.getServerUrl();
        
        if (!refreshToken) {
            throw new Error('Refresh токен не найден');
        }

        const url = `${serverUrl}/api/Auth/refresh`;
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ refreshToken })
        });

        if (!response.ok) {
            this.logout();
            throw new Error('Ошибка обновления токена');
        }

        const data = await response.json();
        localStorage.setItem('jwtToken', data.accessToken);
        localStorage.setItem('tokenExpiresAt', data.expiresAt);
        
        return data;
    },

    async logout() {
        const refreshToken = localStorage.getItem('refreshToken');
        const serverUrl = this.getServerUrl();
        
        if (refreshToken) {
            try {
                const url = `${serverUrl}/api/Auth/logout`;
                await fetch(url, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ refreshToken })
                });
            } catch (error) {
                console.warn('Ошибка при выходе на сервере:', error);
            }
        }

        localStorage.removeItem('jwtToken');
        localStorage.removeItem('refreshToken');
        localStorage.removeItem('tokenExpiresAt');
        localStorage.removeItem('username');
    },

    getToken() {
        return localStorage.getItem('jwtToken');
    },

    getUsername() {
        return localStorage.getItem('username');
    },

    getServerUrl() {
        return localStorage.getItem('serverUrl') || 'http://localhost:2578';
    },

    isTokenExpired() {
        const expiresAt = localStorage.getItem('tokenExpiresAt');
        if (!expiresAt) return true;
        
        const expiryTime = new Date(expiresAt);
        const now = new Date();
        const bufferTime = 5 * 60 * 1000; // 5 минут буфера
        
        return now.getTime() + bufferTime >= expiryTime.getTime();
    },

    async getValidToken() {
        if (this.isTokenExpired()) {
            try {
                await this.refreshToken();
            } catch (error) {
                this.redirectToLogin();
                throw error;
            }
        }
        return this.getToken();
    },

    redirectToLogin() {
        this.logout();
        location.reload();
    },

    async makeAuthenticatedRequest(url, options = {}) {
        url = this.getServerUrl() + url;

        try {
            const token = await this.getValidToken();
            
            const requestOptions = {
                ...options,
                headers: {
                    ...options.headers,
                    'Authorization': `Bearer ${token}`
                }
            };
    
            const response = await fetch(url, requestOptions);
            
            if (response.status === 401) {
                this.redirectToLogin();
                return { result: false, error: 'Unauthorized', value: null };
            }
            
            if (!response.ok) {
                const errorText = await response.text();
                return { result: false, error: errorText, value: null };
            }
            
            try {
                const data = await response.json();
                return { result: true, error: null, value: data };
            } catch (error) {
            }

            try {
                const data = await response.blob();
                return { result: true, error: null, value: data };
            }
            catch (error) {
            }

            return { result: true, error: "нет данных в ответе", value: "" };
            
        } catch (error) {
            if (error.message.includes('обновления токена') || error.message.includes('Refresh токен не найден')) {
                this.redirectToLogin();
            }
            return { result: false, error: error.message, value: null };
        }
    },

    startTokenRefreshTimer() {
        setInterval(async () => {
            if (this.isTokenExpired()) {
                try {
                    await this.refreshToken();
                    console.log('Токен автоматически обновлен');
                } catch (error) {
                    console.error('Ошибка автоматического обновления токена:', error);
                    this.redirectToLogin();
                }
            }
        }, 60000); // Проверка каждую минуту
    }
};