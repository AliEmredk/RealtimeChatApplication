import { useState } from "react";
import { login, register } from "../auth/authApi";
import { useNavigate } from "react-router-dom";


export default function LoginPage() {
    const [username, setUsername] = useState("");
    const [password, setPassword] = useState("");
    const [mode, setMode] = useState<"login" | "register">("login");
    const [error, setError] = useState<string | null>(null);
    const [info, setInfo] = useState<string | null>(null);
    const navigate = useNavigate();


    async function onSubmit(e: React.FormEvent) {
        e.preventDefault();
        setError(null);
        setInfo(null);

        try {
            const res =
                mode === "login"
                    ? await login(username, password)
                    : await register(username, password);

            setInfo(`Logged in as ${res.username} (${res.userId})`);
            navigate("/chat", { replace: true });
        } catch (err: any) {
            console.error(err);
            setError(err?.message ?? "Something went wrong");
        }
    }


    return (
        <div
            style={{
                minHeight: "100vh",
                display: "flex",
                justifyContent: "center",
                alignItems: "center",
                background: "#111",        // optional dark bg
            }}
        >
            <div
                style={{
                    width: 320,
                    padding: 24,
                    borderRadius: 12,
                    background: "#1a1a1a",
                    boxShadow: "0 10px 30px rgba(0,0,0,0.4)",
                    display: "flex",
                    flexDirection: "column",
                    gap: 16,
                }}
            >
                <h1 style={{ textAlign: "center", margin: 0 }}>
                    {mode === "login" ? "Login" : "Register"}
                </h1>

                <form
                    onSubmit={onSubmit}
                    style={{ display: "flex", flexDirection: "column", gap: 12 }}
                >
                    <input
                        placeholder="username"
                        value={username}
                        onChange={(e) => setUsername(e.target.value)}
                        style={{ padding: 10, borderRadius: 8 }}
                    />

                    <input
                        placeholder="password"
                        type="password"
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        style={{ padding: 10, borderRadius: 8 }}
                    />

                    <button type="submit">
                        {mode === "login" ? "Login" : "Register"}
                    </button>

                    <button
                        type="button"
                        onClick={() =>
                            setMode(mode === "login" ? "register" : "login")
                        }
                    >
                        Switch to {mode === "login" ? "Register" : "Login"}
                    </button>

                    <button type="button" onClick={() => navigate("/chat")}>
                        Continue as Guest
                    </button>
                </form>

                {error && (
                    <p style={{ color: "crimson", textAlign: "center" }}>
                        {error}
                    </p>
                )}
                {info && <p style={{ textAlign: "center" }}>{info}</p>}
            </div>
        </div>
    );


}
