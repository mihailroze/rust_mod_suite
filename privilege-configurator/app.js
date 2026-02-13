(function () {
  "use strict";

  const DEFAULT_RU_ITEM_NAMES = (window.DEFAULT_RU_ITEM_NAMES && typeof window.DEFAULT_RU_ITEM_NAMES === "object")
    ? window.DEFAULT_RU_ITEM_NAMES
    : {};
  const EMBEDDED_CATALOG = normalizeCatalog(window.DEFAULT_LOOT_CATALOG || null);

  const state = {
    config: createDefaultConfig(),
    catalog: EMBEDDED_CATALOG,
    itemByShortname: Object.create(null),
    selectedRankKey: null
  };
  const SERVER_SAVE_ENDPOINT = "/api/save-config";
  const SERVER_DEPLOY_ENDPOINT = "/api/deploy-plugin";

  const els = {
    configFileInput: document.getElementById("configFileInput"),
    catalogFileInput: document.getElementById("catalogFileInput"),
    resetBtn: document.getElementById("resetBtn"),
    copyBtn: document.getElementById("copyBtn"),
    downloadBtn: document.getElementById("downloadBtn"),
    saveServerBtn: document.getElementById("saveServerBtn"),
    saveServerStatus: document.getElementById("saveServerStatus"),
    deployPluginBtn: document.getElementById("deployPluginBtn"),
    deployPluginStatus: document.getElementById("deployPluginStatus"),
    notifyOnConnect: document.getElementById("notifyOnConnect"),
    expiryInterval: document.getElementById("expiryInterval"),
    teleportPermissionsInput: document.getElementById("teleportPermissionsInput"),
    homeBasePermissionsInput: document.getElementById("homeBasePermissionsInput"),
    homeLimitTemplateInput: document.getElementById("homeLimitTemplateInput"),
    pocketRecyclerPermissionsInput: document.getElementById("pocketRecyclerPermissionsInput"),
    removePermissionsInput: document.getElementById("removePermissionsInput"),
    dailyEnabled: document.getElementById("dailyEnabled"),
    dailyCooldownSeconds: document.getElementById("dailyCooldownSeconds"),
    dailyAllowWithoutRank: document.getElementById("dailyAllowWithoutRank"),
    dailyItemsInput: document.getElementById("dailyItemsInput"),
    homeCommandTemplateInput: document.getElementById("homeCommandTemplateInput"),
    townCommandInput: document.getElementById("townCommandInput"),
    teamCommandTemplateInput: document.getElementById("teamCommandTemplateInput"),
    homeBaseCooldownInput: document.getElementById("homeBaseCooldownInput"),
    homeActivationDelayInput: document.getElementById("homeActivationDelayInput"),
    homePointsWithoutPrivilegeInput: document.getElementById("homePointsWithoutPrivilegeInput"),
    removeModeDurationInput: document.getElementById("removeModeDurationInput"),
    pocketRecyclerSpeedMultiplierInput: document.getElementById("pocketRecyclerSpeedMultiplierInput"),
    pocketRecyclerCommandCooldownInput: document.getElementById("pocketRecyclerCommandCooldownInput"),
    pocketRecyclerAutoCloseInput: document.getElementById("pocketRecyclerAutoCloseInput"),
    pocketRecyclerLocalSoundsInput: document.getElementById("pocketRecyclerLocalSoundsInput"),
    pocketRecyclerOpenSoundInput: document.getElementById("pocketRecyclerOpenSoundInput"),
    pocketRecyclerCloseSoundInput: document.getElementById("pocketRecyclerCloseSoundInput"),
    pocketRecyclerWorkingSoundInput: document.getElementById("pocketRecyclerWorkingSoundInput"),
    pocketRecyclerWorkingSoundIntervalInput: document.getElementById("pocketRecyclerWorkingSoundIntervalInput"),
    teamBaseCooldownInput: document.getElementById("teamBaseCooldownInput"),
    townBaseLimitInput: document.getElementById("townBaseLimitInput"),
    tpDebugRepliesInput: document.getElementById("tpDebugRepliesInput"),
    shopEnabledInput: document.getElementById("shopEnabledInput"),
    shopCurrencyInput: document.getElementById("shopCurrencyInput"),
    shopPackagesInput: document.getElementById("shopPackagesInput"),
    auditEnabledInput: document.getElementById("auditEnabledInput"),
    auditMaxEntriesInput: document.getElementById("auditMaxEntriesInput"),
    auditEchoToConsoleInput: document.getElementById("auditEchoToConsoleInput"),
    webShopEnabledInput: document.getElementById("webShopEnabledInput"),
    webShopApiBaseUrlInput: document.getElementById("webShopApiBaseUrlInput"),
    webShopServerIdInput: document.getElementById("webShopServerIdInput"),
    webShopServerKeyInput: document.getElementById("webShopServerKeyInput"),
    webShopPollIntervalInput: document.getElementById("webShopPollIntervalInput"),
    webShopBatchSizeInput: document.getElementById("webShopBatchSizeInput"),
    webShopRequestTimeoutInput: document.getElementById("webShopRequestTimeoutInput"),
    webShopGrantSourceInput: document.getElementById("webShopGrantSourceInput"),
    catalogInfo: document.getElementById("catalogInfo"),
    rankSearch: document.getElementById("rankSearch"),
    newRankKey: document.getElementById("newRankKey"),
    addRankBtn: document.getElementById("addRankBtn"),
    ranksList: document.getElementById("ranksList"),
    noRankState: document.getElementById("noRankState"),
    rankEditor: document.getElementById("rankEditor"),
    rankKeyInput: document.getElementById("rankKeyInput"),
    renameRankBtn: document.getElementById("renameRankBtn"),
    deleteRankBtn: document.getElementById("deleteRankBtn"),
    displayName: document.getElementById("displayName"),
    chatTag: document.getElementById("chatTag"),
    chatColor: document.getElementById("chatColor"),
    oxideGroup: document.getElementById("oxideGroup"),
    gatherMultiplier: document.getElementById("gatherMultiplier"),
    groundMultiplier: document.getElementById("groundMultiplier"),
    containerMultiplier: document.getElementById("containerMultiplier"),
    npcScrapReward: document.getElementById("npcScrapReward"),
    kitCooldownSeconds: document.getElementById("kitCooldownSeconds"),
    kitAmountMultiplier: document.getElementById("kitAmountMultiplier"),
    allowTeleport: document.getElementById("allowTeleport"),
    homePoints: document.getElementById("homePoints"),
    allowPocketRecycler: document.getElementById("allowPocketRecycler"),
    pocketRecyclerRankSpeedMultiplier: document.getElementById("pocketRecyclerRankSpeedMultiplier"),
    pocketRecyclerRankOutputMultiplier: document.getElementById("pocketRecyclerRankOutputMultiplier"),
    allowRemoveCommand: document.getElementById("allowRemoveCommand"),
    dailyRewardMultiplier: document.getElementById("dailyRewardMultiplier"),
    homeTpCooldownReduction: document.getElementById("homeTpCooldownReduction"),
    teamTpCooldownReduction: document.getElementById("teamTpCooldownReduction"),
    townTpDailyLimitBonus: document.getElementById("townTpDailyLimitBonus"),
    permissionsBody: document.getElementById("permissionsBody"),
    newPermission: document.getElementById("newPermission"),
    addPermissionBtn: document.getElementById("addPermissionBtn"),
    kitItemsBody: document.getElementById("kitItemsBody"),
    addKitShortname: document.getElementById("addKitShortname"),
    addKitAmount: document.getElementById("addKitAmount"),
    addKitItemBtn: document.getElementById("addKitItemBtn"),
    kitItemHint: document.getElementById("kitItemHint"),
    outputJson: document.getElementById("outputJson"),
    itemShortnamesList: document.getElementById("itemShortnamesList")
  };

  rebuildItemLookup();
  bindEvents();
  renderAll();

  function createDefaultFeaturePermissions() {
    return {
      "Teleport permissions": ["nteleportation.tp", "nteleportation.tpr"],
      "Home base permissions": ["nteleportation.home"],
      "Home limit permission template (use {0} for points, empty = disabled)": "nteleportation.home.{0}",
      "Pocket recycler permissions": ["recycler.use"],
      "Remove command permissions": ["removertool.remove"]
    };
  }

  function createDefaultDailyRewards() {
    return {
      "Enabled": true,
      "Cooldown seconds": 86400,
      "Allow without active rank": false,
      "Base items": [
        { "Shortname": "scrap", "Amount": 150 },
        { "Shortname": "cloth", "Amount": 100 },
        { "Shortname": "lowgradefuel", "Amount": 40 }
      ]
    };
  }

  function createDefaultTeleportFeatures() {
    return {
      "Home command template (use {0} for home name)": "home {0}",
      "Town command": "town",
      "Team command template (use {0} for target name/id)": "tpr {0}",
      "Home base cooldown seconds": 30,
      "Home activation base delay seconds": 15,
      "Home points without privilege": 1,
      "Remove mode duration seconds": 30,
      "Pocket recycler speed multiplier": 1,
      "Pocket recycler command cooldown seconds": 10,
      "Pocket recycler auto close on menu close": true,
      "Pocket recycler local sounds enabled": true,
      "Pocket recycler open sound effect prefab": "assets/bundled/prefabs/fx/notice/item.select.fx.prefab",
      "Pocket recycler close sound effect prefab": "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab",
      "Pocket recycler working sound effect prefab": "assets/bundled/prefabs/fx/notice/item.select.fx.prefab",
      "Pocket recycler working sound interval seconds": 1.2,
      "Team base cooldown seconds": 15,
      "Town base daily limit": 10,
      "Show debug replies": false
    };
  }

  function createDefaultShop() {
    return {
      "Enabled": true,
      "Currency (economics/serverrewards)": "economics",
      "Packages": {
        "vip_30d": {
          "Display name": "VIP 30d",
          "Rank": "vip",
          "Days (0 = permanent)": 30,
          "Economics price": 500,
          "ServerRewards price": 500
        },
        "premium_30d": {
          "Display name": "Premium 30d",
          "Rank": "premium",
          "Days (0 = permanent)": 30,
          "Economics price": 1200,
          "ServerRewards price": 1200
        },
        "elite_30d": {
          "Display name": "Elite 30d",
          "Rank": "elite",
          "Days (0 = permanent)": 30,
          "Economics price": 2500,
          "ServerRewards price": 2500
        }
      }
    };
  }

  function createDefaultAudit() {
    return {
      "Enabled": true,
      "Max entries": 500,
      "Echo to console": true
    };
  }

  function createDefaultWebShopBridge() {
    return {
      "Enabled": false,
      "Api base url": "http://127.0.0.1:8001/api/v1",
      "Server id": "local-rust-1",
      "Server key": "change-me-server-key",
      "Poll interval seconds": 10,
      "Batch size": 10,
      "Request timeout seconds": 10,
      "Grant source label": "WebShop"
    };
  }

  function createDefaultConfig() {
    return {
      "Notify player on connect": true,
      "Expiry check interval (seconds)": 60,
      "Feature permissions": createDefaultFeaturePermissions(),
      "Daily rewards": createDefaultDailyRewards(),
      "Teleport features": createDefaultTeleportFeatures(),
      "Shop": createDefaultShop(),
      "Audit": createDefaultAudit(),
      "Web shop bridge": createDefaultWebShopBridge(),
      "Ranks": createDefaultRanks()
    };
  }

  function createDefaultRanks() {
    return {
      "vip": normalizeRank({
        "Display name": "VIP",
        "Chat tag": "[VIP]",
        "Chat color": "#f4c542",
        "Oxide group (optional)": "vip",
        "Permissions": ["privilegesystem.rank.vip"],
        "Allow teleport": true,
        "Home points": 1,
        "Pocket recycler speed multiplier": 1.25,
        "Pocket recycler output multiplier": 1.3,
        "Daily reward multiplier": 1.2,
        "Home teleport cooldown reduction (seconds)": 5,
        "Team teleport cooldown reduction (seconds)": 3,
        "Town teleport daily limit bonus": 2,
        "Gather multiplier": 1.3,
        "Ground pickup multiplier": 1.2,
        "Container loot multiplier": 1.15,
        "NPC kill scrap reward": 3,
        "Rank kit cooldown seconds": 86400,
        "Rank kit amount multiplier": 1,
        "Rank kit items": [
          { "Shortname": "scrap", "Amount": 300 },
          { "Shortname": "metal.fragments", "Amount": 1000 },
          { "Shortname": "lowgradefuel", "Amount": 120 }
        ]
      }, "vip"),
      "premium": normalizeRank({
        "Display name": "Premium",
        "Chat tag": "[PREMIUM]",
        "Chat color": "#6ed3ff",
        "Oxide group (optional)": "premium",
        "Permissions": ["privilegesystem.rank.vip", "privilegesystem.rank.premium"],
        "Allow teleport": true,
        "Home points": 2,
        "Allow pocket recycler": true,
        "Pocket recycler speed multiplier": 1.75,
        "Pocket recycler output multiplier": 1.6,
        "Daily reward multiplier": 1.5,
        "Home teleport cooldown reduction (seconds)": 10,
        "Team teleport cooldown reduction (seconds)": 6,
        "Town teleport daily limit bonus": 5,
        "Gather multiplier": 1.6,
        "Ground pickup multiplier": 1.4,
        "Container loot multiplier": 1.3,
        "NPC kill scrap reward": 6,
        "Rank kit cooldown seconds": 72000,
        "Rank kit amount multiplier": 1.2,
        "Rank kit items": [
          { "Shortname": "scrap", "Amount": 600 },
          { "Shortname": "metal.fragments", "Amount": 2500 },
          { "Shortname": "lowgradefuel", "Amount": 250 },
          { "Shortname": "metal.refined", "Amount": 20 }
        ]
      }, "premium"),
      "elite": normalizeRank({
        "Display name": "Elite",
        "Chat tag": "[ELITE]",
        "Chat color": "#ff9f43",
        "Oxide group (optional)": "elite",
        "Permissions": ["privilegesystem.rank.vip", "privilegesystem.rank.premium", "privilegesystem.rank.elite"],
        "Allow teleport": true,
        "Home points": 3,
        "Allow pocket recycler": true,
        "Pocket recycler speed multiplier": 2.5,
        "Pocket recycler output multiplier": 2,
        "Allow remove command": true,
        "Daily reward multiplier": 2,
        "Home teleport cooldown reduction (seconds)": 15,
        "Team teleport cooldown reduction (seconds)": 9,
        "Town teleport daily limit bonus": 10,
        "Gather multiplier": 2,
        "Ground pickup multiplier": 1.8,
        "Container loot multiplier": 1.5,
        "NPC kill scrap reward": 10,
        "Rank kit cooldown seconds": 57600,
        "Rank kit amount multiplier": 1.4,
        "Rank kit items": [
          { "Shortname": "scrap", "Amount": 1000 },
          { "Shortname": "metal.fragments", "Amount": 4000 },
          { "Shortname": "lowgradefuel", "Amount": 400 },
          { "Shortname": "metal.refined", "Amount": 40 },
          { "Shortname": "cloth", "Amount": 500 }
        ]
      }, "elite")
    };
  }

  function createEmptyRank(rankKey) {
    const key = normalizeRankKey(rankKey) || "rank";
    const upper = key.toUpperCase();
    return normalizeRank({
      "Display name": upper,
      "Chat tag": `[${upper}]`,
      "Chat color": "#ffffff",
      "Oxide group (optional)": key,
      "Permissions": [],
      "Allow teleport": false,
      "Home points": 0,
      "Allow pocket recycler": false,
      "Pocket recycler speed multiplier": 1,
      "Pocket recycler output multiplier": 1,
      "Allow remove command": false,
      "Daily reward multiplier": 1,
      "Home teleport cooldown reduction (seconds)": 0,
      "Team teleport cooldown reduction (seconds)": 0,
      "Town teleport daily limit bonus": 0,
      "Gather multiplier": 1,
      "Ground pickup multiplier": 1,
      "Container loot multiplier": 1,
      "NPC kill scrap reward": 0,
      "Rank kit cooldown seconds": 0,
      "Rank kit amount multiplier": 1,
      "Rank kit items": []
    }, key);
  }

  function normalizeRank(source, rankKey) {
    const key = normalizeRankKey(rankKey) || "rank";
    const displayName = String((source && source["Display name"]) || key.toUpperCase()).trim() || key.toUpperCase();
    const chatTag = String((source && source["Chat tag"]) || `[${displayName}]`).trim();
    const chatColor = String((source && source["Chat color"]) || "#ffffff").trim() || "#ffffff";
    const oxideGroup = String((source && source["Oxide group (optional)"]) || "").trim();
    const permissions = dedupePermissions(Array.isArray(source && source["Permissions"]) ? source["Permissions"] : []);

    const rankKitItems = Array.isArray(source && source["Rank kit items"])
      ? source["Rank kit items"].map((entry) => ({
        "Shortname": normalizeShortname(entry && entry["Shortname"]),
        "Amount": Math.max(1, clampInt(parseInt(entry && entry["Amount"], 10), 1, 100000000, 1))
      }))
      : [];

    return {
      "Display name": displayName,
      "Chat tag": chatTag,
      "Chat color": chatColor,
      "Oxide group (optional)": oxideGroup,
      "Permissions": permissions,
      "Allow teleport": !!(source && source["Allow teleport"]),
      "Home points": Math.max(0, clampInt(parseInt(source && source["Home points"], 10), 0, 100, 0)),
      "Allow pocket recycler": !!(source && source["Allow pocket recycler"]),
      "Pocket recycler speed multiplier": clampFloat(parseFloat(source && source["Pocket recycler speed multiplier"]), 1, 20, 1),
      "Pocket recycler output multiplier": clampFloat(parseFloat(source && source["Pocket recycler output multiplier"]), 1, 20, 1),
      "Allow remove command": !!(source && source["Allow remove command"]),
      "Daily reward multiplier": clampFloat(parseFloat(source && source["Daily reward multiplier"]), 0.1, 20, 1),
      "Home teleport cooldown reduction (seconds)": Math.max(0, clampInt(parseInt(source && source["Home teleport cooldown reduction (seconds)"], 10), 0, 600, 0)),
      "Team teleport cooldown reduction (seconds)": Math.max(0, clampInt(parseInt(source && source["Team teleport cooldown reduction (seconds)"], 10), 0, 600, 0)),
      "Town teleport daily limit bonus": Math.max(0, clampInt(parseInt(source && source["Town teleport daily limit bonus"], 10), 0, 1000, 0)),
      "Gather multiplier": clampFloat(parseFloat(source && source["Gather multiplier"]), 1, 10, 1),
      "Ground pickup multiplier": clampFloat(parseFloat(source && source["Ground pickup multiplier"]), 1, 10, 1),
      "Container loot multiplier": clampFloat(parseFloat(source && source["Container loot multiplier"]), 1, 10, 1),
      "NPC kill scrap reward": Math.max(0, clampInt(parseInt(source && source["NPC kill scrap reward"], 10), 0, 100000000, 0)),
      "Rank kit cooldown seconds": Math.max(0, clampInt(parseInt(source && source["Rank kit cooldown seconds"], 10), 0, 1000000000, 0)),
      "Rank kit amount multiplier": clampFloat(parseFloat(source && source["Rank kit amount multiplier"]), 1, 20, 1),
      "Rank kit items": rankKitItems.filter((entry) => !!entry["Shortname"] && entry["Amount"] > 0)
    };
  }

  function normalizeFeaturePermissions(source) {
    const d = createDefaultFeaturePermissions();
    const raw = source && typeof source === "object" ? source : {};
    return {
      "Teleport permissions": dedupePermissions(Array.isArray(raw["Teleport permissions"]) ? raw["Teleport permissions"] : d["Teleport permissions"]),
      "Home base permissions": dedupePermissions(Array.isArray(raw["Home base permissions"]) ? raw["Home base permissions"] : d["Home base permissions"]),
      "Home limit permission template (use {0} for points, empty = disabled)": String(raw["Home limit permission template (use {0} for points, empty = disabled)"] != null ? raw["Home limit permission template (use {0} for points, empty = disabled)"] : d["Home limit permission template (use {0} for points, empty = disabled)"]).trim(),
      "Pocket recycler permissions": dedupePermissions(Array.isArray(raw["Pocket recycler permissions"]) ? raw["Pocket recycler permissions"] : d["Pocket recycler permissions"]),
      "Remove command permissions": dedupePermissions(Array.isArray(raw["Remove command permissions"]) ? raw["Remove command permissions"] : d["Remove command permissions"])
    };
  }

  function normalizeDailyRewardItems(rawItems, fallbackItems) {
    const source = Array.isArray(rawItems) ? rawItems : (Array.isArray(fallbackItems) ? fallbackItems : []);
    const normalized = source.map((entry) => ({
      "Shortname": normalizeShortname(entry && entry["Shortname"]),
      "Amount": Math.max(1, clampInt(parseInt(entry && entry["Amount"], 10), 1, 100000000, 1))
    })).filter((entry) => !!entry["Shortname"]);
    return normalized.length > 0 ? normalized : createDefaultDailyRewards()["Base items"];
  }

  function normalizeDailyRewards(source) {
    const d = createDefaultDailyRewards();
    const raw = source && typeof source === "object" ? source : {};
    return {
      "Enabled": typeof raw["Enabled"] === "boolean" ? raw["Enabled"] : d["Enabled"],
      "Cooldown seconds": Math.max(60, clampInt(parseInt(raw["Cooldown seconds"], 10), 60, 1000000000, d["Cooldown seconds"])),
      "Allow without active rank": typeof raw["Allow without active rank"] === "boolean" ? raw["Allow without active rank"] : d["Allow without active rank"],
      "Base items": normalizeDailyRewardItems(raw["Base items"], d["Base items"])
    };
  }

  function normalizeTeleportFeatures(source) {
    const d = createDefaultTeleportFeatures();
    const raw = source && typeof source === "object" ? source : {};
    return {
      "Home command template (use {0} for home name)": String(raw["Home command template (use {0} for home name)"] != null ? raw["Home command template (use {0} for home name)"] : d["Home command template (use {0} for home name)"]).trim() || d["Home command template (use {0} for home name)"],
      "Town command": String(raw["Town command"] != null ? raw["Town command"] : d["Town command"]).trim() || d["Town command"],
      "Team command template (use {0} for target name/id)": String(raw["Team command template (use {0} for target name/id)"] != null ? raw["Team command template (use {0} for target name/id)"] : d["Team command template (use {0} for target name/id)"]).trim() || d["Team command template (use {0} for target name/id)"],
      "Home base cooldown seconds": Math.max(0, clampInt(parseInt(raw["Home base cooldown seconds"], 10), 0, 1000000, d["Home base cooldown seconds"])),
      "Home activation base delay seconds": Math.max(0, clampInt(parseInt(raw["Home activation base delay seconds"], 10), 0, 60, d["Home activation base delay seconds"])),
      "Home points without privilege": Math.max(0, clampInt(parseInt(raw["Home points without privilege"], 10), 0, 20, d["Home points without privilege"])),
      "Remove mode duration seconds": Math.max(5, clampInt(parseInt(raw["Remove mode duration seconds"], 10), 5, 600, d["Remove mode duration seconds"])),
      "Pocket recycler speed multiplier": clampFloat(parseFloat(raw["Pocket recycler speed multiplier"]), 1, 20, d["Pocket recycler speed multiplier"]),
      "Pocket recycler command cooldown seconds": Math.max(0, clampInt(parseInt(raw["Pocket recycler command cooldown seconds"], 10), 0, 600, d["Pocket recycler command cooldown seconds"])),
      "Pocket recycler auto close on menu close": typeof raw["Pocket recycler auto close on menu close"] === "boolean"
        ? raw["Pocket recycler auto close on menu close"]
        : d["Pocket recycler auto close on menu close"],
      "Pocket recycler local sounds enabled": typeof raw["Pocket recycler local sounds enabled"] === "boolean"
        ? raw["Pocket recycler local sounds enabled"]
        : d["Pocket recycler local sounds enabled"],
      "Pocket recycler open sound effect prefab": String(
        raw["Pocket recycler open sound effect prefab"] != null
          ? raw["Pocket recycler open sound effect prefab"]
          : d["Pocket recycler open sound effect prefab"]
      ).trim(),
      "Pocket recycler close sound effect prefab": String(
        raw["Pocket recycler close sound effect prefab"] != null
          ? raw["Pocket recycler close sound effect prefab"]
          : d["Pocket recycler close sound effect prefab"]
      ).trim(),
      "Pocket recycler working sound effect prefab": String(
        raw["Pocket recycler working sound effect prefab"] != null
          ? raw["Pocket recycler working sound effect prefab"]
          : d["Pocket recycler working sound effect prefab"]
      ).trim(),
      "Pocket recycler working sound interval seconds": clampFloat(
        parseFloat(raw["Pocket recycler working sound interval seconds"]),
        0.1,
        10,
        d["Pocket recycler working sound interval seconds"]
      ),
      "Team base cooldown seconds": Math.max(0, clampInt(parseInt(raw["Team base cooldown seconds"], 10), 0, 1000000, d["Team base cooldown seconds"])),
      "Town base daily limit": Math.max(0, clampInt(parseInt(raw["Town base daily limit"], 10), 0, 1000, d["Town base daily limit"])),
      "Show debug replies": typeof raw["Show debug replies"] === "boolean" ? raw["Show debug replies"] : d["Show debug replies"]
    };
  }

  function normalizeShopCurrency(value) {
    const normalized = normalizeRankKey(value);
    return normalized === "serverrewards" ? "serverrewards" : "economics";
  }

  function normalizeShopPackages(rawPackages) {
    const defaults = createDefaultShop()["Packages"];
    const source = rawPackages && typeof rawPackages === "object" ? rawPackages : defaults;
    const normalized = {};
    for (const rawKey of Object.keys(source)) {
      const pkg = source[rawKey];
      if (!pkg || typeof pkg !== "object") continue;
      const key = normalizeRankKey(rawKey);
      if (!key) continue;
      normalized[key] = {
        "Display name": String(pkg["Display name"] || key.toUpperCase()).trim() || key.toUpperCase(),
        "Rank": normalizeRankKey(pkg["Rank"] || "vip") || "vip",
        "Days (0 = permanent)": Math.max(0, clampInt(parseInt(pkg["Days (0 = permanent)"], 10), 0, 3650, 30)),
        "Economics price": Math.max(0, clampFloat(parseFloat(pkg["Economics price"]), 0, 1000000000, 0)),
        "ServerRewards price": Math.max(0, clampInt(parseInt(pkg["ServerRewards price"], 10), 0, 1000000000, 0))
      };
    }
    if (Object.keys(normalized).length === 0) return defaults;
    return normalized;
  }

  function normalizeShop(source) {
    const d = createDefaultShop();
    const raw = source && typeof source === "object" ? source : {};
    return {
      "Enabled": typeof raw["Enabled"] === "boolean" ? raw["Enabled"] : d["Enabled"],
      "Currency (economics/serverrewards)": normalizeShopCurrency(raw["Currency (economics/serverrewards)"] || d["Currency (economics/serverrewards)"]),
      "Packages": normalizeShopPackages(raw["Packages"])
    };
  }

  function normalizeAudit(source) {
    const d = createDefaultAudit();
    const raw = source && typeof source === "object" ? source : {};
    return {
      "Enabled": typeof raw["Enabled"] === "boolean" ? raw["Enabled"] : d["Enabled"],
      "Max entries": Math.max(50, clampInt(parseInt(raw["Max entries"], 10), 50, 5000, d["Max entries"])),
      "Echo to console": typeof raw["Echo to console"] === "boolean" ? raw["Echo to console"] : d["Echo to console"]
    };
  }

  function normalizeWebShopBridge(source) {
    const d = createDefaultWebShopBridge();
    const raw = source && typeof source === "object" ? source : {};
    return {
      "Enabled": typeof raw["Enabled"] === "boolean" ? raw["Enabled"] : d["Enabled"],
      "Api base url": String(raw["Api base url"] != null ? raw["Api base url"] : d["Api base url"]).trim() || d["Api base url"],
      "Server id": String(raw["Server id"] != null ? raw["Server id"] : d["Server id"]).trim() || d["Server id"],
      "Server key": String(raw["Server key"] != null ? raw["Server key"] : d["Server key"]).trim() || d["Server key"],
      "Poll interval seconds": Math.max(2, clampInt(parseInt(raw["Poll interval seconds"], 10), 2, 300, d["Poll interval seconds"])),
      "Batch size": Math.max(1, clampInt(parseInt(raw["Batch size"], 10), 1, 100, d["Batch size"])),
      "Request timeout seconds": Math.max(3, clampInt(parseInt(raw["Request timeout seconds"], 10), 3, 60, d["Request timeout seconds"])),
      "Grant source label": String(raw["Grant source label"] != null ? raw["Grant source label"] : d["Grant source label"]).trim() || d["Grant source label"]
    };
  }

  function normalizeConfig(raw) {
    const cfg = createDefaultConfig();
    if (!raw || typeof raw !== "object") return cfg;

    cfg["Notify player on connect"] = typeof raw["Notify player on connect"] === "boolean" ? raw["Notify player on connect"] : cfg["Notify player on connect"];
    cfg["Expiry check interval (seconds)"] = Math.max(15, clampInt(parseInt(raw["Expiry check interval (seconds)"], 10), 15, 10000000, 60));
    cfg["Feature permissions"] = normalizeFeaturePermissions(raw["Feature permissions"]);
    cfg["Daily rewards"] = normalizeDailyRewards(raw["Daily rewards"]);
    cfg["Teleport features"] = normalizeTeleportFeatures(raw["Teleport features"]);
    cfg["Shop"] = normalizeShop(raw["Shop"]);
    cfg["Audit"] = normalizeAudit(raw["Audit"]);
    cfg["Web shop bridge"] = normalizeWebShopBridge(raw["Web shop bridge"]);

    const ranksRaw = raw["Ranks"];
    if (!ranksRaw || typeof ranksRaw !== "object") return cfg;
    const normalizedRanks = {};
    for (const rawKey of Object.keys(ranksRaw)) {
      const key = normalizeRankKey(rawKey);
      if (!key) continue;
      normalizedRanks[key] = normalizeRank(ranksRaw[rawKey], key);
    }
    if (Object.keys(normalizedRanks).length > 0) cfg["Ranks"] = normalizedRanks;
    return cfg;
  }

  function normalizeCatalog(raw) {
    const catalog = { items: [] };
    if (!raw || typeof raw !== "object") return catalog;
    const items = Array.isArray(raw["Items"]) ? raw["Items"] : [];
    for (const item of items) {
      const shortname = normalizeShortname(item && item["Shortname"]);
      if (!shortname) continue;
      const displayName = String((item && item["Display name"]) || shortname);
      const mappedRu = DEFAULT_RU_ITEM_NAMES[shortname] ? String(DEFAULT_RU_ITEM_NAMES[shortname]) : "";
      const displayNameRu = String((item && (item["Display name ru"] || item["Display name RU"])) || mappedRu || "").trim();
      catalog.items.push({ shortname: shortname, displayName: displayName, displayNameRu: displayNameRu, category: String((item && item["Category"]) || "") });
    }
    catalog.items.sort((a, b) => a.shortname.localeCompare(b.shortname));
    return catalog;
  }

  function bindEvents() {
    els.configFileInput.addEventListener("change", onConfigFileChosen);
    els.catalogFileInput.addEventListener("change", onCatalogFileChosen);
    els.resetBtn.addEventListener("click", onResetClicked);
    els.copyBtn.addEventListener("click", onCopyClicked);
    els.downloadBtn.addEventListener("click", onDownloadClicked);
    if (els.saveServerBtn) {
      els.saveServerBtn.addEventListener("click", onSaveServerClicked);
    }
    if (els.deployPluginBtn) {
      els.deployPluginBtn.addEventListener("click", onDeployPluginClicked);
    }

    els.notifyOnConnect.addEventListener("change", onGlobalFieldsChanged);
    els.expiryInterval.addEventListener("input", onGlobalFieldsChanged);
    els.teleportPermissionsInput.addEventListener("input", onGlobalFieldsChanged);
    els.homeBasePermissionsInput.addEventListener("input", onGlobalFieldsChanged);
    els.homeLimitTemplateInput.addEventListener("input", onGlobalFieldsChanged);
    els.pocketRecyclerPermissionsInput.addEventListener("input", onGlobalFieldsChanged);
    els.removePermissionsInput.addEventListener("input", onGlobalFieldsChanged);
    els.dailyEnabled.addEventListener("change", onGlobalFieldsChanged);
    els.dailyCooldownSeconds.addEventListener("input", onGlobalFieldsChanged);
    els.dailyAllowWithoutRank.addEventListener("change", onGlobalFieldsChanged);
    els.dailyItemsInput.addEventListener("input", onGlobalFieldsChanged);
    els.homeCommandTemplateInput.addEventListener("input", onGlobalFieldsChanged);
    els.townCommandInput.addEventListener("input", onGlobalFieldsChanged);
    els.teamCommandTemplateInput.addEventListener("input", onGlobalFieldsChanged);
    els.homeBaseCooldownInput.addEventListener("input", onGlobalFieldsChanged);
    els.homeActivationDelayInput.addEventListener("input", onGlobalFieldsChanged);
    els.homePointsWithoutPrivilegeInput.addEventListener("input", onGlobalFieldsChanged);
    els.removeModeDurationInput.addEventListener("input", onGlobalFieldsChanged);
    els.pocketRecyclerSpeedMultiplierInput.addEventListener("input", onGlobalFieldsChanged);
    els.pocketRecyclerCommandCooldownInput.addEventListener("input", onGlobalFieldsChanged);
    els.pocketRecyclerAutoCloseInput.addEventListener("change", onGlobalFieldsChanged);
    els.pocketRecyclerLocalSoundsInput.addEventListener("change", onGlobalFieldsChanged);
    els.pocketRecyclerOpenSoundInput.addEventListener("input", onGlobalFieldsChanged);
    els.pocketRecyclerCloseSoundInput.addEventListener("input", onGlobalFieldsChanged);
    els.pocketRecyclerWorkingSoundInput.addEventListener("input", onGlobalFieldsChanged);
    els.pocketRecyclerWorkingSoundIntervalInput.addEventListener("input", onGlobalFieldsChanged);
    els.teamBaseCooldownInput.addEventListener("input", onGlobalFieldsChanged);
    els.townBaseLimitInput.addEventListener("input", onGlobalFieldsChanged);
    els.tpDebugRepliesInput.addEventListener("change", onGlobalFieldsChanged);
    els.shopEnabledInput.addEventListener("change", onGlobalFieldsChanged);
    els.shopCurrencyInput.addEventListener("change", onGlobalFieldsChanged);
    els.shopPackagesInput.addEventListener("input", onGlobalFieldsChanged);
    els.auditEnabledInput.addEventListener("change", onGlobalFieldsChanged);
    els.auditMaxEntriesInput.addEventListener("input", onGlobalFieldsChanged);
    els.auditEchoToConsoleInput.addEventListener("change", onGlobalFieldsChanged);
    els.webShopEnabledInput.addEventListener("change", onGlobalFieldsChanged);
    els.webShopApiBaseUrlInput.addEventListener("input", onGlobalFieldsChanged);
    els.webShopServerIdInput.addEventListener("input", onGlobalFieldsChanged);
    els.webShopServerKeyInput.addEventListener("input", onGlobalFieldsChanged);
    els.webShopPollIntervalInput.addEventListener("input", onGlobalFieldsChanged);
    els.webShopBatchSizeInput.addEventListener("input", onGlobalFieldsChanged);
    els.webShopRequestTimeoutInput.addEventListener("input", onGlobalFieldsChanged);
    els.webShopGrantSourceInput.addEventListener("input", onGlobalFieldsChanged);

    els.rankSearch.addEventListener("input", renderRanksList);
    els.addRankBtn.addEventListener("click", onAddRankClicked);
    els.renameRankBtn.addEventListener("click", onRenameRankClicked);
    els.deleteRankBtn.addEventListener("click", onDeleteRankClicked);

    els.displayName.addEventListener("input", onRankFieldsChanged);
    els.chatTag.addEventListener("input", onRankFieldsChanged);
    els.chatColor.addEventListener("input", onRankFieldsChanged);
    els.oxideGroup.addEventListener("input", onRankFieldsChanged);
    els.gatherMultiplier.addEventListener("input", onRankFieldsChanged);
    els.groundMultiplier.addEventListener("input", onRankFieldsChanged);
    els.containerMultiplier.addEventListener("input", onRankFieldsChanged);
    els.npcScrapReward.addEventListener("input", onRankFieldsChanged);
    els.kitCooldownSeconds.addEventListener("input", onRankFieldsChanged);
    els.kitAmountMultiplier.addEventListener("input", onRankFieldsChanged);
    els.allowTeleport.addEventListener("change", onRankFieldsChanged);
    els.homePoints.addEventListener("input", onRankFieldsChanged);
    els.allowPocketRecycler.addEventListener("change", onRankFieldsChanged);
    els.pocketRecyclerRankSpeedMultiplier.addEventListener("input", onRankFieldsChanged);
    els.pocketRecyclerRankOutputMultiplier.addEventListener("input", onRankFieldsChanged);
    els.allowRemoveCommand.addEventListener("change", onRankFieldsChanged);
    els.dailyRewardMultiplier.addEventListener("input", onRankFieldsChanged);
    els.homeTpCooldownReduction.addEventListener("input", onRankFieldsChanged);
    els.teamTpCooldownReduction.addEventListener("input", onRankFieldsChanged);
    els.townTpDailyLimitBonus.addEventListener("input", onRankFieldsChanged);

    els.newPermission.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        onAddPermissionClicked();
      }
    });
    els.addPermissionBtn.addEventListener("click", onAddPermissionClicked);

    els.addKitShortname.addEventListener("input", renderKitItemHint);
    els.addKitShortname.addEventListener("change", renderKitItemHint);
    els.addKitItemBtn.addEventListener("click", onAddKitItemClicked);
  }

  function onConfigFileChosen(event) {
    const file = event.target.files && event.target.files[0];
    if (!file) return;
    readJsonFile(file)
      .then((json) => {
        state.config = normalizeConfig(json);
        state.selectedRankKey = null;
        renderAll();
      })
      .catch((err) => alert("Не удалось загрузить конфиг: " + err.message))
      .finally(() => {
        els.configFileInput.value = "";
      });
  }

  function onCatalogFileChosen(event) {
    const file = event.target.files && event.target.files[0];
    if (!file) return;
    readJsonFile(file)
      .then((json) => {
        state.catalog = normalizeCatalog(json);
        rebuildItemLookup();
        rebuildItemDatalist();
        renderCatalogInfo();
        renderKitItemHint();
        renderRankEditor();
      })
      .catch((err) => alert("Не удалось загрузить каталог: " + err.message))
      .finally(() => {
        els.catalogFileInput.value = "";
      });
  }

  function onResetClicked() {
    if (!confirm("Сбросить конфигуратор к значениям по умолчанию?")) return;
    state.config = createDefaultConfig();
    state.catalog = EMBEDDED_CATALOG;
    state.selectedRankKey = null;
    rebuildItemLookup();
    renderAll();
  }

  async function onCopyClicked() {
    const text = JSON.stringify(buildOutputConfig(), null, 2);
    try {
      await navigator.clipboard.writeText(text);
      alert("JSON скопирован.");
    } catch (_err) {
      alert("Буфер обмена недоступен. Используй кнопку 'Скачать JSON'.");
    }
  }

  function onDownloadClicked() {
    const blob = new Blob([JSON.stringify(buildOutputConfig(), null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "PrivilegeSystem.json";
    a.click();
    URL.revokeObjectURL(url);
  }

  function setSaveServerStatus(text, isError) {
    if (!els.saveServerStatus) return;
    els.saveServerStatus.textContent = String(text || "");
    els.saveServerStatus.style.color = isError ? "#ff7b72" : "#3fb950";
  }

  async function onSaveServerClicked() {
    if (!els.saveServerBtn) return;

    els.saveServerBtn.disabled = true;
    setSaveServerStatus("Сохранение...", false);

    try {
      const res = await fetch(SERVER_SAVE_ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          config_type: "privilege",
          config: buildOutputConfig()
        })
      });

      let payload = {};
      try {
        payload = await res.json();
      } catch (_err) {
        payload = {};
      }

      if (!res.ok || payload.ok !== true) {
        const details = String(payload.error || `HTTP ${res.status}`);
        throw new Error(details);
      }

      const targetPath = String(payload.path || "C:\\rust\\server\\oxide\\config\\PrivilegeSystem.json");
      setSaveServerStatus("Сохранено на сервер", false);
      alert(`Конфиг сохранен:\n${targetPath}\n\nВыполни на сервере: oxide.reload PrivilegeSystem`);
    } catch (err) {
      setSaveServerStatus("Ошибка сохранения", true);
      alert(
        "Не удалось сохранить конфиг на сервер автоматически.\n" +
        "Запускай конфигуратор через open-configurator.ps1.\n\n" +
        "Ошибка: " + (err && err.message ? err.message : String(err))
      );
    } finally {
      els.saveServerBtn.disabled = false;
    }
  }

  function setDeployPluginStatus(text, isError) {
    if (!els.deployPluginStatus) return;
    els.deployPluginStatus.textContent = String(text || "");
    els.deployPluginStatus.style.color = isError ? "#ff7b72" : "#3fb950";
  }

  async function onDeployPluginClicked() {
    if (!els.deployPluginBtn) return;

    els.deployPluginBtn.disabled = true;
    setDeployPluginStatus("Развертывание...", false);

    try {
      const res = await fetch(SERVER_DEPLOY_ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          plugin: "privilege"
        })
      });

      let payload = {};
      try {
        payload = await res.json();
      } catch (_err) {
        payload = {};
      }

      if (!res.ok || payload.ok !== true) {
        const details = String(payload.error || `HTTP ${res.status}`);
        throw new Error(details);
      }

      const targetPath = String(payload.path || "C:\\rust\\server\\oxide\\plugins\\PrivilegeSystem.cs");
      setDeployPluginStatus("Плагин развернут", false);
      alert(`Плагин развернут:\n${targetPath}\n\nВыполни на сервере: oxide.reload PrivilegeSystem`);
    } catch (err) {
      setDeployPluginStatus("Ошибка развертывания", true);
      alert(
        "Не удалось развернуть плагин автоматически.\n" +
        "Запускай конфигуратор через open-configurator.ps1.\n\n" +
        "Ошибка: " + (err && err.message ? err.message : String(err))
      );
    } finally {
      els.deployPluginBtn.disabled = false;
    }
  }

  function onGlobalFieldsChanged() {
    state.config["Notify player on connect"] = !!els.notifyOnConnect.checked;
    state.config["Expiry check interval (seconds)"] = Math.max(15, clampInt(parseInt(els.expiryInterval.value, 10), 15, 10000000, state.config["Expiry check interval (seconds)"]));
    state.config["Feature permissions"] = normalizeFeaturePermissions({
      "Teleport permissions": parsePermissionsCsv(els.teleportPermissionsInput.value),
      "Home base permissions": parsePermissionsCsv(els.homeBasePermissionsInput.value),
      "Home limit permission template (use {0} for points, empty = disabled)": String(els.homeLimitTemplateInput.value || "").trim(),
      "Pocket recycler permissions": parsePermissionsCsv(els.pocketRecyclerPermissionsInput.value),
      "Remove command permissions": parsePermissionsCsv(els.removePermissionsInput.value)
    });
    state.config["Daily rewards"] = normalizeDailyRewards({
      "Enabled": !!els.dailyEnabled.checked,
      "Cooldown seconds": Math.max(60, clampInt(parseInt(els.dailyCooldownSeconds.value, 10), 60, 1000000000, 86400)),
      "Allow without active rank": !!els.dailyAllowWithoutRank.checked,
      "Base items": parseItemListLines(els.dailyItemsInput.value)
    });
    state.config["Teleport features"] = normalizeTeleportFeatures({
      "Home command template (use {0} for home name)": String(els.homeCommandTemplateInput.value || "").trim(),
      "Town command": String(els.townCommandInput.value || "").trim(),
      "Team command template (use {0} for target name/id)": String(els.teamCommandTemplateInput.value || "").trim(),
      "Home base cooldown seconds": Math.max(0, clampInt(parseInt(els.homeBaseCooldownInput.value, 10), 0, 1000000, 30)),
      "Home activation base delay seconds": Math.max(0, clampInt(parseInt(els.homeActivationDelayInput.value, 10), 0, 60, 15)),
      "Home points without privilege": Math.max(0, clampInt(parseInt(els.homePointsWithoutPrivilegeInput.value, 10), 0, 20, 1)),
      "Remove mode duration seconds": Math.max(5, clampInt(parseInt(els.removeModeDurationInput.value, 10), 5, 600, 30)),
      "Pocket recycler speed multiplier": clampFloat(parseFloat(els.pocketRecyclerSpeedMultiplierInput.value), 1, 20, 1),
      "Pocket recycler command cooldown seconds": Math.max(0, clampInt(parseInt(els.pocketRecyclerCommandCooldownInput.value, 10), 0, 600, 10)),
      "Pocket recycler auto close on menu close": !!els.pocketRecyclerAutoCloseInput.checked,
      "Pocket recycler local sounds enabled": !!els.pocketRecyclerLocalSoundsInput.checked,
      "Pocket recycler open sound effect prefab": String(els.pocketRecyclerOpenSoundInput.value || "").trim(),
      "Pocket recycler close sound effect prefab": String(els.pocketRecyclerCloseSoundInput.value || "").trim(),
      "Pocket recycler working sound effect prefab": String(els.pocketRecyclerWorkingSoundInput.value || "").trim(),
      "Pocket recycler working sound interval seconds": clampFloat(parseFloat(els.pocketRecyclerWorkingSoundIntervalInput.value), 0.1, 10, 1.2),
      "Team base cooldown seconds": Math.max(0, clampInt(parseInt(els.teamBaseCooldownInput.value, 10), 0, 1000000, 15)),
      "Town base daily limit": Math.max(0, clampInt(parseInt(els.townBaseLimitInput.value, 10), 0, 1000, 10)),
      "Show debug replies": !!els.tpDebugRepliesInput.checked
    });
    state.config["Shop"] = normalizeShop({
      "Enabled": !!els.shopEnabledInput.checked,
      "Currency (economics/serverrewards)": normalizeShopCurrency(els.shopCurrencyInput.value),
      "Packages": parseShopPackagesLines(els.shopPackagesInput.value)
    });
    state.config["Audit"] = normalizeAudit({
      "Enabled": !!els.auditEnabledInput.checked,
      "Max entries": Math.max(50, clampInt(parseInt(els.auditMaxEntriesInput.value, 10), 50, 5000, 500)),
      "Echo to console": !!els.auditEchoToConsoleInput.checked
    });
    state.config["Web shop bridge"] = normalizeWebShopBridge({
      "Enabled": !!els.webShopEnabledInput.checked,
      "Api base url": String(els.webShopApiBaseUrlInput.value || "").trim(),
      "Server id": String(els.webShopServerIdInput.value || "").trim(),
      "Server key": String(els.webShopServerKeyInput.value || "").trim(),
      "Poll interval seconds": Math.max(2, clampInt(parseInt(els.webShopPollIntervalInput.value, 10), 2, 300, 10)),
      "Batch size": Math.max(1, clampInt(parseInt(els.webShopBatchSizeInput.value, 10), 1, 100, 10)),
      "Request timeout seconds": Math.max(3, clampInt(parseInt(els.webShopRequestTimeoutInput.value, 10), 3, 60, 10)),
      "Grant source label": String(els.webShopGrantSourceInput.value || "").trim()
    });
    renderOutput();
  }

  function onAddRankClicked() {
    const key = normalizeRankKey(els.newRankKey.value);
    if (!key) return;
    if (!state.config["Ranks"][key]) state.config["Ranks"][key] = createEmptyRank(key);
    state.selectedRankKey = key;
    els.newRankKey.value = "";
    renderAll();
  }

  function onRenameRankClicked() {
    const oldKey = state.selectedRankKey;
    if (!oldKey) return;
    const newKey = normalizeRankKey(els.rankKeyInput.value);
    if (!newKey || newKey === oldKey) return;
    if (state.config["Ranks"][newKey]) {
      alert("Ранг с таким ключом уже существует.");
      return;
    }
    state.config["Ranks"][newKey] = state.config["Ranks"][oldKey];
    delete state.config["Ranks"][oldKey];
    state.selectedRankKey = newKey;
    renderAll();
  }

  function onDeleteRankClicked() {
    const key = state.selectedRankKey;
    if (!key) return;
    const keys = getRankKeys();
    if (keys.length <= 1) {
      alert("Нельзя удалить последний ранг.");
      return;
    }
    if (!confirm(`Удалить ранг '${key}'?`)) return;
    delete state.config["Ranks"][key];
    const nextKeys = getRankKeys();
    state.selectedRankKey = nextKeys.length > 0 ? nextKeys[0] : null;
    renderAll();
  }

  function onRankFieldsChanged() {
    const rank = getSelectedRank();
    if (!rank) return;

    rank["Display name"] = String(els.displayName.value || "").trim() || rank["Display name"];
    rank["Chat tag"] = String(els.chatTag.value || "").trim();
    rank["Chat color"] = String(els.chatColor.value || "").trim();
    rank["Oxide group (optional)"] = String(els.oxideGroup.value || "").trim();
    rank["Allow teleport"] = !!els.allowTeleport.checked;
    rank["Home points"] = Math.max(0, clampInt(parseInt(els.homePoints.value, 10), 0, 100, rank["Home points"]));
    rank["Allow pocket recycler"] = !!els.allowPocketRecycler.checked;
    rank["Pocket recycler speed multiplier"] = clampFloat(parseFloat(els.pocketRecyclerRankSpeedMultiplier.value), 1, 20, rank["Pocket recycler speed multiplier"]);
    rank["Pocket recycler output multiplier"] = clampFloat(parseFloat(els.pocketRecyclerRankOutputMultiplier.value), 1, 20, rank["Pocket recycler output multiplier"]);
    rank["Allow remove command"] = !!els.allowRemoveCommand.checked;
    rank["Daily reward multiplier"] = clampFloat(parseFloat(els.dailyRewardMultiplier.value), 0.1, 20, rank["Daily reward multiplier"]);
    rank["Home teleport cooldown reduction (seconds)"] = Math.max(0, clampInt(parseInt(els.homeTpCooldownReduction.value, 10), 0, 600, rank["Home teleport cooldown reduction (seconds)"]));
    rank["Team teleport cooldown reduction (seconds)"] = Math.max(0, clampInt(parseInt(els.teamTpCooldownReduction.value, 10), 0, 600, rank["Team teleport cooldown reduction (seconds)"]));
    rank["Town teleport daily limit bonus"] = Math.max(0, clampInt(parseInt(els.townTpDailyLimitBonus.value, 10), 0, 1000, rank["Town teleport daily limit bonus"]));
    rank["Gather multiplier"] = clampFloat(parseFloat(els.gatherMultiplier.value), 1, 10, rank["Gather multiplier"]);
    rank["Ground pickup multiplier"] = clampFloat(parseFloat(els.groundMultiplier.value), 1, 10, rank["Ground pickup multiplier"]);
    rank["Container loot multiplier"] = clampFloat(parseFloat(els.containerMultiplier.value), 1, 10, rank["Container loot multiplier"]);
    rank["NPC kill scrap reward"] = Math.max(0, clampInt(parseInt(els.npcScrapReward.value, 10), 0, 100000000, rank["NPC kill scrap reward"]));
    rank["Rank kit cooldown seconds"] = Math.max(0, clampInt(parseInt(els.kitCooldownSeconds.value, 10), 0, 1000000000, rank["Rank kit cooldown seconds"]));
    rank["Rank kit amount multiplier"] = clampFloat(parseFloat(els.kitAmountMultiplier.value), 1, 20, rank["Rank kit amount multiplier"]);

    renderRanksList();
    renderOutput();
  }

  function onAddPermissionClicked() {
    const rank = getSelectedRank();
    if (!rank) return;
    const permission = normalizePermission(els.newPermission.value);
    if (!permission) return;
    rank["Permissions"].push(permission);
    rank["Permissions"] = dedupePermissions(rank["Permissions"]);
    els.newPermission.value = "";
    renderRankEditor();
    renderOutput();
  }

  function onPermissionChanged(index, value) {
    const rank = getSelectedRank();
    if (!rank || !Array.isArray(rank["Permissions"])) return;
    if (index < 0 || index >= rank["Permissions"].length) return;
    rank["Permissions"][index] = normalizePermission(value);
    rank["Permissions"] = dedupePermissions(rank["Permissions"]);
    renderRankEditor();
    renderOutput();
  }

  function removePermission(index) {
    const rank = getSelectedRank();
    if (!rank || !Array.isArray(rank["Permissions"])) return;
    if (index < 0 || index >= rank["Permissions"].length) return;
    rank["Permissions"].splice(index, 1);
    renderRankEditor();
    renderOutput();
  }

  function onAddKitItemClicked() {
    const rank = getSelectedRank();
    if (!rank) return;
    const shortname = normalizeShortname(els.addKitShortname.value);
    if (!shortname) return;
    const amount = Math.max(1, clampInt(parseInt(els.addKitAmount.value, 10), 1, 100000000, 1));
    rank["Rank kit items"].push({ "Shortname": shortname, "Amount": amount });
    els.addKitShortname.value = "";
    els.addKitAmount.value = "100";
    renderKitItemHint();
    renderRankEditor();
    renderOutput();
  }

  function onKitItemChanged(index, field, value) {
    const rank = getSelectedRank();
    if (!rank || !Array.isArray(rank["Rank kit items"])) return;
    if (index < 0 || index >= rank["Rank kit items"].length) return;
    const item = rank["Rank kit items"][index];
    if (!item) return;
    if (field === "Shortname") item["Shortname"] = normalizeShortname(value) || item["Shortname"];
    if (field === "Amount") item["Amount"] = Math.max(1, clampInt(parseInt(value, 10), 1, 100000000, item["Amount"]));
    renderRankEditor();
    renderOutput();
  }

  function removeKitItem(index) {
    const rank = getSelectedRank();
    if (!rank || !Array.isArray(rank["Rank kit items"])) return;
    if (index < 0 || index >= rank["Rank kit items"].length) return;
    rank["Rank kit items"].splice(index, 1);
    renderRankEditor();
    renderOutput();
  }

  function rebuildItemLookup() {
    const lookup = Object.create(null);
    if (state.catalog && Array.isArray(state.catalog.items)) {
      for (const item of state.catalog.items) {
        const shortname = normalizeShortname(item.shortname);
        if (!shortname) continue;
        lookup[shortname] = item;
      }
    }
    state.itemByShortname = lookup;
  }

  function getItemMeta(shortname) {
    const key = normalizeShortname(shortname);
    if (!key) return null;
    return state.itemByShortname[key] || null;
  }

  function getItemLabel(shortname) {
    const item = getItemMeta(shortname);
    const key = normalizeShortname(shortname);
    if (!key) return "";
    if (!item) return key;
    const en = item.displayName || key;
    const ru = item.displayNameRu || "";
    if (ru && normalizeShortname(ru) !== normalizeShortname(en)) return `${ru} / ${en}`;
    return ru || en;
  }

  function rebuildItemDatalist() {
    els.itemShortnamesList.innerHTML = "";
    for (const item of state.catalog.items) {
      const option = document.createElement("option");
      option.value = item.shortname;
      option.label = getItemLabel(item.shortname) + (item.category ? ` (${item.category})` : "");
      option.textContent = option.label;
      els.itemShortnamesList.appendChild(option);
    }
  }

  function renderKitItemHint() {
    const shortname = normalizeShortname(els.addKitShortname.value);
    if (!shortname) {
      els.kitItemHint.textContent = "";
      return;
    }
    const item = getItemMeta(shortname);
    if (!item) {
      els.kitItemHint.textContent = `Неизвестный shortname: ${shortname}`;
      return;
    }
    els.kitItemHint.textContent = getItemLabel(shortname) + (item.category ? ` (${item.category})` : "");
  }

  function renderAll() {
    if (!state.selectedRankKey) {
      const keys = getRankKeys();
      if (keys.length > 0) state.selectedRankKey = keys[0];
    }
    renderGlobals();
    renderCatalogInfo();
    rebuildItemLookup();
    rebuildItemDatalist();
    renderKitItemHint();
    renderRanksList();
    renderRankEditor();
    renderOutput();
  }

  function renderGlobals() {
    const featurePermissions = normalizeFeaturePermissions(state.config["Feature permissions"]);
    state.config["Feature permissions"] = featurePermissions;
    const dailyRewards = normalizeDailyRewards(state.config["Daily rewards"]);
    state.config["Daily rewards"] = dailyRewards;
    const teleportFeatures = normalizeTeleportFeatures(state.config["Teleport features"]);
    state.config["Teleport features"] = teleportFeatures;
    const shop = normalizeShop(state.config["Shop"]);
    state.config["Shop"] = shop;
    const audit = normalizeAudit(state.config["Audit"]);
    state.config["Audit"] = audit;
    const webShopBridge = normalizeWebShopBridge(state.config["Web shop bridge"]);
    state.config["Web shop bridge"] = webShopBridge;

    els.notifyOnConnect.checked = !!state.config["Notify player on connect"];
    els.expiryInterval.value = String(state.config["Expiry check interval (seconds)"]);
    els.teleportPermissionsInput.value = formatPermissionsCsv(featurePermissions["Teleport permissions"]);
    els.homeBasePermissionsInput.value = formatPermissionsCsv(featurePermissions["Home base permissions"]);
    els.homeLimitTemplateInput.value = String(featurePermissions["Home limit permission template (use {0} for points, empty = disabled)"] || "");
    els.pocketRecyclerPermissionsInput.value = formatPermissionsCsv(featurePermissions["Pocket recycler permissions"]);
    els.removePermissionsInput.value = formatPermissionsCsv(featurePermissions["Remove command permissions"]);
    els.dailyEnabled.checked = !!dailyRewards["Enabled"];
    els.dailyCooldownSeconds.value = String(dailyRewards["Cooldown seconds"]);
    els.dailyAllowWithoutRank.checked = !!dailyRewards["Allow without active rank"];
    els.dailyItemsInput.value = formatItemListLines(dailyRewards["Base items"]);
    els.homeCommandTemplateInput.value = String(teleportFeatures["Home command template (use {0} for home name)"] || "");
    els.townCommandInput.value = String(teleportFeatures["Town command"] || "");
    els.teamCommandTemplateInput.value = String(teleportFeatures["Team command template (use {0} for target name/id)"] || "");
    els.homeBaseCooldownInput.value = String(teleportFeatures["Home base cooldown seconds"]);
    els.homeActivationDelayInput.value = String(teleportFeatures["Home activation base delay seconds"]);
    els.homePointsWithoutPrivilegeInput.value = String(teleportFeatures["Home points without privilege"]);
    els.removeModeDurationInput.value = String(teleportFeatures["Remove mode duration seconds"]);
    els.pocketRecyclerSpeedMultiplierInput.value = String(teleportFeatures["Pocket recycler speed multiplier"]);
    els.pocketRecyclerCommandCooldownInput.value = String(teleportFeatures["Pocket recycler command cooldown seconds"]);
    els.pocketRecyclerAutoCloseInput.checked = !!teleportFeatures["Pocket recycler auto close on menu close"];
    els.pocketRecyclerLocalSoundsInput.checked = !!teleportFeatures["Pocket recycler local sounds enabled"];
    els.pocketRecyclerOpenSoundInput.value = String(teleportFeatures["Pocket recycler open sound effect prefab"] || "");
    els.pocketRecyclerCloseSoundInput.value = String(teleportFeatures["Pocket recycler close sound effect prefab"] || "");
    els.pocketRecyclerWorkingSoundInput.value = String(teleportFeatures["Pocket recycler working sound effect prefab"] || "");
    els.pocketRecyclerWorkingSoundIntervalInput.value = String(teleportFeatures["Pocket recycler working sound interval seconds"]);
    els.teamBaseCooldownInput.value = String(teleportFeatures["Team base cooldown seconds"]);
    els.townBaseLimitInput.value = String(teleportFeatures["Town base daily limit"]);
    els.tpDebugRepliesInput.checked = !!teleportFeatures["Show debug replies"];
    els.shopEnabledInput.checked = !!shop["Enabled"];
    els.shopCurrencyInput.value = normalizeShopCurrency(shop["Currency (economics/serverrewards)"]);
    els.shopPackagesInput.value = formatShopPackagesLines(shop["Packages"]);
    els.auditEnabledInput.checked = !!audit["Enabled"];
    els.auditMaxEntriesInput.value = String(audit["Max entries"]);
    els.auditEchoToConsoleInput.checked = !!audit["Echo to console"];
    els.webShopEnabledInput.checked = !!webShopBridge["Enabled"];
    els.webShopApiBaseUrlInput.value = String(webShopBridge["Api base url"] || "");
    els.webShopServerIdInput.value = String(webShopBridge["Server id"] || "");
    els.webShopServerKeyInput.value = String(webShopBridge["Server key"] || "");
    els.webShopPollIntervalInput.value = String(webShopBridge["Poll interval seconds"]);
    els.webShopBatchSizeInput.value = String(webShopBridge["Batch size"]);
    els.webShopRequestTimeoutInput.value = String(webShopBridge["Request timeout seconds"]);
    els.webShopGrantSourceInput.value = String(webShopBridge["Grant source label"] || "");
  }

  function renderCatalogInfo() {
    const itemCount = Array.isArray(state.catalog.items) ? state.catalog.items.length : 0;
    els.catalogInfo.textContent = itemCount > 0 ? `Каталог предметов загружен: ${itemCount}` : "Каталог предметов: не загружен";
  }

  function renderRanksList() {
    const search = normalizeRankKey(els.rankSearch.value);
    const keys = getRankKeys().filter((key) => !search || key.includes(search));
    els.ranksList.innerHTML = "";
    for (const key of keys) {
      const rank = state.config["Ranks"][key];
      const features = [];
      if (rank["Allow teleport"]) features.push("TP");
      if (rank["Home points"] > 0) features.push(`HOME:${rank["Home points"]}`);
      if (rank["Allow pocket recycler"]) {
        features.push(`REC:Sx${formatFloat(rank["Pocket recycler speed multiplier"])} Rx${formatFloat(rank["Pocket recycler output multiplier"])}`);
      }
      if (rank["Allow remove command"]) features.push("REMOVE");
      if (rank["Daily reward multiplier"] > 1.01) features.push(`DAILY:x${formatFloat(rank["Daily reward multiplier"])}`);
      if (rank["Home teleport cooldown reduction (seconds)"] > 0) features.push(`HCD:-${rank["Home teleport cooldown reduction (seconds)"]}`);
      if (rank["Team teleport cooldown reduction (seconds)"] > 0) features.push(`TCD:-${rank["Team teleport cooldown reduction (seconds)"]}`);
      if (rank["Town teleport daily limit bonus"] > 0) features.push(`TOWN:+${rank["Town teleport daily limit bonus"]}`);
      const btn = document.createElement("button");
      btn.className = "rank-btn" + (key === state.selectedRankKey ? " selected" : "");
      btn.textContent = `${key} | ${rank["Display name"]} | loot x${formatFloat(rank["Container loot multiplier"])} | kit x${formatFloat(rank["Rank kit amount multiplier"])}${features.length ? ` | ${features.join(" ")}` : ""}`;
      btn.addEventListener("click", () => {
        state.selectedRankKey = key;
        renderRankEditor();
        renderRanksList();
      });
      els.ranksList.appendChild(btn);
    }
  }

  function renderRankEditor() {
    const rank = getSelectedRank();
    if (!rank) {
      els.noRankState.classList.remove("hidden");
      els.rankEditor.classList.add("hidden");
      return;
    }
    els.noRankState.classList.add("hidden");
    els.rankEditor.classList.remove("hidden");

    els.rankKeyInput.value = state.selectedRankKey || "";
    els.displayName.value = String(rank["Display name"] || "");
    els.chatTag.value = String(rank["Chat tag"] || "");
    els.chatColor.value = String(rank["Chat color"] || "");
    els.oxideGroup.value = String(rank["Oxide group (optional)"] || "");
    els.allowTeleport.checked = !!rank["Allow teleport"];
    els.homePoints.value = String(rank["Home points"]);
    els.allowPocketRecycler.checked = !!rank["Allow pocket recycler"];
    els.pocketRecyclerRankSpeedMultiplier.value = String(rank["Pocket recycler speed multiplier"]);
    els.pocketRecyclerRankOutputMultiplier.value = String(rank["Pocket recycler output multiplier"]);
    els.allowRemoveCommand.checked = !!rank["Allow remove command"];
    els.dailyRewardMultiplier.value = String(rank["Daily reward multiplier"]);
    els.homeTpCooldownReduction.value = String(rank["Home teleport cooldown reduction (seconds)"]);
    els.teamTpCooldownReduction.value = String(rank["Team teleport cooldown reduction (seconds)"]);
    els.townTpDailyLimitBonus.value = String(rank["Town teleport daily limit bonus"]);
    els.gatherMultiplier.value = String(rank["Gather multiplier"]);
    els.groundMultiplier.value = String(rank["Ground pickup multiplier"]);
    els.containerMultiplier.value = String(rank["Container loot multiplier"]);
    els.npcScrapReward.value = String(rank["NPC kill scrap reward"]);
    els.kitCooldownSeconds.value = String(rank["Rank kit cooldown seconds"]);
    els.kitAmountMultiplier.value = String(rank["Rank kit amount multiplier"]);

    els.permissionsBody.innerHTML = "";
    rank["Permissions"].forEach((permissionName, index) => {
      const tr = document.createElement("tr");
      const tdP = document.createElement("td");
      const input = document.createElement("input");
      input.type = "text";
      input.className = "table-input";
      input.value = String(permissionName);
      input.addEventListener("change", () => onPermissionChanged(index, input.value));
      tdP.appendChild(input);
      tr.appendChild(tdP);
      const tdA = document.createElement("td");
      const delBtn = document.createElement("button");
      delBtn.className = "btn danger small";
      delBtn.textContent = "X";
      delBtn.addEventListener("click", () => removePermission(index));
      tdA.appendChild(delBtn);
      tr.appendChild(tdA);
      els.permissionsBody.appendChild(tr);
    });

    els.kitItemsBody.innerHTML = "";
    rank["Rank kit items"].forEach((entry, index) => {
      const tr = document.createElement("tr");
      const tdS = document.createElement("td");
      const shortInput = document.createElement("input");
      shortInput.type = "text";
      shortInput.className = "table-input";
      shortInput.setAttribute("list", "itemShortnamesList");
      shortInput.value = String(entry["Shortname"]);
      shortInput.addEventListener("change", () => onKitItemChanged(index, "Shortname", shortInput.value));
      tdS.appendChild(shortInput);
      tr.appendChild(tdS);
      const tdN = document.createElement("td");
      tdN.className = "name-cell";
      tdN.textContent = getItemLabel(entry["Shortname"]);
      tr.appendChild(tdN);
      const tdAmount = document.createElement("td");
      const amountInput = document.createElement("input");
      amountInput.type = "number";
      amountInput.min = "1";
      amountInput.step = "1";
      amountInput.className = "table-input";
      amountInput.value = String(entry["Amount"]);
      amountInput.addEventListener("change", () => onKitItemChanged(index, "Amount", amountInput.value));
      tdAmount.appendChild(amountInput);
      tr.appendChild(tdAmount);
      const tdA = document.createElement("td");
      const delBtn = document.createElement("button");
      delBtn.className = "btn danger small";
      delBtn.textContent = "X";
      delBtn.addEventListener("click", () => removeKitItem(index));
      tdA.appendChild(delBtn);
      tr.appendChild(tdA);
      els.kitItemsBody.appendChild(tr);
    });
  }

  function renderOutput() {
    els.outputJson.value = JSON.stringify(buildOutputConfig(), null, 2);
  }

  function buildOutputConfig() {
    const output = {
      "Notify player on connect": !!state.config["Notify player on connect"],
      "Expiry check interval (seconds)": Math.max(15, clampInt(parseInt(state.config["Expiry check interval (seconds)"], 10), 15, 10000000, 60)),
      "Feature permissions": normalizeFeaturePermissions(state.config["Feature permissions"]),
      "Daily rewards": normalizeDailyRewards(state.config["Daily rewards"]),
      "Teleport features": normalizeTeleportFeatures(state.config["Teleport features"]),
      "Shop": normalizeShop(state.config["Shop"]),
      "Audit": normalizeAudit(state.config["Audit"]),
      "Web shop bridge": normalizeWebShopBridge(state.config["Web shop bridge"]),
      "Ranks": {}
    };
    for (const key of getRankKeys()) output["Ranks"][key] = normalizeRank(state.config["Ranks"][key], key);
    if (Object.keys(output["Ranks"]).length === 0) output["Ranks"] = createDefaultRanks();
    return output;
  }

  function getRankKeys() {
    return Object.keys(state.config["Ranks"] || {}).sort((a, b) => a.localeCompare(b));
  }

  function getSelectedRank() {
    return state.selectedRankKey ? (state.config["Ranks"][state.selectedRankKey] || null) : null;
  }

  function dedupePermissions(rawPermissions) {
    const set = new Set();
    const result = [];
    for (const value of rawPermissions || []) {
      const p = normalizePermission(value);
      if (!p || set.has(p)) continue;
      set.add(p);
      result.push(p);
    }
    return result;
  }

  function parsePermissionsCsv(value) {
    if (!value) return [];
    return dedupePermissions(String(value).split(/[\n,;]+/g).map((token) => token.trim()).filter((token) => token.length > 0));
  }

  function formatPermissionsCsv(list) {
    return dedupePermissions(list || []).join(", ");
  }

  function parseItemListLines(value) {
    const lines = String(value || "").split(/\r?\n/g);
    const items = [];
    for (const rawLine of lines) {
      const line = String(rawLine || "").trim();
      if (!line || line.startsWith("#")) continue;
      const parts = line.split(/[\s,;:]+/g).filter((x) => !!x);
      if (parts.length < 2) continue;
      const shortname = normalizeShortname(parts[0]);
      const amount = Math.max(1, clampInt(parseInt(parts[1], 10), 1, 100000000, 1));
      if (!shortname) continue;
      items.push({ "Shortname": shortname, "Amount": amount });
    }
    return items;
  }

  function formatItemListLines(items) {
    if (!Array.isArray(items) || items.length === 0) return "";
    return items.map((entry) => `${normalizeShortname(entry["Shortname"])} ${Math.max(1, clampInt(parseInt(entry["Amount"], 10), 1, 100000000, 1))}`).join("\n");
  }

  function parseShopPackagesLines(value) {
    const rawLines = String(value || "").split(/\r?\n/g);
    const packages = {};
    for (const rawLine of rawLines) {
      const line = String(rawLine || "").trim();
      if (!line || line.startsWith("#")) continue;
      const parts = line.split("|").map((x) => String(x || "").trim());
      if (parts.length < 6) continue;
      const key = normalizeRankKey(parts[0]);
      if (!key) continue;
      packages[key] = {
        "Display name": parts[1] || key.toUpperCase(),
        "Rank": normalizeRankKey(parts[2] || "vip") || "vip",
        "Days (0 = permanent)": Math.max(0, clampInt(parseInt(parts[3], 10), 0, 3650, 30)),
        "Economics price": Math.max(0, clampFloat(parseFloat(parts[4]), 0, 1000000000, 0)),
        "ServerRewards price": Math.max(0, clampInt(parseInt(parts[5], 10), 0, 1000000000, 0))
      };
    }
    return packages;
  }

  function formatShopPackagesLines(packages) {
    const source = normalizeShopPackages(packages);
    const keys = Object.keys(source).sort((a, b) => a.localeCompare(b));
    return keys.map((key) => {
      const pkg = source[key];
      return [
        key,
        String(pkg["Display name"] || key.toUpperCase()),
        normalizeRankKey(pkg["Rank"] || "vip") || "vip",
        String(Math.max(0, clampInt(parseInt(pkg["Days (0 = permanent)"], 10), 0, 3650, 30))),
        String(Math.max(0, clampFloat(parseFloat(pkg["Economics price"]), 0, 1000000000, 0))),
        String(Math.max(0, clampInt(parseInt(pkg["ServerRewards price"], 10), 0, 1000000000, 0)))
      ].join("|");
    }).join("\n");
  }

  function normalizeRankKey(value) {
    return value ? String(value).trim().toLowerCase() : "";
  }

  function normalizeShortname(value) {
    return value ? String(value).trim().toLowerCase() : "";
  }

  function normalizePermission(value) {
    return value ? String(value).trim() : "";
  }

  function clampInt(value, min, max, fallback) {
    if (!Number.isFinite(value)) return fallback;
    if (value < min) return min;
    if (value > max) return max;
    return Math.trunc(value);
  }

  function clampFloat(value, min, max, fallback) {
    if (!Number.isFinite(value)) return fallback;
    if (value < min) return min;
    if (value > max) return max;
    return value;
  }

  function formatFloat(value) {
    return Number(value).toFixed(2).replace(/\.?0+$/, "");
  }

  function readJsonFile(file) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        try {
          resolve(JSON.parse(String(reader.result || "")));
        } catch (err) {
          reject(err);
        }
      };
      reader.onerror = () => reject(reader.error || new Error("Не удалось прочитать файл"));
      reader.readAsText(file);
    });
  }
})();
