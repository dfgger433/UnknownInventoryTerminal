export type BridgeEvent = { type: "event"; name: string; payload?: unknown };
type Listener = (payload: unknown) => void;
type ChromeWebView = { postMessage: (message: unknown) => void; addEventListener?: (type: "message", listener: (event: MessageEvent<unknown>) => void) => void; removeEventListener?: (type: "message", listener: (event: MessageEvent<unknown>) => void) => void };
declare global { interface Window { chrome?: { webview?: ChromeWebView }; casualtiesWebUI: { on: (name: string, callback: Listener) => () => void; emit: (name: string, payload?: unknown) => void; close: () => void } } }
const listeners = new Map<string, Set<Listener>>();
let connected = false;
let readyAnnounced = false;
const post = (message: unknown) => { const webview = window.chrome?.webview; if (webview) { connected = true; webview.postMessage(message); } };
const dispatch = (message: BridgeEvent) => { if (message.type === "event") listeners.get(message.name)?.forEach((callback) => callback(message.payload)); };
const receive = (event: MessageEvent<unknown>) => { const data = event.data; if (!data || typeof data !== "object" || !("type" in data)) return; if ((data as { type?: string }).type === "ready") { connected = true; window.dispatchEvent(new Event("casualties:bridge-ready")); if (!readyAnnounced) { readyAnnounced = true; post({ type: "event", name: "webui.ready", payload: { source: "oreui-panel" } }); } return; } if ((data as BridgeEvent).type === "event") { connected = true; dispatch(data as BridgeEvent); } };
const webview = window.chrome?.webview;
webview?.addEventListener?.("message", receive);
window.addEventListener("message", (event: MessageEvent<unknown>) => { if (event.source === window && (event.origin === window.location.origin || event.origin === "")) receive(event); });
window.addEventListener("webui:ready", () => { connected = true; window.dispatchEvent(new Event("casualties:bridge-ready")); if (!readyAnnounced) { readyAnnounced = true; post({ type: "event", name: "webui.ready", payload: { source: "oreui-panel" } }); } });
window.casualtiesWebUI = { on(name, callback) { const bucket = listeners.get(name) ?? new Set<Listener>(); bucket.add(callback); listeners.set(name, bucket); return () => bucket.delete(callback); }, emit(name, payload) { post({ type: "event", name, payload }); }, close() { post({ type: "close" }); } };
export const bridgeStatus = () => connected;
