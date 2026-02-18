import { Client } from "../generatedapi";
import { tokenStore } from "../auth/tokenStore";

const baseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

export const api = new Client(baseUrl, {
    fetch: (url: RequestInfo, init?: RequestInit) => {
        const token = tokenStore.get();

        const headers = new Headers(init?.headers);

        // Add Authorization automatically for protected endpoints
        if (token) headers.set("Authorization", `Bearer ${token}`);

        return fetch(url, { ...init, headers });
    },
});
