import { tokenStore } from "./tokenStore";

export function isLoggedIn() {
    return !!tokenStore.get();
}
