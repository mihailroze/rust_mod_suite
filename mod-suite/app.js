(function () {
  "use strict";
  // Rust Mod Suite hub logic by Shmatko.

  const DEPLOY_PLUGIN_ENDPOINT = "/api/deploy-plugin";
  const DEPLOY_ALL_ENDPOINT = "/api/deploy-all";
  const HEALTH_ENDPOINT = "/health";

  const els = {
    deployAllBtn: document.getElementById("deployAllBtn"),
    deployPrivilegeBtn: document.getElementById("deployPrivilegeBtn"),
    deployLootBtn: document.getElementById("deployLootBtn"),
    deployStatus: document.getElementById("deployStatus"),
    refreshHealthBtn: document.getElementById("refreshHealthBtn"),
    healthBox: document.getElementById("healthBox")
  };

  bindEvents();
  refreshHealth();

  function bindEvents() {
    if (els.deployAllBtn) {
      els.deployAllBtn.addEventListener("click", () => deployAll(["privilege", "loot"]));
    }
    if (els.deployPrivilegeBtn) {
      els.deployPrivilegeBtn.addEventListener("click", () => deployPlugin("privilege"));
    }
    if (els.deployLootBtn) {
      els.deployLootBtn.addEventListener("click", () => deployPlugin("loot"));
    }
    if (els.refreshHealthBtn) {
      els.refreshHealthBtn.addEventListener("click", refreshHealth);
    }
  }

  function setDeployStatus(text, isError) {
    if (!els.deployStatus) return;
    els.deployStatus.textContent = String(text || "");
    els.deployStatus.style.color = isError ? "#ff7b72" : "#3fb950";
  }

  async function deployPlugin(plugin) {
    setDeployStatus("Развертывание...", false);
    try {
      const res = await fetch(DEPLOY_PLUGIN_ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ plugin: plugin })
      });
      const payload = await safeJson(res);
      if (!res.ok || payload.ok !== true) {
        throw new Error(String(payload.error || `HTTP ${res.status}`));
      }
      setDeployStatus(`Развернуто ${payload.plugin}: ${payload.path}`, false);
    } catch (err) {
      setDeployStatus(`Ошибка развертывания: ${err && err.message ? err.message : String(err)}`, true);
    }
  }

  async function deployAll(plugins) {
    setDeployStatus("Развертываю оба плагина...", false);
    try {
      const res = await fetch(DEPLOY_ALL_ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ plugins: plugins })
      });
      const payload = await safeJson(res);
      if (!res.ok || payload.ok !== true) {
        const details = payload.errors && payload.errors.length > 0
          ? payload.errors.join("; ")
          : `HTTP ${res.status}`;
        throw new Error(details);
      }
      const deployed = Array.isArray(payload.deployed) ? payload.deployed : [];
      const lines = deployed.map((entry) => `${entry.plugin}: ${entry.path}`);
      setDeployStatus(`Развертывание завершено. ${lines.join(" | ")}`, false);
    } catch (err) {
      setDeployStatus(`Ошибка развертывания: ${err && err.message ? err.message : String(err)}`, true);
    }
  }

  async function refreshHealth() {
    if (els.healthBox) {
      els.healthBox.textContent = "Загрузка...";
    }
    try {
      const res = await fetch(HEALTH_ENDPOINT, { method: "GET" });
      const payload = await safeJson(res);
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }
      if (els.healthBox) {
        els.healthBox.textContent = JSON.stringify(payload, null, 2);
      }
    } catch (err) {
      if (els.healthBox) {
        els.healthBox.textContent = `Ошибка запроса health: ${err && err.message ? err.message : String(err)}`;
      }
    }
  }

  async function safeJson(response) {
    try {
      return await response.json();
    } catch (_err) {
      return {};
    }
  }
})();
