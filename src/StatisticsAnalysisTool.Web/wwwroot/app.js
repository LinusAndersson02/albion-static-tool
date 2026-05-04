const state = {
  settings: null,
  currentView: "dashboard",
  selectedLootKey: null,
};

const fmt = new Intl.NumberFormat();
const $ = (id) => document.getElementById(id);

document.querySelectorAll(".nav").forEach((button) => {
  button.addEventListener("click", () => {
    state.currentView = button.dataset.view;
    document.querySelectorAll(".nav").forEach((x) => x.classList.toggle("active", x === button));
    document.querySelectorAll(".view").forEach((x) => x.classList.toggle("active", x.id === state.currentView));
    $("pageTitle").textContent = button.dataset.title || button.textContent.trim();
  });
});

$("startCapture").addEventListener("click", () => post("/api/capture/start"));
$("stopCapture").addEventListener("click", () => post("/api/capture/stop"));
$("resetDps").addEventListener("click", () => post("/api/dps/reset"));
$("resetLoot").addEventListener("click", () => post("/api/loot/reset"));
$("resetTimeline").addEventListener("click", () => post("/api/timeline/reset"));
$("addSilverCheckpoint").addEventListener("click", addSilverCheckpoint);
$("saveSettings").addEventListener("click", saveSettings);
$("detectAlbionFolder").addEventListener("click", detectAlbionFolder);

async function get(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(await response.text());
  return response.json();
}

