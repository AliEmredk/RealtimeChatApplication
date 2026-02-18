const TOKEN_KEY = "auth_token";

export const tokenStore = {
    get(): string | null {
        return sessionStorage.getItem(TOKEN_KEY);
    },
    set(token: string) {
        sessionStorage.setItem(TOKEN_KEY, token);
    },
    clear() {
        sessionStorage.removeItem(TOKEN_KEY);
    },
};
