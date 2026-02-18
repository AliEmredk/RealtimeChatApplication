import { useEffect, useMemo, useState } from "react";
import { api } from "../api/api";
import { CreateMessageRequest, MessageDto } from "../generatedapi";
import { tokenStore } from "../auth/tokenStore";
import { useNavigate } from "react-router-dom";
import { logout } from "../auth/authApi";

function parseJwt(token: string): any | null {
    try {
        const payload = token.split(".")[1];
        const json = atob(payload.replace(/-/g, "+").replace(/_/g, "/"));
        return JSON.parse(json);
    } catch {
        return null;
    }
}

function getRolesFromJwt(token: string): string[] {
    const payload = parseJwt(token);
    if (!payload) return [];

    // common claim names
    const role =
        payload["role"] ??
        payload["roles"] ??
        payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"];

    if (!role) return [];
    return Array.isArray(role) ? role : [String(role)];
}

export default function ChatPage() {
    const [room, setRoom] = useState("general");
    const [messages, setMessages] = useState<MessageDto[]>([]);
    const [text, setText] = useState("");
    const [error, setError] = useState<string | null>(null);

    const [rooms, setRooms] = useState<string[]>([]);
    const [newRoomName, setNewRoomName] = useState("");
    const [busy, setBusy] = useState(false);

    const navigate = useNavigate();
    const token = tokenStore.get();

    const [dmMode, setDmMode] = useState(false);
    const [dmRecipientId, setDmRecipientId] = useState("");

    const [online, setOnline] = useState<number>(0);

    const isAdmin = useMemo(() => {
        if (!token) return false;
        return getRolesFromJwt(token).includes("Admin");
    }, [token]);

    const baseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

    async function reloadRooms() {
        try {
            const r = await fetch(`${baseUrl}/rooms`);
            const data = await r.json();
            if (Array.isArray(data)) setRooms(data);
        } catch {
            // ignore
        }
    }

    // Load rooms list (public)
    useEffect(() => {
        reloadRooms();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    // Load last messages whenever room changes (public)
    useEffect(() => {
        let cancelled = false;
        setMessages([]);

        async function load() {
            setError(null);
            try {
                const data = await api.messagesAll(room, 20);
                if (!cancelled) setMessages(data ?? []);
            } catch (e: any) {
                if (!cancelled) setError(e?.message ?? "Failed to load messages");
            }
        }

        load();
        return () => {
            cancelled = true;
        };
    }, [room]);

    // SSE: live updates for this room (public)
    useEffect(() => {
        const roomName = room.trim();
        const eventName = `room:${roomName}`;

        const userId = token ? getUserIdFromJwt(token) : null;

        // if logged in -> listen-auth, else -> listen
        const url = token
            ? `${baseUrl}/rooms/${encodeURIComponent(roomName)}/listen-auth?access_token=${encodeURIComponent(token)}`
            : `${baseUrl}/rooms/${encodeURIComponent(roomName)}/listen`;

        const es = new EventSource(url);

        const handler = (ev: MessageEvent) => {
            const raw = JSON.parse(ev.data);

            const dto = {
                id: raw.id ?? raw.Id,
                room: raw.room ?? raw.Room,
                senderUsername: raw.senderUsername ?? raw.SenderUsername,
                type: raw.type ?? raw.Type,
                content: raw.content ?? raw.Content,
                recipientUserId: raw.recipientUserId ?? raw.RecipientUserId,
                sentAt: raw.sentAt ?? raw.SentAt,
            } as MessageDto;

            setMessages((prev) => {
                if (dto.id && prev.some((m) => m.id === dto.id)) return prev;
                return [...prev, dto];
            });
        };

        // room messages come as named events
        es.addEventListener(eventName, handler as EventListener);

        // DMs might arrive as default event (depends on backplane implementation)
        es.onmessage = handler;

        return () => {
            es.removeEventListener(eventName, handler as EventListener);
            es.close();
        };
    }, [room, baseUrl, token]);


    useEffect(() => {
        let cancelled = false;

        async function loadOnline() {
            try {
                const r = await fetch(`${baseUrl}/rooms/${encodeURIComponent(room.trim())}/online`);
                const data = await r.json();
                if (!cancelled) setOnline(Number(data?.online ?? 0));
            } catch {
                if (!cancelled) setOnline(0);
            }
        }

        loadOnline();
        const id = setInterval(loadOnline, 3000);

        return () => {
            cancelled = true;
            clearInterval(id);
        };
    }, [room, baseUrl]);

    function getUserIdFromJwt(token: string): string | null {
        const payload = parseJwt(token);
        if (!payload) return null;

        return (
            payload["sub"] ??
            payload["nameid"] ??
            payload["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"] ??
            null
        );
    }

    async function send() {
        setError(null);

        if (!token) {
            setError("Login required to send messages.");
            navigate("/login");
            return;
        }

        if (!text.trim()) return;

        try {
            let dto: MessageDto;

            if (isAdmin && dmMode) {
                const recipient = dmRecipientId.trim();
                if (!recipient) {
                    setError("Recipient userId is required for DM.");
                    return;
                }

                const res = await fetch(`${baseUrl}/rooms/${encodeURIComponent(room.trim())}/dm`, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        Authorization: `Bearer ${token}`,
                    },
                    body: JSON.stringify({ recipientUserId: recipient, content: text }),
                });

                if (!res.ok) throw new Error(await res.text());
                dto = (await res.json()) as MessageDto;
            } else {
                dto = await api.messages(room, new CreateMessageRequest({ content: text }));
            }

            setText("");

            // optimistic append, dedupe by id
            setMessages((prev) => {
                if (dto?.id && prev.some((m) => m.id === dto.id)) return prev;
                return [...prev, dto];
            });
        } catch (e: any) {
            setError(e?.message ?? "Failed to send message");
        }
    }


    async function createRoom() {
        setError(null);

        if (!token) {
            setError("Login required.");
            navigate("/login");
            return;
        }
        if (!isAdmin) {
            setError("Only Admin can create rooms.");
            return;
        }

        const name = newRoomName.trim();
        if (!name) return;

        setBusy(true);
        try {
            const res = await fetch(`${baseUrl}/rooms`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    Authorization: `Bearer ${token}`,
                },
                body: JSON.stringify({ name }),
            });

            if (!res.ok) {
                const txt = await res.text();
                throw new Error(txt || "Failed to create room");
            }

            setNewRoomName("");
            await reloadRooms();
            setRoom(name); // jump to new room
        } catch (e: any) {
            setError(e?.message ?? "Failed to create room");
        } finally {
            setBusy(false);
        }
    }

    async function archiveCurrentRoom() {
        setError(null);

        if (!token) {
            setError("Login required.");
            navigate("/login");
            return;
        }
        if (!isAdmin) {
            setError("Only Admin can archive rooms.");
            return;
        }

        const name = room.trim();
        if (!name) return;

        const ok = window.confirm(`Archive room "${name}"? (It will disappear from the list)`);
        if (!ok) return;

        setBusy(true);
        try {
            const res = await fetch(`${baseUrl}/rooms/${encodeURIComponent(name)}/archive`, {
                method: "POST",
                headers: {
                    Authorization: `Bearer ${token}`,
                },
            });

            if (!res.ok) {
                const txt = await res.text();
                throw new Error(txt || "Failed to archive room");
            }

            await reloadRooms();

            // if you archived the current room, move to first available or fallback
            setRoom((prev) => {
                if (prev !== name) return prev;
                const next = rooms.filter((r) => r !== name)[0];
                return next ?? "general";
            });
        } catch (e: any) {
            setError(e?.message ?? "Failed to archive room");
        } finally {
            setBusy(false);
        }
    }

    return (
        <div style={{ minHeight: "100vh", background: "#111", color: "#fff", padding: 24 }}>
            <div style={{ maxWidth: 1000, margin: "0 auto" }}>
                {/* Header */}
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <h1 style={{ margin: 0 }}>Chat</h1>

                    {token ? (
                        <button
                            onClick={() => {
                                logout();
                                navigate("/login", { replace: true });
                            }}
                        >
                            Logout
                        </button>
                    ) : (
                        <button onClick={() => navigate("/login")}>Login</button>
                    )}
                </div>

                {/* Room list + Chat area */}
                <div style={{ marginTop: 16, display: "flex", gap: 12 }}>
                    {/* Rooms panel */}
                    <div
                        style={{
                            width: 260,
                            padding: 12,
                            borderRadius: 12,
                            background: "#1a1a1a",
                            height: 520,
                            overflowY: "auto",
                        }}
                    >
                        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
                            <h3 style={{ marginTop: 0 }}>Rooms</h3>
                            <button onClick={reloadRooms} disabled={busy} style={{ fontSize: 12 }}>
                                Refresh
                            </button>
                        </div>

                        {/* Admin tools */}
                        {token && isAdmin && (
                            <div style={{ marginBottom: 12, padding: 10, border: "1px solid #2a2a2a", borderRadius: 10 }}>
                                <div style={{ fontSize: 12, opacity: 0.8, marginBottom: 6 }}>Admin</div>

                                <div style={{ display: "flex", gap: 6 }}>
                                    <input
                                        value={newRoomName}
                                        onChange={(e) => setNewRoomName(e.target.value)}
                                        placeholder="new room name"
                                        style={{ flex: 1, padding: 8, borderRadius: 8 }}
                                    />
                                    <button onClick={createRoom} disabled={busy || !newRoomName.trim()}>
                                        Create
                                    </button>
                                </div>

                                <button
                                    onClick={archiveCurrentRoom}
                                    disabled={busy || !room.trim()}
                                    style={{ marginTop: 8, width: "100%" }}
                                >
                                    Archive current room
                                </button>
                            </div>
                        )}

                        {rooms.length === 0 && <p style={{ opacity: 0.7, fontSize: 14 }}>No rooms yet.</p>}

                        {rooms.map((r) => (
                            <button
                                key={r}
                                onClick={() => setRoom(r)}
                                style={{
                                    width: "100%",
                                    marginBottom: 8,
                                    padding: 10,
                                    borderRadius: 8,
                                    textAlign: "left",
                                    background: r === room ? "#333" : "#222",
                                    color: "white",
                                    border: "1px solid #2a2a2a",
                                    cursor: "pointer",
                                }}
                            >
                                #{r}
                            </button>
                        ))}
                    </div>

                    {/* Chat panel */}
                    <div style={{ flex: 1 }}>
                        {/* Room input (manual join) */}
                        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                            <label style={{ minWidth: 50 }}>Room:</label>
                            <input
                                value={room}
                                onChange={(e) => setRoom(e.target.value)}
                                style={{ padding: 8, borderRadius: 8, width: 260 }}
                            />
                            <span style={{ opacity: 0.7, fontSize: 14 }}>
                {token ? (isAdmin ? "(admin)" : "(can write)") : "(read-only, login to write)"}
              </span>

                          
                            <span style={{ marginLeft: "auto", opacity: 0.7, fontSize: 14 }}>
    Online: {online}
  </span>
                        </div>

                        {error && <p style={{ color: "crimson" }}>{error}</p>}

                        {/* Messages */}
                        <div
                            style={{
                                marginTop: 12,
                                padding: 12,
                                borderRadius: 12,
                                background: "#1a1a1a",
                                minHeight: 420,
                                maxHeight: 420,
                                overflowY: "auto",
                            }}
                        >
                            {messages.length === 0 ? (
                                <p style={{ opacity: 0.7 }}>No messages yet.</p>
                            ) : (
                                messages.map((m) => (
                                    <div key={m.id} style={{ padding: "8px 0", borderBottom: "1px solid #2a2a2a" }}>
                                        <b>{m.senderUsername}</b>: {m.content}
                                        {m.type === "dm" && (
                                            <span style={{ marginLeft: 8, fontSize: 12, opacity: 0.8 }}>
        (DM)
      </span>
                                        )}
                                        <div style={{ opacity: 0.6, fontSize: 12 }}>
                                            {m.type} {m.sentAt ? new Date(m.sentAt as any).toLocaleString() : ""}
                                        </div>
                                    </div>
                                ))

                            )}
                        </div>

                        /* DM controls (Admin only) */
                        {token && isAdmin && (
                            <div style={{ display: "flex", gap: 8, marginTop: 10, alignItems: "center" }}>
                                <label style={{ fontSize: 12, opacity: 0.8 }}>
                                    <input
                                        type="checkbox"
                                        checked={dmMode}
                                        onChange={(e) => setDmMode(e.target.checked)}
                                        style={{ marginRight: 6 }}
                                    />
                                    DM mode
                                </label>

                                {dmMode && (
                                    <input
                                        value={dmRecipientId}
                                        onChange={(e) => setDmRecipientId(e.target.value)}
                                        placeholder="recipient userId (GUID)"
                                        style={{ flex: 1, padding: 8, borderRadius: 8 }}
                                    />
                                )}
                            </div>
                        )}
                        
                        {/* Send message */}
                        <div style={{ marginTop: 12, display: "flex", gap: 8 }}>
                            <input
                                value={text}
                                onChange={(e) => setText(e.target.value)}
                                placeholder={token ? "Write a message..." : "Login to write"}
                                disabled={!token}
                                style={{ flex: 1, padding: 10, borderRadius: 8 }}
                            />
                            <button onClick={send} disabled={!token || !text.trim()}>
                                Send
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