async function post(url, body) {
  const response = await fetch(url, {
    method: body ? "PUT" : "POST",
    headers: body ? { "content-type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) throw new Error(await response.text());
  return response.json();
}

async function postJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!response.ok) throw new Error(await response.text());
  return response.json();
}

async function saveSettings() {
  const settings = {
    ...state.settings,
    serverLocation: $("serverLocation").value,
    packetFilter: $("packetFilter").value,
    selectedDeviceIdentifiers: $("selectedDevices").value.split(",").map((x) => x.trim()).filter(Boolean),
    mainGameFolderPath: $("mainGameFolderPath").value,
    gameDataDirectory: $("gameDataDirectory").value,
    gameDataLanguage: $("gameDataLanguage").value,
    localPlayerName: $("localPlayerName").value,
  };
  state.settings = await post("/api/settings", settings);
}

async function detectAlbionFolder() {
  const button = $("detectAlbionFolder");
  const previousText = button.textContent;
  button.disabled = true;
  button.textContent = "Detecting...";
  try {
    const result = await post("/api/game-data/detect");
    state.settings = result.settings;
    fillSettings(state.settings);
    $("statusLine").textContent = `Detected Albion folder: ${result.path}`;
  } catch (error) {
    $("statusLine").textContent = "Albion folder was not found in common install locations.";
  } finally {
    button.disabled = false;
    button.textContent = previousText;
  }
}

async function refreshAll() {
  try {
    const [status, capture, party, timeline, dps, loot, rates, parser, gameData] = await Promise.all([
      get("/api/status"),
      get("/api/capture"),
      get("/api/party"),
      get("/api/timeline"),
      get("/api/dps"),
      get("/api/loot"),
      get("/api/rates"),
      get("/api/parser"),
      get("/api/game-data"),
    ]);

    renderStatus(status, capture, parser, gameData);
    renderDevices(status.deviceResult?.devices ?? []);
    renderParty(party);
    renderTimeline(timeline);
    renderDps(dps);
    renderLoot(loot);
    renderRates(rates);

    if (!state.settings) {
      state.settings = await get("/api/settings");
      fillSettings(state.settings);
    }
  } catch (error) {
    $("statusLine").textContent = error.message;
  }
}

async function addSilverCheckpoint() {
  const silver = Number($("silverCheckpoint").value.replace(/[^\d]/g, ""));
  if (!Number.isFinite(silver) || silver < 0) {
    $("statusLine").textContent = "Enter a valid silver amount.";
    return;
  }

  await postJson("/api/timeline/silver", {
    silver,
    note: $("silverNote").value,
  });
  $("silverCheckpoint").value = "";
  $("silverNote").value = "";
}

function renderParty(snapshot) {
  const members = snapshot.members ?? [];
  $("partySize").textContent = fmt.format(snapshot.partySize ?? members.length);
  $("partyAverageIp").textContent = snapshot.averageItemPower == null ? "-" : number(snapshot.averageItemPower, 0);
  $("partyKnownGear").textContent = `${fmt.format(snapshot.knownEquipmentCount ?? 0)} / ${fmt.format(members.length)}`;

  $("partyGrid").innerHTML = members.length
    ? members.map((member) => {
      return `<article class="party-card">
        <div class="party-card-head">
          <div>
            <h2>${escapeHtml(member.name || "(unknown)")}</h2>
            <div class="loot-sub">${escapeHtml(member.guildName || "No guild seen")}</div>
          </div>
          <div class="ip-badge">${member.averageItemPower == null ? "IP ?" : number(member.averageItemPower, 0)}</div>
        </div>
        ${renderEquipmentGrid(member.equipment ?? [])}
      </article>`;
    }).join("")
    : `<div class="panel"><p class="hint">No party roster observed yet. Join a party or relog with capture running.</p></div>`;
}

function renderEquipmentGrid(equipment) {
  if (!equipment.length) {
    return `<div class="empty-slot">Equipment not seen yet</div>`;
  }

  const slots = new Map(equipment.map((slot) => [slot.slot, slot]));
  const gridOrder = ["Bag", "Head", "Cape", "Main hand", "Chest", "Off hand", "Potion", "Shoes", "Food"];
  return `<div class="equipment-panel">
    <div class="equipment-grid">
      ${gridOrder.map((slotName) => renderEquipmentSlot(slots.get(slotName), slotName)).join("")}
    </div>
    <div class="mount-row">${renderEquipmentSlot(slots.get("Mount"), "Mount")}</div>
  </div>`;
}

function renderEquipmentSlot(slot, slotName) {
  const image = itemImageUrl(slot?.itemUniqueName, slot?.renderUrl);
  const itemName = slot?.itemName || "Empty";
  return `<div class="gear-slot ${slot ? "" : "missing"}" title="${escapeHtml(`${slotName}: ${itemName}`)}">
    <div class="gear-icon">${image ? `<img src="${image}" alt="">` : `<span>${escapeHtml(slotName[0])}</span>`}</div>
    <span>${escapeHtml(slotName)}</span>
  </div>`;
}

function renderStatus(status, capture, parser, gameData) {
  $("statusLine").textContent = `${status.operatingSystem} | ${status.capturePermissionStatus} | ${gameData.status}`;
  $("captureState").textContent = capture.isRunning ? "running" : "stopped";
  $("captureState").classList.toggle("good", Boolean(capture.isRunning));
  $("packetCount").textContent = fmt.format(capture.capturedPacketCount ?? 0);
  $("photonCount").textContent = fmt.format(capture.photonPayloadCount ?? 0);
  $("itemCount").textContent = fmt.format(gameData.itemCount ?? 0);
}

function renderDevices(devices) {
  const selectedCount = devices.filter((device) => device.isSelected).length;
  $("deviceStatus").textContent = devices.length ? `${fmt.format(selectedCount)} / ${fmt.format(devices.length)} selected` : "none";
  $("devices").innerHTML = devices.length
    ? `<div class="device-list">${devices.map((d) => `<label class="device-row"><input type="checkbox" ${d.isSelected ? "checked" : ""} data-id="${escapeHtml(d.identifier)}"> <span>${escapeHtml(d.name)}</span><small>${escapeHtml(d.identifier)}</small></label>`).join("")}</div>`
    : `<p class="hint">No devices found or capture permissions are missing.</p>`;

  $("devices").querySelectorAll("input").forEach((box) => {
    box.addEventListener("change", async () => {
      const selected = [...$("devices").querySelectorAll("input:checked")].map((x) => x.dataset.id);
      $("selectedDevices").value = selected.join(", ");
      if (state.settings) {
        state.settings = await post("/api/settings", { ...state.settings, selectedDeviceIdentifiers: selected });
      }
    });
  });
}

function renderDps(snapshot) {
  const rows = snapshot.entries ?? [];
  const maxDamage = Math.max(1, ...rows.map((x) => x.damage ?? 0));
  $("dpsRows").innerHTML = rows.length
    ? rows.map((x) => {
      const width = Math.max(3, Math.round(((x.damage ?? 0) / maxDamage) * 100));
      return `<tr>
        <td>
          <div class="member-name">${escapeHtml(x.name || formatEntity(x.entityId))}</div>
          <div class="bar"><span style="width:${width}%"></span></div>
        </td>
        <td>${fmt.format(x.damage)}</td>
        <td>${number(x.damageShare, 1)}%</td>
        <td>${number(x.dps, 1)}</td>
        <td>${fmt.format(x.takenDamage)}</td>
        <td>${fmt.format(x.hitCount)}</td>
      </tr>`;
    }).join("")
    : `<tr><td colspan="6">No party combat observed yet.</td></tr>`;

  const healingRows = rows.filter((x) => (x.heal ?? 0) > 0).sort((a, b) => (b.heal ?? 0) - (a.heal ?? 0));
  const totalHeal = Math.max(1, snapshot.totalHeal ?? 0);
  $("healingRows").innerHTML = healingRows.length
    ? healingRows.map((x) => `<tr>
      <td>${escapeHtml(x.name || formatEntity(x.entityId))}</td>
      <td>${fmt.format(x.heal)}</td>
      <td>${number(x.hps, 1)}</td>
      <td>${number(((x.heal ?? 0) / totalHeal) * 100, 1)}%${(x.overheal ?? 0) > 0 ? ` (${fmt.format(x.overheal)} overheal)` : ""}</td>
    </tr>`).join("")
    : `<tr><td colspan="4">No party healing observed yet.</td></tr>`;
}

function renderTimeline(snapshot) {
  const events = snapshot.events ?? [];
  const shares = snapshot.shares ?? [];
  $("timelineLatestSilver").textContent = snapshot.latestSilver == null ? "-" : fmt.format(snapshot.latestSilver);
  $("timelineSplitSilver").textContent = fmt.format(snapshot.splitSilver ?? 0);
  $("timelineSplitLootValue").textContent = fmt.format(snapshot.splitLootValue ?? 0);
  $("timelineActivePlayers").textContent = fmt.format((snapshot.activePlayers ?? []).length);

  $("lootSplitShares").innerHTML = shares.length
    ? shares.map((share) => `<div class="split-row">
      <span>${escapeHtml(share.playerName)}</span>
      <strong>${fmt.format(share.silver)}</strong>
    </div>`).join("")
    : `<p class="hint">Add a silver amount to calculate a split.</p>`;

  const valueShares = snapshot.valueShares ?? [];
  $("lootValueSplitShares").innerHTML = valueShares.length
    ? valueShares.map((share) => `<div class="split-row">
      <span>${escapeHtml(share.playerName)}</span>
      <strong>${fmt.format(share.value)}</strong>
    </div>`).join("")
    : `<p class="hint">No valued loot observed yet. Item values appear after Albion sends item discovery value packets.</p>`;

  $("timelineEvents").innerHTML = events.length
    ? events.map((event) => `<div class="timeline-event ${escapeHtml(event.type)}">
      <time>${new Date(event.time).toLocaleTimeString()}</time>
      <span>${escapeHtml(event.text)}</span>
    </div>`).join("")
    : `<p class="hint">No session events yet.</p>`;
}

function renderLoot(snapshot) {
  const entries = snapshot.recentEntries ?? [];
  const grouped = groupLoot(entries);
  renderLootSummary(snapshot, grouped);

  $("lootGrid").innerHTML = entries.length
    ? entries.map((x, index) => {
      const image = itemImageUrl(x.itemUniqueName);
      const key = `${x.itemIndex}-${x.time}-${index}`;
      return `<article class="loot-card ${state.selectedLootKey === key ? "selected" : ""}" data-loot-key="${escapeHtml(key)}" data-loot-index="${index}">
        <div class="icon-frame">${image ? `<img src="${image}" alt="">` : `<span>?</span>`}</div>
        <div>
          <div class="loot-name">${escapeHtml(x.itemName)}</div>
          <div class="loot-sub">${escapeHtml(x.itemUniqueName || `item #${x.itemIndex}`)}</div>
          ${(x.quality ?? 0) > 0 ? `<div class="loot-sub">Quality ${fmt.format(x.quality)}</div>` : ""}
          <div class="loot-sub">${escapeHtml(x.sourceName || "unknown source")}${x.looterName ? ` -> ${escapeHtml(x.looterName)}` : ""}</div>
          <div><span class="qty-pill">${fmt.format(x.quantity)}</span> <span class="loot-sub">${new Date(x.time).toLocaleTimeString()}</span></div>
        </div>
      </article>`;
    }).join("")
    : `<p class="hint">No local item pickups logged yet.</p>`;

  $("lootGrid").querySelectorAll(".loot-card").forEach((card) => {
    card.addEventListener("click", () => {
      state.selectedLootKey = card.dataset.lootKey;
      renderLootDetail(entries[Number(card.dataset.lootIndex)]);
      renderLoot(snapshot);
    });
  });

  if (!entries.length) {
    $("lootDetail").className = "loot-detail empty";
    $("lootDetail").textContent = "Select a loot item to inspect it.";
  }
}

function renderLootSummary(snapshot, grouped) {
  const top = grouped[0];
  $("lootSummary").innerHTML = `
    <div class="mini-metric"><span>Pickups</span><strong>${fmt.format(snapshot.totalEvents ?? 0)}</strong></div>
    <div class="mini-metric"><span>Unique items</span><strong>${fmt.format(grouped.length)}</strong></div>
    <div class="mini-metric"><span>Estimated value</span><strong>${fmt.format(snapshot.totalEstimatedValue ?? 0)}</strong></div>
    <div class="mini-metric"><span>Top item</span><strong>${escapeHtml(top?.itemName ?? "-")}</strong></div>
  `;
}

function renderLootDetail(item) {
  if (!item) {
    $("lootDetail").className = "loot-detail empty";
    $("lootDetail").textContent = "Select a loot item to inspect it.";
    return;
  }

  const image = itemImageUrl(item.itemUniqueName);
  $("lootDetail").className = "loot-detail";
  $("lootDetail").innerHTML = `
    <div class="icon-frame large">${image ? `<img src="${image}" alt="">` : `<span>?</span>`}</div>
    <div>
      <h3>${escapeHtml(item.itemName)}</h3>
      <p>${escapeHtml(item.itemUniqueName || `item #${item.itemIndex}`)}</p>
      <p>Quantity: <strong>${fmt.format(item.quantity)}</strong></p>
      ${(item.quality ?? 0) > 0 ? `<p>Quality: <strong>${fmt.format(item.quality)}</strong></p>` : ""}
      <p>Estimated value: <strong>${fmt.format(item.estimatedTotalValue ?? 0)}</strong></p>
      <p>Source: ${escapeHtml(item.sourceName || "unknown source")}</p>
      <p>Looter: ${escapeHtml(item.looterName || "unknown looter")}</p>
      <p>Time: ${new Date(item.time).toLocaleString()}</p>
    </div>
  `;
}

function groupLoot(entries) {
  const groups = new Map();
  for (const entry of entries) {
    const key = `${entry.itemIndex}:${entry.itemUniqueName || ""}`;
    const existing = groups.get(key) ?? { itemName: entry.itemName, quantity: 0 };
    existing.quantity += entry.quantity ?? 0;
    groups.set(key, existing);
  }
  return [...groups.values()].sort((a, b) => b.quantity - a.quantity);
}

function renderRates(rates) {
  const values = [
    ["Silver", rates.silver, rates.silverPerHour],
    ["Fame", rates.fame, rates.famePerHour],
    ["ReSpec", rates.reSpecPoints, rates.reSpecPointsPerHour],
    ["ReSpec silver", rates.paidSilverForReSpec, rates.paidSilverForReSpecPerHour],
    ["Faction points", rates.factionPoints, rates.factionPointsPerHour],
    ["Faction standing", rates.factionStanding, rates.factionStandingPerHour],
    ["Might", rates.might, rates.mightPerHour],
    ["Favor", rates.favor, rates.favorPerHour],
  ];
  $("rateGrid").innerHTML = values.map(([name, total, perHour]) => `<div class="rate"><span>${name}</span><strong>${fmt.format(Math.round(total ?? 0))}</strong><span>${fmt.format(Math.round(perHour ?? 0))}/h</span></div>`).join("");
}

function fillSettings(settings) {
  $("serverLocation").value = settings.serverLocation ?? "";
  $("packetFilter").value = settings.packetFilter ?? "";
  $("selectedDevices").value = (settings.selectedDeviceIdentifiers ?? []).join(", ");
  $("mainGameFolderPath").value = settings.mainGameFolderPath ?? "";
  $("gameDataDirectory").value = settings.gameDataDirectory ?? "";
  $("gameDataLanguage").value = settings.gameDataLanguage ?? "en-US";
  $("localPlayerName").value = settings.localPlayerName ?? "";
}

function formatEntity(id) {
  return id ? `entity ${id}` : "unknown";
}

function itemImageUrl(uniqueName, renderUrl = "") {
  if (renderUrl) {
    return renderUrl;
  }

  return uniqueName ? `/api/item-images/${encodeURIComponent(uniqueName)}.png` : "";
}

function number(value, digits) {
  return Number(value ?? 0).toFixed(digits);
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, (char) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#039;",
  }[char]));
}

refreshAll();
setInterval(refreshAll, 1000);
