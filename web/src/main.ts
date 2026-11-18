import "./app.css";
import { bridgeStatus } from "./bridge";
import { initDropdowns, initScrollbars, initTabButtons, initTooltips } from "../vendor/OreUI/src/index.ts";

type Item = { instanceKey: string; typeId: string; displayName: string; containerName: string; count: number };
type State = { items: Item[]; total: number; groups: number; query: string; mode: "merged" | "container"; status?: string };

const sampleItems: Item[] = [
  { instanceKey: "demo-bandage-1", typeId: "medical.bandage", displayName: "Bandage", containerName: "Field satchel", count: 8 },
  { instanceKey: "demo-ammo-1", typeId: "ammo.9mm", displayName: "9mm ammunition", containerName: "Field satchel", count: 42 },
  { instanceKey: "demo-water-1", typeId: "food.water", displayName: "Water flask", containerName: "Pack frame", count: 3 },
  { instanceKey: "demo-battery-1", typeId: "power.battery", displayName: "Battery cell", containerName: "Pack frame", count: 6 },
  { instanceKey: "demo-key-1", typeId: "tool.lockpick", displayName: "Lockpick kit", containerName: "Utility pouch", count: 1 },
  { instanceKey: "demo-filter-1", typeId: "filter.charcoal", displayName: "Charcoal filter", containerName: "Utility pouch", count: 4 },
  { instanceKey: "demo-food-1", typeId: "food.ration", displayName: "Field ration", containerName: "Field satchel", count: 12 },
  { instanceKey: "demo-tool-1", typeId: "tool.multitool", displayName: "Multitool", containerName: "Utility pouch", count: 1 },
];

const initialState: State = { items: sampleItems, total: 77, groups: 8, query: "", mode: "merged", status: "Browser preview · awaiting game snapshot" };
let state = initialState;
const app = document.querySelector<HTMLDivElement>("#app")!;

