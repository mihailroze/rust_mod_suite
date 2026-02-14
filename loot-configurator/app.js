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
    selectedRuleKey: null
  };
  const SERVER_SAVE_ENDPOINT = "/api/save-config";
  const SERVER_DEPLOY_ENDPOINT = "/api/deploy-plugin";
  const CONTAINER_ICON_BASE = "./assets/container-icons/";

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
    pluginEnabled: document.getElementById("pluginEnabled"),
    useVanillaNoRule: document.getElementById("useVanillaNoRule"),
    debugLog: document.getElementById("debugLog"),
    catalogInfo: document.getElementById("catalogInfo"),
    ruleSearch: document.getElementById("ruleSearch"),
    newRuleKey: document.getElementById("newRuleKey"),
    addRuleBtn: document.getElementById("addRuleBtn"),
    addAllRulesBtn: document.getElementById("addAllRulesBtn"),
    rulesList: document.getElementById("rulesList"),
    noRuleState: document.getElementById("noRuleState"),
    ruleEditor: document.getElementById("ruleEditor"),
    ruleKeyInput: document.getElementById("ruleKeyInput"),
    renameRuleBtn: document.getElementById("renameRuleBtn"),
    deleteRuleBtn: document.getElementById("deleteRuleBtn"),
    ruleEnabled: document.getElementById("ruleEnabled"),
    ruleOverride: document.getElementById("ruleOverride"),
    ruleDuplicates: document.getElementById("ruleDuplicates"),
    ruleForceOne: document.getElementById("ruleForceOne"),
    ruleMinRolls: document.getElementById("ruleMinRolls"),
    ruleMaxRolls: document.getElementById("ruleMaxRolls"),
    ruleMaxStacks: document.getElementById("ruleMaxStacks"),
    itemsBody: document.getElementById("itemsBody"),
    addItemShortname: document.getElementById("addItemShortname"),
    addItemMin: document.getElementById("addItemMin"),
    addItemMax: document.getElementById("addItemMax"),
    addItemChance: document.getElementById("addItemChance"),
    addItemWeight: document.getElementById("addItemWeight"),
    addItemSkin: document.getElementById("addItemSkin"),
    addItemBtn: document.getElementById("addItemBtn"),
    addItemNameHint: document.getElementById("addItemNameHint"),
    itemHint: document.getElementById("itemHint"),
    outputJson: document.getElementById("outputJson"),
    itemShortnamesList: document.getElementById("itemShortnamesList"),
    containerKeysList: document.getElementById("containerKeysList")
  };

  rebuildItemLookup();
  ensureAllContainerRulesPresent();
  bindEvents();
  renderAll();

  function createDefaultRule() {
    return {
      "Enabled": false,
      "Override default loot": false,
      "Min rolls": 2,
      "Max rolls": 4,
      "Allow duplicate rolls": true,
      "Force at least one item": true,
      "Max stacks in container (0 = unlimited)": 0,
      "Items": []
    };
  }

  function normalizeRule(source, options) {
    const rule = createDefaultRule();
    const opts = options || {};
    if (!source || typeof source !== "object") {
      return rule;
    }

    if (typeof source["Enabled"] === "boolean") {
      rule["Enabled"] = source["Enabled"];
    } else if (typeof opts.defaultEnabled === "boolean") {
      rule["Enabled"] = opts.defaultEnabled;
    }

    if (typeof source["Override default loot"] === "boolean") {
      rule["Override default loot"] = source["Override default loot"];
    } else if (typeof opts.defaultOverride === "boolean") {
      rule["Override default loot"] = opts.defaultOverride;
    }

    rule["Min rolls"] = clampInt(parseInt(source["Min rolls"], 10), 0, 64, 2);
    rule["Max rolls"] = clampInt(parseInt(source["Max rolls"], 10), 0, 64, 4);
    if (rule["Max rolls"] < rule["Min rolls"]) rule["Max rolls"] = rule["Min rolls"];

    if (typeof source["Allow duplicate rolls"] === "boolean") {
      rule["Allow duplicate rolls"] = source["Allow duplicate rolls"];
    }
    if (typeof source["Force at least one item"] === "boolean") {
      rule["Force at least one item"] = source["Force at least one item"];
    }

    rule["Max stacks in container (0 = unlimited)"] = Math.max(
      0,
      clampInt(parseInt(source["Max stacks in container (0 = unlimited)"], 10), 0, 99999, 0)
    );

    const items = Array.isArray(source["Items"]) ? source["Items"] : [];
    rule["Items"] = items
      .map((item) => ({
        "Shortname": normalizeKey(item["Shortname"]),
        "Min amount": Math.max(1, clampInt(parseInt(item["Min amount"], 10), 1, 100000, 1)),
        "Max amount": Math.max(1, clampInt(parseInt(item["Max amount"], 10), 1, 100000, 1)),
        "Chance (0-1)": clampFloat(parseFloat(item["Chance (0-1)"]), 0, 1, 0.5),
        "Weight": Math.max(0.01, clampFloat(parseFloat(item["Weight"]), 0.01, 100000, 1)),
        "Skin": Math.max(0, clampInt(parseInt(item["Skin"], 10), 0, Number.MAX_SAFE_INTEGER, 0))
      }))
      .filter((item) => !!item["Shortname"])
      .map((item) => {
        if (item["Max amount"] < item["Min amount"]) {
          item["Max amount"] = item["Min amount"];
        }
        return item;
      });

    return rule;
  }

  function cloneRule(rule) {
    return normalizeRule(rule, {
      defaultEnabled: !!(rule && rule["Enabled"]),
      defaultOverride: !!(rule && rule["Override default loot"])
    });
  }

  function createRuleForKey(key) {
    const normalized = normalizeKey(key);
    if (!normalized) return createDefaultRule();

    const observed = state.catalog && state.catalog.observedRules
      ? state.catalog.observedRules[normalized]
      : null;
    if (observed) {
      return cloneRule(observed);
    }

    return createDefaultRule();
  }

  function createDefaultConfig() {
    return {
      "Plugin enabled": true,
      "Use vanilla loot if no matching rule": true,
      "Debug log": false,
      "Rules by container key": {}
    };
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
    els.pluginEnabled.addEventListener("change", syncGlobalInputsToState);
    els.useVanillaNoRule.addEventListener("change", syncGlobalInputsToState);
    els.debugLog.addEventListener("change", syncGlobalInputsToState);
    els.ruleSearch.addEventListener("input", renderRulesList);
    els.addRuleBtn.addEventListener("click", onAddRuleClicked);
    if (els.addAllRulesBtn) {
      els.addAllRulesBtn.addEventListener("click", onAddAllRulesClicked);
    }
    els.renameRuleBtn.addEventListener("click", onRenameRuleClicked);
    els.deleteRuleBtn.addEventListener("click", onDeleteRuleClicked);
    els.ruleEnabled.addEventListener("change", onRuleSettingsChanged);
    els.ruleOverride.addEventListener("change", onRuleSettingsChanged);
    els.ruleDuplicates.addEventListener("change", onRuleSettingsChanged);
    els.ruleForceOne.addEventListener("change", onRuleSettingsChanged);
    els.ruleMinRolls.addEventListener("input", onRuleSettingsChanged);
    els.ruleMaxRolls.addEventListener("input", onRuleSettingsChanged);
    els.ruleMaxStacks.addEventListener("input", onRuleSettingsChanged);
    els.addItemShortname.addEventListener("input", onAddItemShortnameChanged);
    els.addItemShortname.addEventListener("change", onAddItemShortnameChanged);
    els.addItemBtn.addEventListener("click", onAddItemClicked);
  }

  function onConfigFileChosen(event) {
    const file = event.target.files && event.target.files[0];
    if (!file) return;
    readJsonFile(file)
      .then((json) => {
        state.config = normalizeConfig(json);
        ensureAllContainerRulesPresent();
        const keys = getRuleKeys();
        state.selectedRuleKey = keys.length > 0 ? keys[0] : null;
        renderAll();
      })
      .catch((err) => {
        alert("Не удалось загрузить конфиг: " + err.message);
      })
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
        ensureAllContainerRulesPresent();
        renderCatalogInfo();
        rebuildItemAndContainerDatalists();
        renderRulesList();
        renderRuleEditor();
        renderOutput();
      })
      .catch((err) => {
        alert("Не удалось загрузить каталог: " + err.message);
      })
      .finally(() => {
        els.catalogFileInput.value = "";
      });
  }

  function onResetClicked() {
    if (!confirm("Сбросить конфигуратор к значениям по умолчанию?")) return;
    state.config = createDefaultConfig();
    state.catalog = EMBEDDED_CATALOG;
    rebuildItemLookup();
    ensureAllContainerRulesPresent();
    state.selectedRuleKey = null;
    renderAll();
  }

  async function onCopyClicked() {
    const jsonText = JSON.stringify(state.config, null, 2);
    try {
      await navigator.clipboard.writeText(jsonText);
      alert("JSON скопирован.");
    } catch (_err) {
      alert("Буфер обмена недоступен. Используй кнопку 'Скачать JSON'.");
    }
  }

  function onDownloadClicked() {
    const blob = new Blob([JSON.stringify(state.config, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "ContainerLootManager.json";
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
          config_type: "loot",
          config: state.config
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

      const targetPath = String(payload.path || "C:\\rust\\server\\oxide\\config\\ContainerLootManager.json");
      setSaveServerStatus("Сохранено на сервер", false);
      alert(`Конфиг сохранен:\n${targetPath}\n\nВыполни на сервере: oxide.reload ContainerLootManager`);
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
          plugin: "loot"
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

      const targetPath = String(payload.path || "C:\\rust\\server\\oxide\\plugins\\ContainerLootManager.cs");
      setDeployPluginStatus("Плагин развернут", false);
      alert(`Плагин развернут:\n${targetPath}\n\nВыполни на сервере: oxide.reload ContainerLootManager`);
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

  function syncGlobalInputsToState() {
    state.config["Plugin enabled"] = !!els.pluginEnabled.checked;
    state.config["Use vanilla loot if no matching rule"] = !!els.useVanillaNoRule.checked;
    state.config["Debug log"] = !!els.debugLog.checked;
    renderOutput();
  }

  function onAddRuleClicked() {
    const key = normalizeKey(els.newRuleKey.value);
    if (!key) return;
    if (!state.config["Rules by container key"][key]) {
      state.config["Rules by container key"][key] = createRuleForKey(key);
    }
    state.selectedRuleKey = key;
    els.newRuleKey.value = "";
    renderAll();
  }

  function onAddAllRulesClicked() {
    if (!state.catalog || !Array.isArray(state.catalog.containers) || state.catalog.containers.length === 0) {
      alert("Каталог контейнеров пуст. Сначала загрузи каталог.");
      return;
    }

    const unique = new Set();
    for (const c of state.catalog.containers) {
      const key = normalizeKey(c.shortPrefabName);
      if (key) unique.add(key);
    }
    if (state.catalog.observedRules && typeof state.catalog.observedRules === "object") {
      for (const key of Object.keys(state.catalog.observedRules)) {
        const normalized = normalizeKey(key);
        if (normalized) unique.add(normalized);
      }
    }

    let created = 0;
    for (const key of unique) {
      if (!state.config["Rules by container key"][key]) {
        state.config["Rules by container key"][key] = createRuleForKey(key);
        created++;
      }
    }

    if (!state.selectedRuleKey && unique.size > 0) {
      state.selectedRuleKey = Array.from(unique).sort()[0];
    }

    renderAll();
    alert(`Добавлено правил: ${created}. Всего ключей контейнеров: ${unique.size}.`);
  }

  function ensureAllContainerRulesPresent() {
    if (!state.config || !state.config["Rules by container key"]) return;
    if (!state.catalog) return;

    if (Array.isArray(state.catalog.containers)) {
      for (const c of state.catalog.containers) {
        const key = normalizeKey(c.shortPrefabName);
        if (!key) continue;
        if (!state.config["Rules by container key"][key]) {
          state.config["Rules by container key"][key] = createRuleForKey(key);
        }
      }
    }

    if (state.catalog.observedRules && typeof state.catalog.observedRules === "object") {
      for (const key of Object.keys(state.catalog.observedRules)) {
        if (!state.config["Rules by container key"][key]) {
          state.config["Rules by container key"][key] = createRuleForKey(key);
        }
      }
    }
  }

  function onRenameRuleClicked() {
    const oldKey = state.selectedRuleKey;
    if (!oldKey) return;
    const newKey = normalizeKey(els.ruleKeyInput.value);
    if (!newKey) return;
    if (newKey === oldKey) return;
    if (state.config["Rules by container key"][newKey]) {
      alert("Правило с таким ключом уже существует.");
      return;
    }
    state.config["Rules by container key"][newKey] = state.config["Rules by container key"][oldKey];
    delete state.config["Rules by container key"][oldKey];
    state.selectedRuleKey = newKey;
    renderAll();
  }

  function onDeleteRuleClicked() {
    const key = state.selectedRuleKey;
    if (!key) return;
    if (!confirm("Удалить выбранное правило?")) return;
    delete state.config["Rules by container key"][key];
    const keys = getRuleKeys();
    state.selectedRuleKey = keys.length > 0 ? keys[0] : null;
    renderAll();
  }

  function onRuleSettingsChanged() {
    const rule = getSelectedRule();
    if (!rule) return;

    rule["Enabled"] = !!els.ruleEnabled.checked;
    rule["Override default loot"] = !!els.ruleOverride.checked;
    rule["Allow duplicate rolls"] = !!els.ruleDuplicates.checked;
    rule["Force at least one item"] = !!els.ruleForceOne.checked;
    rule["Min rolls"] = clampInt(parseInt(els.ruleMinRolls.value, 10), 0, 64, 2);
    rule["Max rolls"] = clampInt(parseInt(els.ruleMaxRolls.value, 10), 0, 64, 4);
    if (rule["Max rolls"] < rule["Min rolls"]) {
      rule["Max rolls"] = rule["Min rolls"];
      els.ruleMaxRolls.value = String(rule["Max rolls"]);
    }
    rule["Max stacks in container (0 = unlimited)"] = Math.max(0, clampInt(parseInt(els.ruleMaxStacks.value, 10), 0, 999, 0));
    renderOutput();
  }

  function onAddItemShortnameChanged() {
    renderAddItemNameHint();
  }

  function onAddItemClicked() {
    const rule = getSelectedRule();
    if (!rule) return;

    const shortname = normalizeKey(els.addItemShortname.value);
    if (!shortname) return;
    const min = Math.max(1, clampInt(parseInt(els.addItemMin.value, 10), 1, 100000, 1));
    const max = Math.max(min, clampInt(parseInt(els.addItemMax.value, 10), min, 100000, min));
    let chance = parseFloat(els.addItemChance.value);
    if (!Number.isFinite(chance)) chance = 0.5;
    if (chance > 1) chance = chance / 100;
    chance = clampFloat(chance, 0, 1, 0.5);
    const weight = Math.max(0.01, clampFloat(parseFloat(els.addItemWeight.value), 0.01, 100000, 1));
    const skin = Math.max(0, clampInt(parseInt(els.addItemSkin.value, 10), 0, Number.MAX_SAFE_INTEGER, 0));

    rule["Items"].push({
      "Shortname": shortname,
      "Min amount": min,
      "Max amount": max,
      "Chance (0-1)": chance,
      "Weight": weight,
      "Skin": skin
    });

    els.itemHint.textContent = "";
    renderAddItemNameHint();
    renderRuleEditor();
    renderOutput();
  }

  function onItemCellChanged(index, field, value) {
    const rule = getSelectedRule();
    if (!rule || !rule["Items"][index]) return;
    const item = rule["Items"][index];

    switch (field) {
      case "Shortname":
        item["Shortname"] = normalizeKey(value) || item["Shortname"];
        break;
      case "Min amount":
        item["Min amount"] = Math.max(1, clampInt(parseInt(value, 10), 1, 100000, item["Min amount"]));
        if (item["Max amount"] < item["Min amount"]) item["Max amount"] = item["Min amount"];
        break;
      case "Max amount":
        item["Max amount"] = Math.max(1, clampInt(parseInt(value, 10), 1, 100000, item["Max amount"]));
        if (item["Max amount"] < item["Min amount"]) item["Min amount"] = item["Max amount"];
        break;
      case "Chance (0-1)": {
        let chance = parseFloat(value);
        if (!Number.isFinite(chance)) chance = item["Chance (0-1)"];
        if (chance > 1) chance = chance / 100;
        item["Chance (0-1)"] = clampFloat(chance, 0, 1, item["Chance (0-1)"]);
        break;
      }
      case "Weight":
        item["Weight"] = Math.max(0.01, clampFloat(parseFloat(value), 0.01, 100000, item["Weight"]));
        break;
      case "Skin":
        item["Skin"] = Math.max(0, clampInt(parseInt(value, 10), 0, Number.MAX_SAFE_INTEGER, item["Skin"]));
        break;
      default:
        return;
    }

    renderRuleEditor();
    renderOutput();
  }

  function removeItem(index) {
    const rule = getSelectedRule();
    if (!rule) return;
    rule["Items"].splice(index, 1);
    renderRuleEditor();
    renderOutput();
  }

  function rebuildItemLookup() {
    const lookup = Object.create(null);
    if (state.catalog && Array.isArray(state.catalog.items)) {
      for (const item of state.catalog.items) {
        const key = normalizeKey(item.shortname);
        if (!key) continue;
        lookup[key] = item;
      }
    }
    state.itemByShortname = lookup;
  }

  function getItemMeta(shortname) {
    const key = normalizeKey(shortname);
    if (!key) return null;
    return state.itemByShortname[key] || null;
  }

  function getItemLabel(shortname) {
    const key = normalizeKey(shortname);
    if (!key) return "";

    const item = getItemMeta(key);
    const en = item && item.displayName ? String(item.displayName) : key;
    const ru = item && item.displayNameRu ? String(item.displayNameRu) : "";
    if (ru && normalizeKey(ru) !== normalizeKey(en)) {
      return `${ru} / ${en}`;
    }
    return ru || en;
  }

  function getItemOptionLabel(item) {
    const label = getItemLabel(item.shortname);
    if (!item.category) return label;
    return `${label} (${item.category})`;
  }

  function renderAddItemNameHint() {
    if (!els.addItemNameHint) return;
    const shortname = normalizeKey(els.addItemShortname.value);
    if (!shortname) {
      els.addItemNameHint.textContent = "";
      return;
    }

    const item = getItemMeta(shortname);
    if (!item) {
      els.addItemNameHint.textContent = `Неизвестный shortname: ${shortname}`;
      return;
    }

    const categoryPart = item.category ? ` (${item.category})` : "";
    els.addItemNameHint.textContent = `${getItemLabel(shortname)}${categoryPart}`;
  }

  function renderAll() {
    if (!state.selectedRuleKey) {
      const keys = getRuleKeys();
      if (keys.length > 0) {
        state.selectedRuleKey = keys[0];
      }
    }

    renderGlobals();
    renderCatalogInfo();
    rebuildItemAndContainerDatalists();
    renderAddItemNameHint();
    renderRulesList();
    renderRuleEditor();
    renderOutput();
  }

  function renderGlobals() {
    els.pluginEnabled.checked = !!state.config["Plugin enabled"];
    els.useVanillaNoRule.checked = !!state.config["Use vanilla loot if no matching rule"];
    els.debugLog.checked = !!state.config["Debug log"];
  }

  function renderCatalogInfo() {
    const items = state.catalog.items.length;
    const containers = state.catalog.containers.length;
    if (items === 0 && containers === 0) {
      els.catalogInfo.textContent = "Каталог: не загружен";
      return;
    }
    els.catalogInfo.textContent = `Каталог загружен: предметов ${items}, контейнеров ${containers}`;
  }

  function rebuildItemAndContainerDatalists() {
    els.itemShortnamesList.innerHTML = "";
    for (const item of state.catalog.items) {
      const option = document.createElement("option");
      option.value = item.shortname;
      option.label = getItemOptionLabel(item);
      option.textContent = option.label;
      els.itemShortnamesList.appendChild(option);
    }

    els.containerKeysList.innerHTML = "";
    const keys = new Set();
    for (const c of state.catalog.containers) {
      if (c.shortPrefabName) keys.add(c.shortPrefabName);
    }
    for (const key of Array.from(keys).sort()) {
      const option = document.createElement("option");
      option.value = key;
      els.containerKeysList.appendChild(option);
    }
  }

  function renderRulesList() {
    const search = normalizeKey(els.ruleSearch.value);
    const keys = getRuleKeys().filter((k) => !search || k.includes(search));
    els.rulesList.innerHTML = "";

    for (const key of keys) {
      const btn = document.createElement("button");
      btn.className = "rule-btn" + (key === state.selectedRuleKey ? " selected" : "");
      const rule = state.config["Rules by container key"][key];
      const itemCount = Array.isArray(rule["Items"]) ? rule["Items"].length : 0;
      const enabled = rule["Enabled"] ? "ВКЛ" : "ВЫКЛ";
      const sampleCount = state.catalog && state.catalog.observedSampleCount
        ? (state.catalog.observedSampleCount[key] || 0)
        : 0;
      btn.title = key;

      const row = document.createElement("div");
      row.className = "rule-btn-row";

      const icon = document.createElement("img");
      icon.className = "rule-icon";
      icon.alt = key;
      icon.loading = "lazy";
      icon.decoding = "async";
      icon.src = CONTAINER_ICON_BASE + getContainerIconFile(key);
      icon.addEventListener("error", () => {
        if (icon.dataset.fallbackApplied === "1") return;
        icon.dataset.fallbackApplied = "1";
        icon.src = CONTAINER_ICON_BASE + "crate.png";
      });

      const label = document.createElement("span");
      label.className = "rule-btn-label";
      label.textContent = `${key} | ${enabled} | предметов: ${itemCount} | образцов: ${sampleCount}`;

      row.appendChild(icon);
      row.appendChild(label);
      btn.appendChild(row);
      btn.addEventListener("click", () => {
        state.selectedRuleKey = key;
        renderRuleEditor();
        renderRulesList();
      });
      els.rulesList.appendChild(btn);
    }
  }

  function getContainerIconFile(containerKey) {
    const key = normalizeKey(containerKey);
    if (!key) return "crate.png";

    if (key.includes("codelockedhackablecrate")) return "bradley-crate.png";
    if (key.includes("supply_drop")) return "supply-drop.png";
    if (key.includes("oil_barrel")) return "oil-barrel.png";
    if (key.includes("loot-barrel") || key.includes("loot_barrel")) return "barrel.png";
    if (key.includes("roadsign")) return "roadsign.png";
    if (key.includes("vehicle_parts")) return "vehicle-parts.png";

    if (key === "foodbox") return "foodbox.png";
    if (key.includes("crate_food")) return "food-crate.png";
    if (key.includes("crate_medical")) return "medical-crate.png";
    if (key.includes("crate_tools")) return "tool-crate.png";
    if (key.includes("crate_cannons") || key.includes("crate_ammunition")) return "military-crate.png";
    if (key.includes("crate_elite")) return "elite-crate.png";

    if (key.includes("crate")) return "crate.png";
    if (key.includes("barrel")) return "barrel.png";
    return "crate.png";
  }

  function renderRuleEditor() {
    const rule = getSelectedRule();
    if (!rule) {
      els.noRuleState.classList.remove("hidden");
      els.ruleEditor.classList.add("hidden");
      return;
    }

    els.noRuleState.classList.add("hidden");
    els.ruleEditor.classList.remove("hidden");

    els.ruleKeyInput.value = state.selectedRuleKey;
    els.ruleEnabled.checked = !!rule["Enabled"];
    els.ruleOverride.checked = !!rule["Override default loot"];
    els.ruleDuplicates.checked = !!rule["Allow duplicate rolls"];
    els.ruleForceOne.checked = !!rule["Force at least one item"];
    els.ruleMinRolls.value = String(rule["Min rolls"]);
    els.ruleMaxRolls.value = String(rule["Max rolls"]);
    els.ruleMaxStacks.value = String(rule["Max stacks in container (0 = unlimited)"]);

    els.itemsBody.innerHTML = "";
    rule["Items"].forEach((item, index) => {
      const tr = document.createElement("tr");
      tr.appendChild(createCellInput(item["Shortname"], (v) => onItemCellChanged(index, "Shortname", v), "text", "itemShortnamesList"));
      tr.appendChild(createNameCell(item["Shortname"]));
      tr.appendChild(createCellInput(item["Min amount"], (v) => onItemCellChanged(index, "Min amount", v), "number"));
      tr.appendChild(createCellInput(item["Max amount"], (v) => onItemCellChanged(index, "Max amount", v), "number"));
      tr.appendChild(createCellInput(item["Chance (0-1)"], (v) => onItemCellChanged(index, "Chance (0-1)", v), "number", null, "0.01"));
      tr.appendChild(createCellInput(item["Weight"], (v) => onItemCellChanged(index, "Weight", v), "number", null, "0.01"));
      tr.appendChild(createCellInput(item["Skin"], (v) => onItemCellChanged(index, "Skin", v), "number"));

      const tdActions = document.createElement("td");
      const delBtn = document.createElement("button");
      delBtn.className = "btn danger small";
      delBtn.textContent = "X";
      delBtn.addEventListener("click", () => removeItem(index));
      tdActions.appendChild(delBtn);
      tr.appendChild(tdActions);
      els.itemsBody.appendChild(tr);
    });
  }

  function createCellInput(value, onChange, type, listId, step) {
    const td = document.createElement("td");
    const input = document.createElement("input");
    input.type = type || "text";
    input.value = String(value);
    input.className = "cell-input";
    if (listId) input.setAttribute("list", listId);
    if (step) input.step = step;
    input.addEventListener("change", () => onChange(input.value));
    td.appendChild(input);
    return td;
  }

  function createNameCell(shortname) {
    const td = document.createElement("td");
    td.className = "item-name-cell";
    td.textContent = getItemLabel(shortname);
    return td;
  }

  function renderOutput() {
    els.outputJson.value = JSON.stringify(state.config, null, 2);
  }

  function getRuleKeys() {
    return Object.keys(state.config["Rules by container key"]).sort((a, b) => a.localeCompare(b));
  }

  function getSelectedRule() {
    if (!state.selectedRuleKey) return null;
    return state.config["Rules by container key"][state.selectedRuleKey] || null;
  }

  function normalizeConfig(raw) {
    const cfg = createDefaultConfig();
    if (!raw || typeof raw !== "object") return cfg;

    cfg["Plugin enabled"] = !!raw["Plugin enabled"];
    cfg["Use vanilla loot if no matching rule"] = !!raw["Use vanilla loot if no matching rule"];
    cfg["Debug log"] = !!raw["Debug log"];

    const rules = raw["Rules by container key"];
    if (rules && typeof rules === "object") {
      for (const rawKey of Object.keys(rules)) {
        const key = normalizeKey(rawKey);
        if (!key) continue;
        cfg["Rules by container key"][key] = normalizeRule(rules[rawKey], null);
      }
    }

    return cfg;
  }

  function normalizeCatalog(raw) {
    const catalog = { items: [], containers: [], observedRules: {}, observedSampleCount: {} };
    if (!raw || typeof raw !== "object") return catalog;

    const items = Array.isArray(raw["Items"]) ? raw["Items"] : [];
    for (const item of items) {
      const shortname = normalizeKey(item["Shortname"]);
      if (!shortname) continue;
      const displayName = String(item["Display name"] || shortname);
      const mappedRu = DEFAULT_RU_ITEM_NAMES[shortname] ? String(DEFAULT_RU_ITEM_NAMES[shortname]) : "";
      const displayNameRu = String(item["Display name ru"] || item["Display name RU"] || mappedRu || "").trim();
      catalog.items.push({
        shortname,
        displayName,
        displayNameRu,
        category: String(item["Category"] || ""),
        stackSize: clampInt(parseInt(item["Stack size"], 10), 0, 100000, 0)
      });
    }

    const containers = Array.isArray(raw["Containers"]) ? raw["Containers"] : [];
    for (const c of containers) {
      catalog.containers.push({
        shortPrefabName: normalizeKey(c["Short prefab name"]),
        prefabName: String(c["Prefab name"] || "")
      });
    }

    const observedRules = raw["Observed rules by container key"];
    if (observedRules && typeof observedRules === "object") {
      for (const rawKey of Object.keys(observedRules)) {
        const key = normalizeKey(rawKey);
        if (!key) continue;
        catalog.observedRules[key] = normalizeRule(observedRules[rawKey], {
          defaultEnabled: true,
          defaultOverride: true
        });
      }
    }

    const observedCounts = raw["Observed sample count by container key"];
    if (observedCounts && typeof observedCounts === "object") {
      for (const rawKey of Object.keys(observedCounts)) {
        const key = normalizeKey(rawKey);
        if (!key) continue;
        catalog.observedSampleCount[key] = clampInt(parseInt(observedCounts[rawKey], 10), 0, 100000, 0);
      }
    }

    catalog.items.sort((a, b) => a.shortname.localeCompare(b.shortname));
    catalog.containers.sort((a, b) => a.shortPrefabName.localeCompare(b.shortPrefabName));
    return catalog;
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

  function normalizeKey(value) {
    if (!value) return "";
    return String(value).trim().toLowerCase();
  }

  function readJsonFile(file) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        try {
          const json = JSON.parse(String(reader.result || ""));
          resolve(json);
        } catch (err) {
          reject(err);
        }
      };
      reader.onerror = () => reject(reader.error || new Error("Не удалось прочитать файл"));
      reader.readAsText(file);
    });
  }
})();
