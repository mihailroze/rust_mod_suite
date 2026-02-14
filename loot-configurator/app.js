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
    selectedRuleKey: null,
    librarySearch: "",
    libraryCategory: "all",
    dragPayload: null
  };
  const SERVER_SAVE_ENDPOINT = "/api/save-config";
  const SERVER_DEPLOY_ENDPOINT = "/api/deploy-plugin";
  const CONTAINER_ICON_BASE = "./assets/container-icons/";
  const ITEM_ICON_BASE = "https://cdn.rusthelp.com/images/public/";

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
    lootPatternPreset: document.getElementById("lootPatternPreset"),
    lootPatternPower: document.getElementById("lootPatternPower"),
    applyLootPatternBtn: document.getElementById("applyLootPatternBtn"),
    lootPatternStatus: document.getElementById("lootPatternStatus"),
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
    containerItemsMeta: document.getElementById("containerItemsMeta"),
    containerItemsDrop: document.getElementById("containerItemsDrop"),
    containerItemsList: document.getElementById("containerItemsList"),
    containerTrashDrop: document.getElementById("containerTrashDrop"),
    librarySearch: document.getElementById("librarySearch"),
    libraryCategory: document.getElementById("libraryCategory"),
    libraryCategoryBadges: document.getElementById("libraryCategoryBadges"),
    libraryItemsGrid: document.getElementById("libraryItemsGrid"),
    libraryItemsCount: document.getElementById("libraryItemsCount"),
    addItemShortname: document.getElementById("addItemShortname"),
    addItemMin: document.getElementById("addItemMin"),
    addItemMax: document.getElementById("addItemMax"),
    addItemChance: document.getElementById("addItemChance"),
    addItemWeight: document.getElementById("addItemWeight"),
    addItemSkin: document.getElementById("addItemSkin"),
    addItemBtn: document.getElementById("addItemBtn"),
    addItemNameHint: document.getElementById("addItemNameHint"),
    addItemSuggestions: document.getElementById("addItemSuggestions"),
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
    if (els.applyLootPatternBtn) {
      els.applyLootPatternBtn.addEventListener("click", onApplyLootPatternClicked);
    }
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
    if (els.librarySearch) {
      els.librarySearch.addEventListener("input", onLibrarySearchChanged);
    }
    if (els.libraryCategory) {
      els.libraryCategory.addEventListener("change", onLibraryCategoryChanged);
    }
    els.addItemShortname.addEventListener("input", onAddItemShortnameChanged);
    els.addItemShortname.addEventListener("change", onAddItemShortnameChanged);
    els.addItemBtn.addEventListener("click", onAddItemClicked);

    const quickAddInputs = [
      els.addItemShortname,
      els.addItemMin,
      els.addItemMax,
      els.addItemChance,
      els.addItemWeight,
      els.addItemSkin
    ];
    for (const input of quickAddInputs) {
      input.addEventListener("keydown", onQuickAddKeyDown);
    }

    if (els.containerItemsDrop) {
      els.containerItemsDrop.addEventListener("dragover", onContainerDropZoneDragOver);
      els.containerItemsDrop.addEventListener("dragleave", () => {
        els.containerItemsDrop.classList.remove("drag-active");
      });
      els.containerItemsDrop.addEventListener("drop", onContainerDropZoneDrop);
    }

    if (els.containerTrashDrop) {
      els.containerTrashDrop.addEventListener("dragover", onTrashDropZoneDragOver);
      els.containerTrashDrop.addEventListener("dragleave", () => {
        els.containerTrashDrop.classList.remove("drag-active");
      });
      els.containerTrashDrop.addEventListener("drop", onTrashDropZoneDrop);
    }
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
        renderAll();
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

  function setLootPatternStatus(text, isError) {
    if (!els.lootPatternStatus) return;
    els.lootPatternStatus.textContent = String(text || "");
    els.lootPatternStatus.style.color = isError ? "#ff7b72" : "#a8b3c2";
  }

  function parsePatternPower() {
    return clampFloat(parseFloat(els.lootPatternPower.value), 0.5, 3, 1);
  }

  function onApplyLootPatternClicked() {
    const rules = state.config["Rules by container key"] || {};
    const keys = Object.keys(rules);
    if (keys.length === 0) {
      setLootPatternStatus("Нет правил для применения паттерна.", true);
      return;
    }

    const preset = String(els.lootPatternPreset.value || "improved_medium");
    const power = parsePatternPower();
    els.lootPatternPower.value = power.toFixed(1);

    let touchedRules = 0;
    let changedItems = 0;
    let skippedEmpty = 0;

    for (const key of keys) {
      const rule = rules[key];
      if (!rule || !Array.isArray(rule["Items"]) || rule["Items"].length === 0) {
        skippedEmpty += 1;
        continue;
      }

      const profile = buildLootPatternProfile(key, preset, power);
      applyLootPatternToRule(rule, profile);
      touchedRules += 1;
      changedItems += rule["Items"].length;
    }

    renderAll();
    setLootPatternStatus(
      `Паттерн применен: правил ${touchedRules}, предметов ${changedItems}, пропущено пустых правил ${skippedEmpty}.`,
      false
    );
  }

  function getLootPatternBaseProfile(preset) {
    switch (preset) {
      case "improved_light":
        return {
          amountMultiplier: 1.15,
          chanceBonus: 0.05,
          weightMultiplier: 1.07,
          rollBonus: 1,
          maxStacksBonus: 1,
          qualityBias: 0.8
        };
      case "improved_aggressive":
        return {
          amountMultiplier: 1.60,
          chanceBonus: 0.13,
          weightMultiplier: 1.18,
          rollBonus: 2,
          maxStacksBonus: 3,
          qualityBias: 1.35
        };
      case "improved_medium":
      default:
        return {
          amountMultiplier: 1.35,
          chanceBonus: 0.09,
          weightMultiplier: 1.12,
          rollBonus: 1,
          maxStacksBonus: 2,
          qualityBias: 1.0
        };
    }
  }

  function getContainerTier(containerKey) {
    const key = normalizeKey(containerKey);
    if (!key) return "mid";

    if (key.includes("codelockedhackablecrate") || key.includes("crate_elite") || key.includes("bradley")) return "elite";
    if (key.includes("supply_drop") || key.includes("crate_underwater") || key.includes("underwater") || key.includes("vehicle_parts")) return "high";
    if (key.includes("loot-barrel") || key.includes("loot_barrel") || key.includes("oil_barrel") || key.includes("trash")) return "low";
    return "mid";
  }

  function buildLootPatternProfile(containerKey, preset, power) {
    const base = getLootPatternBaseProfile(preset);
    const scaled = {
      amountMultiplier: 1 + (base.amountMultiplier - 1) * power,
      chanceBonus: base.chanceBonus * power,
      weightMultiplier: 1 + (base.weightMultiplier - 1) * power,
      rollBonus: Math.max(0, Math.round(base.rollBonus * power)),
      maxStacksBonus: Math.max(0, Math.round(base.maxStacksBonus * power)),
      qualityBias: base.qualityBias * power
    };

    const tier = getContainerTier(containerKey);
    let tierAdjust;
    switch (tier) {
      case "low":
        tierAdjust = { amount: 0.85, chance: 0.85, weight: 0.90, rolls: 0, stacks: 0 };
        break;
      case "high":
        tierAdjust = { amount: 1.15, chance: 1.10, weight: 1.05, rolls: 1, stacks: 1 };
        break;
      case "elite":
        tierAdjust = { amount: 1.30, chance: 1.20, weight: 1.10, rolls: 1, stacks: 2 };
        break;
      case "mid":
      default:
        tierAdjust = { amount: 1.00, chance: 1.00, weight: 1.00, rolls: 0, stacks: 0 };
        break;
    }

    return {
      amountMultiplier: scaled.amountMultiplier * tierAdjust.amount,
      chanceBonus: scaled.chanceBonus * tierAdjust.chance,
      weightMultiplier: 1 + (scaled.weightMultiplier - 1) * tierAdjust.weight,
      rollBonus: scaled.rollBonus + tierAdjust.rolls,
      maxStacksBonus: scaled.maxStacksBonus + tierAdjust.stacks,
      qualityBias: scaled.qualityBias * tierAdjust.amount
    };
  }

  function getItemCategoryBonus(item) {
    const meta = getItemMeta(item["Shortname"]);
    const category = normalizeKey(meta && meta.category ? meta.category : "");
    switch (category) {
      case "weapon":
        return 0.20;
      case "ammunition":
        return 0.16;
      case "component":
        return 0.14;
      case "medical":
        return 0.10;
      case "resource":
        return 0.08;
      case "food":
        return 0.05;
      default:
        return 0.04;
    }
  }

  function applyLootPatternToRule(rule, profile) {
    rule["Enabled"] = true;
    rule["Override default loot"] = true;

    rule["Min rolls"] = clampInt(parseInt(rule["Min rolls"], 10) + profile.rollBonus, 0, 64, 0);
    rule["Max rolls"] = clampInt(parseInt(rule["Max rolls"], 10) + profile.rollBonus, 0, 64, rule["Min rolls"]);
    if (rule["Max rolls"] < rule["Min rolls"]) rule["Max rolls"] = rule["Min rolls"];

    const maxStacks = Math.max(0, clampInt(parseInt(rule["Max stacks in container (0 = unlimited)"], 10), 0, 99999, 0));
    if (maxStacks > 0) {
      rule["Max stacks in container (0 = unlimited)"] = clampInt(maxStacks + profile.maxStacksBonus, 0, 99999, maxStacks);
    }

    for (const item of rule["Items"]) {
      const categoryBonus = getItemCategoryBonus(item) * profile.qualityBias;
      const amountBoost = profile.amountMultiplier * (1 + categoryBonus * 0.35);
      const chanceBoost = profile.chanceBonus * (1 + categoryBonus * 0.45);
      const weightBoost = profile.weightMultiplier * (1 + categoryBonus * 0.20);

      item["Min amount"] = Math.max(1, Math.round(item["Min amount"] * amountBoost));
      item["Max amount"] = Math.max(item["Min amount"], Math.round(item["Max amount"] * amountBoost));
      item["Chance (0-1)"] = clampFloat(item["Chance (0-1)"] + chanceBoost, 0, 1, item["Chance (0-1)"]);
      item["Weight"] = Math.max(0.01, clampFloat(item["Weight"] * weightBoost, 0.01, 100000, item["Weight"]));
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
    renderAddItemSuggestions();
  }

  function onQuickAddKeyDown(event) {
    if (event.key !== "Enter") return;
    event.preventDefault();
    onAddItemClicked();
  }

  function onAddItemClicked() {
    const shortname = normalizeKey(els.addItemShortname.value);
    if (!shortname) return;
    const itemTemplate = readAddItemTemplate();
    const added = addItemToRule(shortname, null, itemTemplate);
    if (!added) return;

    els.itemHint.textContent = "";
    els.addItemShortname.value = "";
    renderAddItemNameHint();
    renderAddItemSuggestions();
    els.addItemShortname.focus();
  }

  function readAddItemTemplate() {
    const min = Math.max(1, clampInt(parseInt(els.addItemMin.value, 10), 1, 100000, 1));
    const max = Math.max(min, clampInt(parseInt(els.addItemMax.value, 10), min, 100000, min));
    let chance = parseFloat(els.addItemChance.value);
    if (!Number.isFinite(chance)) chance = 0.5;
    if (chance > 1) chance = chance / 100;
    chance = clampFloat(chance, 0, 1, 0.5);
    const weight = Math.max(0.01, clampFloat(parseFloat(els.addItemWeight.value), 0.01, 100000, 1));
    const skin = Math.max(0, clampInt(parseInt(els.addItemSkin.value, 10), 0, Number.MAX_SAFE_INTEGER, 0));
    return {
      "Min amount": min,
      "Max amount": max,
      "Chance (0-1)": chance,
      "Weight": weight,
      "Skin": skin
    };
  }

  function createItemEntry(shortname, template) {
    const source = template || {};
    const minAmount = Math.max(1, clampInt(parseInt(source["Min amount"], 10), 1, 100000, 1));
    const maxAmount = Math.max(minAmount, clampInt(parseInt(source["Max amount"], 10), minAmount, 100000, minAmount));
    const chance = clampFloat(parseFloat(source["Chance (0-1)"]), 0, 1, 0.5);
    const weight = Math.max(0.01, clampFloat(parseFloat(source["Weight"]), 0.01, 100000, 1));
    const skin = Math.max(0, clampInt(parseInt(source["Skin"], 10), 0, Number.MAX_SAFE_INTEGER, 0));
    return {
      "Shortname": normalizeKey(shortname),
      "Min amount": minAmount,
      "Max amount": maxAmount,
      "Chance (0-1)": chance,
      "Weight": weight,
      "Skin": skin
    };
  }

  function addItemToRule(shortname, insertIndex, template) {
    const rule = getSelectedRule();
    if (!rule) return false;

    const key = normalizeKey(shortname);
    if (!key) return false;

    if (!Array.isArray(rule["Items"])) rule["Items"] = [];
    const item = createItemEntry(key, template);

    if (Number.isInteger(insertIndex)) {
      const clampedIndex = clampInt(insertIndex, 0, rule["Items"].length, rule["Items"].length);
      rule["Items"].splice(clampedIndex, 0, item);
    } else {
      rule["Items"].push(item);
    }

    renderRuleEditor();
    renderOutput();
    return true;
  }

  function moveRuleItem(fromIndex, toIndex) {
    const rule = getSelectedRule();
    if (!rule || !Array.isArray(rule["Items"])) return false;
    if (!Number.isInteger(fromIndex) || !Number.isInteger(toIndex)) return false;
    if (fromIndex < 0 || fromIndex >= rule["Items"].length) return false;

    let target = clampInt(toIndex, 0, rule["Items"].length, rule["Items"].length);
    if (fromIndex === target || fromIndex + 1 === target) return false;

    const moved = rule["Items"][fromIndex];
    rule["Items"].splice(fromIndex, 1);
    if (fromIndex < target) {
      target -= 1;
    }
    rule["Items"].splice(target, 0, moved);
    renderRuleEditor();
    renderOutput();
    return true;
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
    if (!rule || !Array.isArray(rule["Items"])) return false;
    if (!Number.isInteger(index) || index < 0 || index >= rule["Items"].length) return false;
    rule["Items"].splice(index, 1);
    renderRuleEditor();
    renderOutput();
    return true;
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

  function getItemSearchScore(item, query) {
    const q = normalizeKey(query);
    if (!q) return 0;

    const shortname = normalizeKey(item.shortname);
    const en = normalizeKey(item.displayName);
    const ru = normalizeKey(item.displayNameRu);
    const category = normalizeKey(item.category);

    if (shortname === q) return 1000;
    if (shortname.startsWith(q)) return 800;
    if (shortname.includes(q)) return 700;
    if (ru && ru.startsWith(q)) return 600;
    if (ru && ru.includes(q)) return 500;
    if (en && en.startsWith(q)) return 450;
    if (en && en.includes(q)) return 400;
    if (category && category.includes(q)) return 250;
    return 0;
  }

  function renderAddItemSuggestions() {
    if (!els.addItemSuggestions) return;
    const query = normalizeKey(els.addItemShortname.value);
    els.addItemSuggestions.innerHTML = "";
    if (!query) return;

    const matches = [];
    for (const item of state.catalog.items) {
      const score = getItemSearchScore(item, query);
      if (score <= 0) continue;
      matches.push({ item, score });
    }

    matches.sort((a, b) => {
      if (b.score !== a.score) return b.score - a.score;
      return a.item.shortname.localeCompare(b.item.shortname);
    });

    const top = matches.slice(0, 12);
    for (const entry of top) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "suggest-btn";
      btn.textContent = `${entry.item.shortname} - ${getItemLabel(entry.item.shortname)}`;
      btn.addEventListener("click", () => {
        els.addItemShortname.value = entry.item.shortname;
        renderAddItemNameHint();
        renderAddItemSuggestions();
        els.addItemShortname.focus();
      });
      els.addItemSuggestions.appendChild(btn);
    }
  }

  function onLibrarySearchChanged() {
    state.librarySearch = String(els.librarySearch.value || "");
    renderItemLibrary();
  }

  function onLibraryCategoryChanged() {
    state.libraryCategory = normalizeKey(els.libraryCategory.value) || "all";
    renderLibraryCategoryBadges();
    renderItemLibrary();
  }

  function getItemCategoryLabel(item) {
    const category = item && item.category ? String(item.category).trim() : "";
    return category || "Без категории";
  }

  function getLibraryCategoryEntries() {
    const byKey = Object.create(null);
    for (const item of state.catalog.items) {
      const label = getItemCategoryLabel(item);
      const key = normalizeKey(label);
      if (!key) continue;
      if (!byKey[key]) {
        byKey[key] = { key, label, count: 0 };
      }
      byKey[key].count += 1;
    }
    return Object.values(byKey).sort((a, b) => a.label.localeCompare(b.label, "ru"));
  }

  function renderLibraryCategoryOptions() {
    if (!els.libraryCategory) return;
    const entries = getLibraryCategoryEntries();
    const current = normalizeKey(state.libraryCategory) || "all";
    const available = new Set(entries.map((entry) => entry.key));
    if (current !== "all" && !available.has(current)) {
      state.libraryCategory = "all";
    }

    els.libraryCategory.innerHTML = "";
    const allOpt = document.createElement("option");
    allOpt.value = "all";
    allOpt.textContent = "Все категории";
    els.libraryCategory.appendChild(allOpt);
    for (const entry of entries) {
      const option = document.createElement("option");
      option.value = entry.key;
      option.textContent = `${entry.label} (${entry.count})`;
      els.libraryCategory.appendChild(option);
    }
    els.libraryCategory.value = state.libraryCategory;
  }

  function renderLibraryCategoryBadges() {
    if (!els.libraryCategoryBadges) return;
    els.libraryCategoryBadges.innerHTML = "";

    const entries = getLibraryCategoryEntries();
    const makeChip = (key, label) => {
      const chip = document.createElement("button");
      chip.type = "button";
      chip.className = "category-chip" + (state.libraryCategory === key ? " active" : "");
      chip.textContent = label;
      chip.addEventListener("click", () => {
        state.libraryCategory = key;
        if (els.libraryCategory) {
          els.libraryCategory.value = key;
        }
        renderLibraryCategoryBadges();
        renderItemLibrary();
      });
      return chip;
    };

    els.libraryCategoryBadges.appendChild(makeChip("all", "Все"));
    for (const entry of entries) {
      els.libraryCategoryBadges.appendChild(makeChip(entry.key, entry.label));
    }
  }

  function getFilteredLibraryItems() {
    const query = normalizeKey(state.librarySearch || "");
    const category = normalizeKey(state.libraryCategory || "all");
    const filtered = [];
    for (const item of state.catalog.items) {
      if (category !== "all" && normalizeKey(getItemCategoryLabel(item)) !== category) {
        continue;
      }
      const score = query ? getItemSearchScore(item, query) : 1;
      if (query && score <= 0) continue;
      filtered.push({ item, score });
    }

    filtered.sort((a, b) => {
      if (query && b.score !== a.score) return b.score - a.score;
      const catCmp = getItemCategoryLabel(a.item).localeCompare(getItemCategoryLabel(b.item), "ru");
      if (catCmp !== 0) return catCmp;
      return a.item.shortname.localeCompare(b.item.shortname);
    });

    return filtered.map((entry) => entry.item);
  }

  function renderItemLibrary() {
    if (!els.libraryItemsGrid) return;
    els.libraryItemsGrid.innerHTML = "";

    const items = getFilteredLibraryItems();
    if (els.libraryItemsCount) {
      els.libraryItemsCount.textContent = `Показано: ${items.length} / ${state.catalog.items.length}`;
    }

    for (const item of items) {
      els.libraryItemsGrid.appendChild(createLibraryItemCard(item));
    }
  }

  function createLibraryItemCard(item) {
    const card = document.createElement("div");
    card.className = "library-item-card";
    card.draggable = true;
    card.addEventListener("dragstart", (event) => {
      beginDrag(event, { source: "library", shortname: item.shortname });
    });
    card.addEventListener("dragend", clearDragPayload);

    const thumb = createItemThumb(item.shortname, getItemLabel(item.shortname));

    const meta = document.createElement("div");
    const title = document.createElement("div");
    title.className = "library-item-title";
    title.textContent = getItemLabel(item.shortname);
    const short = document.createElement("div");
    short.className = "library-item-shortname";
    short.textContent = item.shortname;
    const category = document.createElement("div");
    category.className = "library-item-category";
    category.textContent = getItemCategoryLabel(item);
    meta.appendChild(title);
    meta.appendChild(short);
    meta.appendChild(category);

    const actions = document.createElement("div");
    actions.className = "library-item-actions";
    const addBtn = document.createElement("button");
    addBtn.className = "btn small";
    addBtn.type = "button";
    addBtn.textContent = "Добавить";
    addBtn.addEventListener("click", () => {
      addItemToRule(item.shortname, null, readAddItemTemplate());
    });
    actions.appendChild(addBtn);

    card.appendChild(thumb);
    card.appendChild(meta);
    card.appendChild(actions);
    return card;
  }

  function renderContainerItemsVisual() {
    if (!els.containerItemsList) return;
    const rule = getSelectedRule();
    els.containerItemsList.innerHTML = "";
    if (els.containerItemsMeta) {
      els.containerItemsMeta.textContent = rule && Array.isArray(rule["Items"]) ? `Предметов: ${rule["Items"].length}` : "";
    }

    if (!rule || !Array.isArray(rule["Items"])) {
      const emptyNoRule = document.createElement("div");
      emptyNoRule.className = "container-items-empty";
      emptyNoRule.textContent = "Выбери правило контейнера слева.";
      els.containerItemsList.appendChild(emptyNoRule);
      return;
    }

    if (rule["Items"].length === 0) {
      const empty = document.createElement("div");
      empty.className = "container-items-empty";
      empty.textContent = "В контейнере пока нет предметов. Перетащи предмет из библиотеки.";
      els.containerItemsList.appendChild(empty);
      return;
    }

    rule["Items"].forEach((item, index) => {
      els.containerItemsList.appendChild(createContainerItemCard(item, index));
    });
  }

  function createContainerItemCard(item, index) {
    const card = document.createElement("div");
    card.className = "container-item-card";
    card.draggable = true;
    card.addEventListener("dragstart", (event) => {
      beginDrag(event, { source: "container", index });
    });
    card.addEventListener("dragend", clearDragPayload);
    card.addEventListener("dragover", (event) => {
      const payload = resolveDragPayload(event);
      if (!payload) return;
      event.preventDefault();
      card.classList.add("drag-target");
      if (event.dataTransfer) {
        event.dataTransfer.dropEffect = payload.source === "library" ? "copy" : "move";
      }
    });
    card.addEventListener("dragleave", () => {
      card.classList.remove("drag-target");
    });
    card.addEventListener("drop", (event) => {
      event.preventDefault();
      card.classList.remove("drag-target");
      const payload = resolveDragPayload(event);
      if (!payload) return;

      if (payload.source === "library") {
        addItemToRule(payload.shortname, index, readAddItemTemplate());
      } else if (payload.source === "container") {
        moveRuleItem(payload.index, index);
      }
      clearDragPayload();
    });

    const thumb = createItemThumb(item["Shortname"], getItemLabel(item["Shortname"]));

    const info = document.createElement("div");
    const title = document.createElement("div");
    title.className = "container-item-title";
    title.textContent = getItemLabel(item["Shortname"]);
    const short = document.createElement("div");
    short.className = "container-item-shortname";
    short.textContent = item["Shortname"];
    const fields = document.createElement("div");
    fields.className = "container-item-fields";
    fields.appendChild(createMiniField("Мин", item["Min amount"], (v) => onItemCellChanged(index, "Min amount", v), "1", "1"));
    fields.appendChild(createMiniField("Макс", item["Max amount"], (v) => onItemCellChanged(index, "Max amount", v), "1", "1"));
    fields.appendChild(createMiniField("Шанс", item["Chance (0-1)"], (v) => onItemCellChanged(index, "Chance (0-1)", v), "0.01", "0"));
    fields.appendChild(createMiniField("Вес", item["Weight"], (v) => onItemCellChanged(index, "Weight", v), "0.01", "0.01"));
    fields.appendChild(createMiniField("Skin", item["Skin"], (v) => onItemCellChanged(index, "Skin", v), "1", "0"));
    info.appendChild(title);
    info.appendChild(short);
    info.appendChild(fields);

    const actions = document.createElement("div");
    actions.className = "container-item-actions";
    const removeBtn = document.createElement("button");
    removeBtn.type = "button";
    removeBtn.className = "btn danger small";
    removeBtn.textContent = "Удалить";
    removeBtn.addEventListener("click", () => removeItem(index));
    actions.appendChild(removeBtn);

    card.appendChild(thumb);
    card.appendChild(info);
    card.appendChild(actions);
    return card;
  }

  function createMiniField(label, value, onChange, step, min) {
    const wrap = document.createElement("label");
    wrap.className = "field-mini";
    const title = document.createElement("span");
    title.textContent = label;
    const input = document.createElement("input");
    input.type = "number";
    input.value = String(value);
    if (step) input.step = step;
    if (min) input.min = min;
    input.addEventListener("change", () => onChange(input.value));
    wrap.appendChild(title);
    wrap.appendChild(input);
    return wrap;
  }

  function createItemThumb(shortname, altText) {
    const img = document.createElement("img");
    img.className = "item-thumb";
    img.alt = altText || shortname;
    img.loading = "lazy";
    img.decoding = "async";
    img.src = getItemIconUrl(shortname);
    img.addEventListener("error", () => {
      if (img.dataset.fallback === "1") return;
      img.dataset.fallback = "1";
      img.src = createFallbackIconDataUrl(shortname);
    });
    return img;
  }

  function getItemIconUrl(shortname) {
    const key = normalizeKey(shortname);
    if (!key) return createFallbackIconDataUrl("item");
    return ITEM_ICON_BASE + encodeURIComponent(key) + ".png";
  }

  function createFallbackIconDataUrl(shortname) {
    const key = normalizeKey(shortname);
    const text = key ? key.slice(0, 2).toUpperCase() : "??";
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><rect width="100%" height="100%" rx="10" fill="#1b2431"/><text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#8ea0ba" font-family="Segoe UI" font-size="18">${text}</text></svg>`;
    return "data:image/svg+xml;charset=UTF-8," + encodeURIComponent(svg);
  }

  function beginDrag(event, payload) {
    state.dragPayload = payload;
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = payload.source === "library" ? "copy" : "move";
      event.dataTransfer.setData("text/plain", JSON.stringify(payload));
    }
  }

  function resolveDragPayload(event) {
    if (state.dragPayload) return state.dragPayload;
    if (!event || !event.dataTransfer) return null;
    const raw = event.dataTransfer.getData("text/plain");
    if (!raw) return null;
    try {
      const payload = JSON.parse(raw);
      if (!payload || typeof payload !== "object") return null;
      if (payload.source === "library" && normalizeKey(payload.shortname)) return payload;
      if (payload.source === "container" && Number.isInteger(payload.index)) return payload;
    } catch (_err) {
      return null;
    }
    return null;
  }

  function clearDragPayload() {
    state.dragPayload = null;
    if (els.containerItemsDrop) els.containerItemsDrop.classList.remove("drag-active");
    if (els.containerTrashDrop) els.containerTrashDrop.classList.remove("drag-active");
    document.querySelectorAll(".drag-target").forEach((node) => node.classList.remove("drag-target"));
  }

  function onContainerDropZoneDragOver(event) {
    const payload = resolveDragPayload(event);
    if (!payload) return;
    event.preventDefault();
    els.containerItemsDrop.classList.add("drag-active");
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = payload.source === "library" ? "copy" : "move";
    }
  }

  function onContainerDropZoneDrop(event) {
    event.preventDefault();
    const payload = resolveDragPayload(event);
    els.containerItemsDrop.classList.remove("drag-active");
    if (!payload) {
      clearDragPayload();
      return;
    }

    const rule = getSelectedRule();
    if (!rule || !Array.isArray(rule["Items"])) {
      clearDragPayload();
      return;
    }

    if (payload.source === "library") {
      addItemToRule(payload.shortname, rule["Items"].length, readAddItemTemplate());
    } else if (payload.source === "container") {
      moveRuleItem(payload.index, rule["Items"].length);
    }
    clearDragPayload();
  }

  function onTrashDropZoneDragOver(event) {
    const payload = resolveDragPayload(event);
    if (!payload || payload.source !== "container") return;
    event.preventDefault();
    els.containerTrashDrop.classList.add("drag-active");
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = "move";
    }
  }

  function onTrashDropZoneDrop(event) {
    event.preventDefault();
    const payload = resolveDragPayload(event);
    els.containerTrashDrop.classList.remove("drag-active");
    if (!payload || payload.source !== "container") {
      clearDragPayload();
      return;
    }

    removeItem(payload.index);
    clearDragPayload();
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
    if (els.librarySearch) {
      els.librarySearch.value = state.librarySearch;
    }
    renderLibraryCategoryOptions();
    renderLibraryCategoryBadges();
    renderAddItemNameHint();
    renderAddItemSuggestions();
    renderItemLibrary();
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
      renderContainerItemsVisual();
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

    renderContainerItemsVisual();
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