function esc(value: string) { return value.replace(/[&<>\"]/g, (char) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '\"': "&quot;" })[char] ?? char); }
function visibleItems() {
  const query = state.query.trim().toLocaleLowerCase();
  return state.items.filter((item) => !query || `${item.displayName} ${item.typeId} ${item.containerName}`.toLocaleLowerCase().includes(query));
}
function render() {
  const items = visibleItems();
  const grouped = state.mode === "container" ? items.reduce<Record<string, Item[]>>((groups, item) => ((groups[item.containerName] ??= []).push(item), groups), {}) : { "All stored items": items };
  app.innerHTML = `
    <main class="terminal-shell">
      <header class="terminal-header">
        <div class="brand-lockup"><div class="brand-sigil" aria-hidden="true"><span></span><span></span><span></span><span></span></div><div><p class="kicker">UNKNOWN INVENTORY TERMINAL / WEBUI</p><h1>STORAGE ARRAY</h1><p class="subline">Client-side view · local inventory authority</p></div></div>
        <div class="header-actions"><span class="connection ${bridgeStatus() ? "online" : "preview"}"><i></i>${bridgeStatus() ? "HOST CONNECTED" : "BROWSER PREVIEW"}</span><button class="ore-icon-button" id="close" aria-label="Close terminal" data-tooltip="Close terminal">×</button></div>
      </header>
      <section class="terminal-toolbar" aria-label="Inventory controls">
        <label class="search-field"><span aria-hidden="true">⌕</span><input id="search" type="search" value="${esc(state.query)}" placeholder="Search item ID, name or container" autocomplete="off" /></label>
        <div class="toolbar-actions"><button class="ore-button" data-color="secondary" id="refresh" type="button">↻ Refresh</button><button class="ore-button" data-color="secondary" id="clear" type="button">Clear</button></div>
      </section>
      <div class="terminal-layout">
        <aside class="rail" aria-label="Terminal filters">
          <p class="rail-label">VIEW</p><div class="mode-tabs" role="tablist"><button class="ore-tab-button" data-mode="merged" aria-selected="${state.mode === "merged"}">Merged</button><button class="ore-tab-button" data-mode="container" aria-selected="${state.mode === "container"}">By container</button></div>
          <div class="rail-rule"></div><p class="rail-label">SORT</p><button class="rail-option active" type="button"><span>◈</span>Name <b>↕</b></button><button class="rail-option" type="button"><span>▦</span>Quantity <b>↕</b></button><button class="rail-option" type="button"><span>⌁</span>Source <b>↕</b></button>
          <div class="rail-rule"></div><p class="rail-label">VIEW CELLS</p><div class="filter-slots"><button class="filter-slot" type="button" aria-label="Empty view cell">+</button><button class="filter-slot" type="button" aria-label="Empty view cell">+</button><button class="filter-slot" type="button" aria-label="Empty view cell">+</button></div><p class="rail-help">Drop a real item onto a slot to add a filter.</p>
        </aside>
        <section class="inventory-surface"><div class="surface-meta"><div><span class="meta-label">NETWORK INDEX</span><strong>${items.length} matching groups</strong></div><div class="status-copy">${esc(state.status ?? "Ready")} <span class="pulse"></span></div></div><div class="item-groups">${Object.entries(grouped).map(([group, groupItems]) => `<section class="item-group"><div class="group-heading"><h2>${esc(group)}</h2><span>${groupItems.length} groups</span></div><div class="item-grid">${groupItems.map((item) => `<button class="item-tile" type="button" data-instance="${esc(item.instanceKey)}" title="${esc(item.displayName)} · ${esc(item.containerName)}"><span class="item-icon" aria-hidden="true">${iconFor(item.typeId)}</span><span class="item-name">${esc(item.displayName)}</span><span class="item-source">${esc(item.containerName)}</span><strong class="item-count">${item.count}</strong></button>`).join("") || `<div class="empty-state"><span>∅</span><strong>No matching items</strong><p>Clear the search or refresh the local index.</p></div>`}</div></section>`).join("")}</div></section>
      </div>
      <footer class="terminal-footer"><div><span class="footer-label">LOCAL INVENTORY</span><strong>${state.total} instances indexed</strong></div><div><span class="footer-label">INTERACTION</span><strong>Original item controls remain authoritative</strong></div><div class="footer-actions"><button class="ore-button" data-color="primary" id="open-native" type="button">Open native controls <span>→</span></button></div></footer>
      <div class="toast-region" id="toast" role="status" aria-live="polite"></div>
    </main>`;
  bind();
  initTabButtons(); initDropdowns(); initScrollbars(); initTooltips();
}
function iconFor(typeId: string) { return typeId.startsWith("medical") ? "+" : typeId.startsWith("ammo") ? "••" : typeId.startsWith("food") ? "◒" : typeId.startsWith("power") ? "ϟ" : typeId.startsWith("tool") ? "⌁" : "◇"; }
function notify(message: string) { const toast = document.querySelector<HTMLDivElement>("#toast"); if (!toast) return; toast.textContent = message; toast.classList.add("visible"); window.setTimeout(() => toast.classList.remove("visible"), 2200); }
function emit(name: string, payload?: unknown) { window.casualtiesWebUI.emit(name, payload); }
function bind() {
  document.querySelector<HTMLInputElement>("#search")?.addEventListener("input", (event) => { state = { ...state, query: (event.target as HTMLInputElement).value }; emit("uit.query", { query: state.query }); render(); const input = document.querySelector<HTMLInputElement>("#search"); input?.focus(); input?.setSelectionRange(input.value.length, input.value.length); });
  document.querySelector("#clear")?.addEventListener("click", () => { state = { ...state, query: "" }; emit("uit.query", { query: "" }); render(); notify("Search cleared"); });
  document.querySelector("#refresh")?.addEventListener("click", () => { emit("uit.refresh"); notify("Refresh requested from host"); });
  document.querySelector("#close")?.addEventListener("click", () => { emit("uit.close"); window.casualtiesWebUI.close(); });
  document.querySelector("#open-native")?.addEventListener("click", () => { emit("uit.native", { action: "open" }); notify("Native controls remain authoritative"); });
  document.querySelectorAll<HTMLButtonElement>("[data-mode]").forEach((button) => button.addEventListener("click", () => { state = { ...state, mode: button.dataset.mode as State["mode"] }; emit("uit.mode", { mode: state.mode }); render(); }));
  document.querySelectorAll<HTMLButtonElement>("[data-instance]").forEach((button) => button.addEventListener("click", () => { emit("uit.interaction", { action: "select", instanceKey: button.dataset.instance }); notify("Selection sent to native inventory layer"); }));
}
function acceptSnapshot(payload: unknown) {
  if (!payload || typeof payload !== "object") return;
  const value = payload as Partial<State> & { entries?: Item[] };
  const items = Array.isArray(value.items) ? value.items : Array.isArray(value.entries) ? value.entries : null;
  if (!items) return;
  state = { ...state, items, total: typeof value.total === "number" ? value.total : items.reduce((sum, item) => sum + item.count, 0), groups: typeof value.groups === "number" ? value.groups : items.length, status: typeof value.status === "string" ? value.status : "Host snapshot received" };
  render();
}
window.casualtiesWebUI.on("uit.snapshot", acceptSnapshot); window.casualtiesWebUI.on("inventory.snapshot", acceptSnapshot);
window.casualtiesWebUI.on("uit.status", (payload) => { if (payload && typeof payload === "object" && "text" in payload) { state = { ...state, status: String((payload as { text: unknown }).text) }; render(); } });
window.casualtiesWebUI.on("inventory.status", (payload) => { if (payload && typeof payload === "object" && "text" in payload) { state = { ...state, status: String((payload as { text: unknown }).text) }; render(); } });
window.casualtiesWebUI.on("uit.query", (payload) => { if (payload && typeof payload === "object" && "query" in payload) { state = { ...state, query: String((payload as { query: unknown }).query) }; render(); } });
window.casualtiesWebUI.on("inventory.query", (payload) => { if (payload && typeof payload === "object" && "query" in payload) { state = { ...state, query: String((payload as { query: unknown }).query) }; render(); } });
window.casualtiesWebUI.on("uit.mode", (payload) => { if (payload && typeof payload === "object" && "mode" in payload) { const mode = String((payload as { mode: unknown }).mode).toLocaleLowerCase(); state = { ...state, mode: mode === "container" || mode === "bycontainer" ? "container" : "merged" }; render(); } });
window.casualtiesWebUI.on("inventory.mode", (payload) => { if (payload && typeof payload === "object" && "mode" in payload) { const mode = String((payload as { mode: unknown }).mode).toLocaleLowerCase(); state = { ...state, mode: mode === "container" || mode === "bycontainer" ? "container" : "merged" }; render(); } });
render();
