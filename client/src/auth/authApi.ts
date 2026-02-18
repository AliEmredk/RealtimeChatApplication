import { api } from "../api/api";
import { tokenStore } from "./tokenStore";
import { LoginRequest, RegisterRequest } from "../generatedapi";

export async function login(username: string, password: string) {
    const res = await api.login(new LoginRequest({ username, password }));
    if (!res.token) throw new Error("No token returned from server.");
    tokenStore.set(res.token);
    return res;
}

export async function register(username: string, password: string) {
    const res = await api.register(new RegisterRequest({ username, password }));
    if (!res.token) throw new Error("No token returned from server.");
    tokenStore.set(res.token);
    return res;
}

export function logout() {
    tokenStore.clear();
}