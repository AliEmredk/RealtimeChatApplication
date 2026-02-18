import { Navigate } from "react-router-dom";
import { isLoggedIn } from "./auth";
import type { ReactNode } from "react";

export default function ProtectedRoute({ children }: { children: ReactNode }) {
    if (!isLoggedIn()) {
        return <Navigate to="/login" replace />;
    }
    return <>{children}</>;
}
