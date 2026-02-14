using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ContainerLootManager", "Shmatko", "0.1.2")]
    [Description("Per-container loot tables with custom items and spawn chance.")]
    public class ContainerLootManager : RustPlugin
    {
        private const string PermissionAdmin = "containerlootmanager.admin";
        private const string UiRoot = "ContainerLootManager.UI.Root";
        private ConfigData config;
        private readonly HashSet<string> warnedInvalidItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<ulong> uiViewers = new HashSet<ulong>();
        private readonly Dictionary<ulong, string> uiSelectedRuleKey = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, int> uiSelectedItemIndex = new Dictionary<ulong, int>();
        private MethodInfo spawnLootMethod;
        private bool warnedMissingSpawnLootMethod;

        private class LootItemEntry
        {
            [JsonProperty("Shortname")]
            public string ShortName = "scrap";

            [JsonProperty("Min amount")]
            public int MinAmount = 1;

            [JsonProperty("Max amount")]
            public int MaxAmount = 10;

            [JsonProperty("Chance (0-1)")]
            public float Chance = 0.5f;

            [JsonProperty("Weight")]
            public float Weight = 1f;

            [JsonProperty("Skin")]
            public ulong Skin;
        }

        private class ContainerRule
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Override default loot")]
            public bool OverrideDefaultLoot = true;

            [JsonProperty("Min rolls")]
            public int MinRolls = 2;

            [JsonProperty("Max rolls")]
            public int MaxRolls = 4;

            [JsonProperty("Allow duplicate rolls")]
            public bool AllowDuplicates = true;

            [JsonProperty("Force at least one item")]
            public bool ForceAtLeastOneItem = true;

            [JsonProperty("Max stacks in container (0 = unlimited)")]
            public int MaxStacks;

            [JsonProperty("Items")]
            public List<LootItemEntry> Items = new List<LootItemEntry>();
        }

        private class ConfigData
        {
            [JsonProperty("Plugin enabled")]
            public bool Enabled = true;

            [JsonProperty("Use vanilla loot if no matching rule")]
            public bool UseVanillaWhenNoRule = true;

            [JsonProperty("Debug log")]
            public bool DebugLog = false;

            [JsonProperty("Rules by container key")]
            public Dictionary<string, ContainerRule> Rules = new Dictionary<string, ContainerRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["crate_normal_2"] = new ContainerRule
                {
                    Enabled = true,
                    OverrideDefaultLoot = true,
                    MinRolls = 2,
                    MaxRolls = 4,
                    AllowDuplicates = true,
                    ForceAtLeastOneItem = true,
                    MaxStacks = 6,
                    Items = new List<LootItemEntry>
                    {
                        new LootItemEntry { ShortName = "scrap", MinAmount = 20, MaxAmount = 80, Chance = 0.90f, Weight = 3f },
                        new LootItemEntry { ShortName = "metal.fragments", MinAmount = 200, MaxAmount = 700, Chance = 0.70f, Weight = 2f },
                        new LootItemEntry { ShortName = "rope", MinAmount = 1, MaxAmount = 3, Chance = 0.35f, Weight = 1f }
                    }
                },
                ["loot-barrel-1"] = new ContainerRule
                {
                    Enabled = true,
                    OverrideDefaultLoot = true,
                    MinRolls = 1,
                    MaxRolls = 2,
                    AllowDuplicates = false,
                    ForceAtLeastOneItem = true,
                    MaxStacks = 3,
                    Items = new List<LootItemEntry>
                    {
                        new LootItemEntry { ShortName = "scrap", MinAmount = 2, MaxAmount = 12, Chance = 0.85f, Weight = 3f },
                        new LootItemEntry { ShortName = "metal.fragments", MinAmount = 40, MaxAmount = 150, Chance = 0.60f, Weight = 2f },
                        new LootItemEntry { ShortName = "cloth", MinAmount = 5, MaxAmount = 25, Chance = 0.45f, Weight = 1f }
                    }
                }
            };
        }

        private class CatalogItemEntry
        {
            [JsonProperty("Shortname")]
            public string ShortName;

            [JsonProperty("Display name")]
            public string DisplayName;

            [JsonProperty("Category")]
            public string Category;

            [JsonProperty("Stack size")]
            public int StackSize;
        }

        private class CatalogContainerEntry
        {
            [JsonProperty("Short prefab name")]
            public string ShortPrefabName;

            [JsonProperty("Prefab name")]
            public string PrefabName;
        }

        private class CatalogData
        {
            [JsonProperty("Generated at UTC")]
            public string GeneratedAtUtc;

            [JsonProperty("Items")]
            public List<CatalogItemEntry> Items = new List<CatalogItemEntry>();

            [JsonProperty("Containers")]
            public List<CatalogContainerEntry> Containers = new List<CatalogContainerEntry>();

            [JsonProperty("Observed rules by container key")]
            public Dictionary<string, ContainerRule> ObservedRulesByContainerKey = new Dictionary<string, ContainerRule>(StringComparer.OrdinalIgnoreCase);

            [JsonProperty("Observed sample count by container key")]
            public Dictionary<string, int> ObservedSampleCountByContainerKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private class ObservedItemStat
        {
            public int ContainerOccurrences;
            public int MinAmount = int.MaxValue;
            public int MaxAmount;
        }

        private class ObservedContainerStat
        {
            public int SampleCount;
            public int MinStacks = int.MaxValue;
            public int MaxStacks;
            public readonly Dictionary<string, ObservedItemStat> Items = new Dictionary<string, ObservedItemStat>(StringComparer.OrdinalIgnoreCase);
        }

        private void Init()
        {
            permission.RegisterPermission(PermissionAdmin, this);
        }

        private void OnServerInitialized()
        {
            timer.Once(2f, () => ExportCatalog(null));
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
            }

            uiViewers.Clear();
            uiSelectedRuleKey.Clear();
            uiSelectedItemIndex.Clear();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            uiViewers.Remove(player.userID);
            uiSelectedRuleKey.Remove(player.userID);
            uiSelectedItemIndex.Remove(player.userID);
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    throw new Exception("Config is empty");
                }
            }
            catch (Exception ex)
            {
                PrintWarning($"Failed to load config, using defaults: {ex.Message}");
                config = new ConfigData();
            }

            ValidateConfig();
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        private void ValidateConfig()
        {
            if (config == null)
            {
                config = new ConfigData();
            }

            if (config.Rules == null)
            {
                config.Rules = new Dictionary<string, ContainerRule>(StringComparer.OrdinalIgnoreCase);
            }

            var normalized = new Dictionary<string, ContainerRule>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in config.Rules)
            {
                var key = NormalizeKey(pair.Key);
                if (string.IsNullOrEmpty(key)) continue;

                var rule = pair.Value ?? new ContainerRule();
                rule.MinRolls = Mathf.Clamp(rule.MinRolls, 0, 64);
                rule.MaxRolls = Mathf.Clamp(rule.MaxRolls, 0, 64);
                if (rule.MaxRolls < rule.MinRolls)
                {
                    rule.MaxRolls = rule.MinRolls;
                }

                if (rule.MaxStacks < 0)
                {
                    rule.MaxStacks = 0;
                }

                if (rule.Items == null)
                {
                    rule.Items = new List<LootItemEntry>();
                }

                foreach (var entry in rule.Items)
                {
                    if (entry == null) continue;
                    if (entry.MinAmount < 1) entry.MinAmount = 1;
                    if (entry.MaxAmount < entry.MinAmount) entry.MaxAmount = entry.MinAmount;
                    if (entry.Chance < 0f) entry.Chance = 0f;
                    if (entry.Chance > 1f) entry.Chance = 1f;
                    if (entry.Weight <= 0f) entry.Weight = 0.01f;
                }

                rule.Items.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.ShortName));
                normalized[key] = rule;
            }

            config.Rules = normalized;
        }

        private static string NormalizeKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
        }

        private bool HasAccess(BasePlayer player)
        {
            if (player == null) return false;
            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermissionAdmin);
        }

        private static string GetPrefabFileKey(LootContainer container)
        {
            if (container == null || string.IsNullOrWhiteSpace(container.PrefabName))
            {
                return string.Empty;
            }

            var prefab = container.PrefabName.ToLowerInvariant();
            var slash = prefab.LastIndexOf('/');
            var fileName = slash >= 0 ? prefab.Substring(slash + 1) : prefab;

            if (fileName.EndsWith(".prefab", StringComparison.Ordinal))
            {
                fileName = fileName.Substring(0, fileName.Length - ".prefab".Length);
            }

            return NormalizeKey(fileName);
        }

        private bool TryFindRule(LootContainer container, out string matchedKey, out ContainerRule rule)
        {
            matchedKey = string.Empty;
            rule = null;

            if (container == null || config?.Rules == null || config.Rules.Count == 0)
            {
                return false;
            }

            var shortKey = NormalizeKey(container.ShortPrefabName);
            var prefabFileKey = GetPrefabFileKey(container);
            var prefabFullKey = NormalizeKey(container.PrefabName);

            if (!string.IsNullOrEmpty(shortKey) && config.Rules.TryGetValue(shortKey, out rule))
            {
                matchedKey = shortKey;
                return true;
            }

            if (!string.IsNullOrEmpty(prefabFileKey) && config.Rules.TryGetValue(prefabFileKey, out rule))
            {
                matchedKey = prefabFileKey;
                return true;
            }

            if (!string.IsNullOrEmpty(prefabFullKey) && config.Rules.TryGetValue(prefabFullKey, out rule))
            {
                matchedKey = prefabFullKey;
                return true;
            }

            return false;
        }

        private void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null) return;
            if (config == null || !config.Enabled) return;

            string ruleKey;
            ContainerRule rule;
            var hasRule = TryFindRule(container, out ruleKey, out rule);

            if (!hasRule)
            {
                if (!config.UseVanillaWhenNoRule)
                {
                    ClearContainer(container.inventory);
                    container.inventory.MarkDirty();
                }
                return;
            }

            if (rule == null || !rule.Enabled) return;

            if (rule.OverrideDefaultLoot)
            {
                ClearContainer(container.inventory);
            }

            var added = FillWithCustomLoot(container, ruleKey, rule);

            if (rule.MaxStacks > 0)
            {
                TrimToMaxStacks(container.inventory, rule.MaxStacks);
            }

            container.inventory.MarkDirty();

            if (config.DebugLog)
            {
                Puts($"Applied container rule '{ruleKey}' to '{container.ShortPrefabName}' (added stacks: {added}).");
            }
        }

        private static void ClearContainer(ItemContainer inventory)
        {
            if (inventory == null) return;
            var snapshot = inventory.itemList.ToArray();
            foreach (var item in snapshot)
            {
                if (item == null) continue;
                item.RemoveFromContainer();
                item.Remove();
            }
        }

        private int FillWithCustomLoot(LootContainer container, string ruleKey, ContainerRule rule)
        {
            if (container?.inventory == null || rule?.Items == null || rule.Items.Count == 0)
            {
                return 0;
            }

            var validEntries = new List<LootItemEntry>();
            foreach (var entry in rule.Items)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ShortName))
                {
                    continue;
                }

                var definition = ItemManager.FindItemDefinition(entry.ShortName);
                if (definition == null)
                {
                    if (warnedInvalidItems.Add(entry.ShortName))
                    {
                        PrintWarning($"Unknown item shortname in rule '{ruleKey}': {entry.ShortName}");
                    }
                    continue;
                }

                validEntries.Add(entry);
            }

            if (validEntries.Count == 0)
            {
                return 0;
            }

            var minRolls = Mathf.Max(0, rule.MinRolls);
            var maxRolls = Mathf.Max(minRolls, rule.MaxRolls);
            var rolls = UnityEngine.Random.Range(minRolls, maxRolls + 1);

            var availableIndexes = new List<int>();
            for (var i = 0; i < validEntries.Count; i++)
            {
                availableIndexes.Add(i);
            }

            var addedStacks = 0;
            for (var roll = 0; roll < rolls; roll++)
            {
                if (availableIndexes.Count == 0) break;

                var poolIndex = SelectWeightedIndex(validEntries, availableIndexes);
                if (poolIndex < 0) break;

                var entryIndex = availableIndexes[poolIndex];
                var entry = validEntries[entryIndex];

                if (!rule.AllowDuplicates)
                {
                    availableIndexes.RemoveAt(poolIndex);
                }

                if (UnityEngine.Random.Range(0f, 1f) > entry.Chance)
                {
                    continue;
                }

                if (TryCreateAndMoveItem(container.inventory, entry))
                {
                    addedStacks++;
                }
            }

            if (addedStacks == 0 && rule.ForceAtLeastOneItem && validEntries.Count > 0)
            {
                var fallbackIndexes = availableIndexes.Count > 0 ? availableIndexes : BuildAllIndexes(validEntries.Count);
                var forcedPoolIndex = SelectWeightedIndex(validEntries, fallbackIndexes);
                if (forcedPoolIndex >= 0)
                {
                    var forcedEntry = validEntries[fallbackIndexes[forcedPoolIndex]];
                    if (TryCreateAndMoveItem(container.inventory, forcedEntry))
                    {
                        addedStacks++;
                    }
                }
            }

            return addedStacks;
        }

        private static List<int> BuildAllIndexes(int count)
        {
            var result = new List<int>();
            for (var i = 0; i < count; i++)
            {
                result.Add(i);
            }
            return result;
        }

        private static int SelectWeightedIndex(List<LootItemEntry> entries, List<int> pool)
        {
            if (entries == null || pool == null || pool.Count == 0)
            {
                return -1;
            }

            var totalWeight = 0f;
            for (var i = 0; i < pool.Count; i++)
            {
                var weight = entries[pool[i]].Weight;
                if (weight <= 0f) weight = 0.01f;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return UnityEngine.Random.Range(0, pool.Count);
            }

            var roll = UnityEngine.Random.Range(0f, totalWeight);
            var accumulated = 0f;
            for (var i = 0; i < pool.Count; i++)
            {
                var weight = entries[pool[i]].Weight;
                if (weight <= 0f) weight = 0.01f;
                accumulated += weight;
                if (roll <= accumulated)
                {
                    return i;
                }
            }

            return pool.Count - 1;
        }

        private void ReplyTo(BasePlayer player, string message)
        {
            if (player != null)
            {
                SendReply(player, message);
            }
            else
            {
                Puts(message);
            }
        }

        private bool TryRespawnVanillaLoot(LootContainer container)
        {
            if (container == null || container.inventory == null)
            {
                return false;
            }

            if (spawnLootMethod == null)
            {
                spawnLootMethod = typeof(LootContainer).GetMethod(
                    "SpawnLoot",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null
                );

                if (spawnLootMethod == null && !warnedMissingSpawnLootMethod)
                {
                    warnedMissingSpawnLootMethod = true;
                    PrintWarning("LootContainer.SpawnLoot() was not found. 'respawnall all' can only apply custom rules.");
                }
            }

            if (spawnLootMethod == null)
            {
                return false;
            }

            try
            {
                ClearContainer(container.inventory);
                spawnLootMethod.Invoke(container, null);
                container.inventory.MarkDirty();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCreateAndMoveItem(ItemContainer container, LootItemEntry entry)
        {
            if (container == null || entry == null || string.IsNullOrWhiteSpace(entry.ShortName))
            {
                return false;
            }

            var minAmount = Mathf.Max(1, entry.MinAmount);
            var maxAmount = Mathf.Max(minAmount, entry.MaxAmount);
            var amount = UnityEngine.Random.Range(minAmount, maxAmount + 1);

            var item = ItemManager.CreateByName(entry.ShortName, amount, entry.Skin);
            if (item == null) return false;

            if (!item.MoveToContainer(container))
            {
                item.Remove();
                return false;
            }

            return true;
        }

        private static void TrimToMaxStacks(ItemContainer container, int maxStacks)
        {
            if (container == null || maxStacks <= 0) return;

            while (container.itemList.Count > maxStacks)
            {
                var index = UnityEngine.Random.Range(0, container.itemList.Count);
                var item = container.itemList[index];
                if (item == null) break;
                item.RemoveFromContainer();
                item.Remove();
            }
        }

        private List<string> GetSortedRuleKeys()
        {
            var keys = new List<string>(config.Rules.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            return keys;
        }

        private string GetLookedContainerKey(BasePlayer player)
        {
            var target = FindLookAtLootContainer(player, 12f);
            if (target == null) return string.Empty;

            var key = NormalizeKey(target.ShortPrefabName);
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }

            return GetPrefabFileKey(target);
        }

        private string EnsureUiSelectedRule(BasePlayer player)
        {
            if (player == null) return "crate_normal_2";

            string selectedKey;
            if (uiSelectedRuleKey.TryGetValue(player.userID, out selectedKey))
            {
                selectedKey = NormalizeKey(selectedKey);
                if (!string.IsNullOrEmpty(selectedKey))
                {
                    GetOrCreateRule(selectedKey);
                    return selectedKey;
                }
            }

            var keys = GetSortedRuleKeys();
            if (keys.Count > 0)
            {
                selectedKey = keys[0];
                uiSelectedRuleKey[player.userID] = selectedKey;
                return selectedKey;
            }

            selectedKey = "crate_normal_2";
            GetOrCreateRule(selectedKey);
            uiSelectedRuleKey[player.userID] = selectedKey;
            SaveConfig();
            return selectedKey;
        }

        private int GetSelectedItemIndex(BasePlayer player, ContainerRule rule)
        {
            if (player == null || rule == null || rule.Items == null || rule.Items.Count == 0)
            {
                return -1;
            }

            int index;
            if (!uiSelectedItemIndex.TryGetValue(player.userID, out index))
            {
                index = 0;
            }

            if (index < 0) index = 0;
            if (index >= rule.Items.Count) index = rule.Items.Count - 1;
            uiSelectedItemIndex[player.userID] = index;
            return index;
        }

        private void OpenUi(BasePlayer player)
        {
            if (player == null || !HasAccess(player)) return;

            var selectedKey = EnsureUiSelectedRule(player);
            var rule = GetOrCreateRule(selectedKey);
            var selectedItem = GetSelectedItemIndex(player, rule);

            uiViewers.Add(player.userID);

            DestroyUi(player);

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.09 0.12 0.97" },
                RectTransform = { AnchorMin = "0.12 0.05", AnchorMax = "0.88 0.95" },
                CursorEnabled = true
            }, "Overlay", UiRoot);

            container.Add(new CuiPanel
            {
                Image = { Color = "0.14 0.17 0.23 0.98" },
                RectTransform = { AnchorMin = "0.01 0.93", AnchorMax = "0.99 0.99" }
            }, UiRoot, UiRoot + ".hdr");

            container.Add(new CuiLabel
            {
                Text = { Text = $"ContainerLootManager - CUI | Rule: {selectedKey}", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.96 0.96 0.96 1" },
                RectTransform = { AnchorMin = "0.02 0.1", AnchorMax = "0.80 0.9" }
            }, UiRoot + ".hdr");

            AddUiButton(container, UiRoot + ".hdr", "0.82 0.15", "0.90 0.85", "0.22 0.40 0.20 0.95", "SAVE", "loot.ui.save");
            AddUiButton(container, UiRoot + ".hdr", "0.91 0.15", "0.99 0.85", "0.60 0.20 0.20 0.95", "X", "loot.ui.close");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.14 0.18 0.96" },
                RectTransform = { AnchorMin = "0.01 0.84", AnchorMax = "0.99 0.92" }
            }, UiRoot, UiRoot + ".rule");

            AddUiButton(container, UiRoot + ".rule", "0.01 0.1", "0.06 0.9", "0.22 0.34 0.58 0.95", "<", "loot.ui.rule.prev");
            AddUiButton(container, UiRoot + ".rule", "0.07 0.1", "0.12 0.9", "0.22 0.34 0.58 0.95", ">", "loot.ui.rule.next");
            AddUiButton(container, UiRoot + ".rule", "0.13 0.1", "0.30 0.9", "0.24 0.44 0.26 0.95", "Use Looked", "loot.ui.rule.look");
            AddUiButton(container, UiRoot + ".rule", "0.31 0.1", "0.46 0.9", "0.24 0.36 0.52 0.95", "Reroll Looked", "loot.ui.reroll");
            AddUiButton(container, UiRoot + ".rule", "0.47 0.1", "0.62 0.9", "0.28 0.28 0.48 0.95", "Add Held Item", "loot.ui.item.addheld");

            container.Add(new CuiLabel
            {
                Text = { Text = "Target keys: /lootcfg where  |  Nearby: /lootcfg nearby", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.84 0.90 0.96 1" },
                RectTransform = { AnchorMin = "0.63 0.1", AnchorMax = "0.99 0.9" }
            }, UiRoot + ".rule");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.11 0.13 0.17 0.95" },
                RectTransform = { AnchorMin = "0.01 0.52", AnchorMax = "0.49 0.83" }
            }, UiRoot, UiRoot + ".opts");

            AddToggleButton(container, UiRoot + ".opts", "Enabled", rule.Enabled, "loot.ui.toggle enabled", "0.03 0.78", "0.31 0.93");
            AddToggleButton(container, UiRoot + ".opts", "Override Vanilla", rule.OverrideDefaultLoot, "loot.ui.toggle override", "0.34 0.78", "0.62 0.93");
            AddToggleButton(container, UiRoot + ".opts", "Duplicates", rule.AllowDuplicates, "loot.ui.toggle duplicates", "0.65 0.78", "0.93 0.93");
            AddToggleButton(container, UiRoot + ".opts", "Force One", rule.ForceAtLeastOneItem, "loot.ui.toggle forceone", "0.03 0.61", "0.31 0.76");

            AddAdjustRow(container, UiRoot + ".opts", "Min rolls", rule.MinRolls.ToString(), "minrolls", "0.03 0.43", true);
            AddAdjustRow(container, UiRoot + ".opts", "Max rolls", rule.MaxRolls.ToString(), "maxrolls", "0.03 0.26", true);
            AddAdjustRow(container, UiRoot + ".opts", "Max stacks", rule.MaxStacks.ToString(), "maxstacks", "0.03 0.09", true);

            container.Add(new CuiPanel
            {
                Image = { Color = "0.11 0.13 0.17 0.95" },
                RectTransform = { AnchorMin = "0.51 0.52", AnchorMax = "0.99 0.83" }
            }, UiRoot, UiRoot + ".items");

            container.Add(new CuiLabel
            {
                Text = { Text = "Items in rule", FontSize = 13, Align = TextAnchor.UpperLeft, Color = "0.95 0.95 0.95 1" },
                RectTransform = { AnchorMin = "0.03 0.86", AnchorMax = "0.80 0.98" }
            }, UiRoot + ".items");

            AddUiButton(container, UiRoot + ".items", "0.82 0.86", "0.98 0.98", "0.60 0.22 0.22 0.95", "Remove", "loot.ui.item.remove");

            var rows = rule.Items ?? new List<LootItemEntry>();
            var shown = Mathf.Min(rows.Count, 8);
            for (var i = 0; i < shown; i++)
            {
                var entry = rows[i];
                if (entry == null) continue;
                var yMax = 0.83f - (i * 0.1f);
                var yMin = yMax - 0.09f;
                var isSelected = i == selectedItem;
                var color = isSelected ? "0.28 0.50 0.28 0.95" : "0.18 0.22 0.30 0.95";
                var text = $"#{i + 1} {entry.ShortName} [{entry.MinAmount}-{entry.MaxAmount}] c:{entry.Chance:0.##} w:{entry.Weight:0.##}";
                AddUiButton(container, UiRoot + ".items", $"0.03 {yMin:0.00}", $"0.98 {yMax:0.00}", color, text, $"loot.ui.item.select {i}");
            }

            if (rows.Count == 0)
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = "No items. Add held item or use /lootcfg additem ...", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.82 0.86 0.90 1" },
                    RectTransform = { AnchorMin = "0.03 0.10", AnchorMax = "0.98 0.80" }
                }, UiRoot + ".items");
            }

            container.Add(new CuiPanel
            {
                Image = { Color = "0.11 0.13 0.17 0.95" },
                RectTransform = { AnchorMin = "0.01 0.02", AnchorMax = "0.99 0.50" }
            }, UiRoot, UiRoot + ".detail");

            if (selectedItem >= 0 && selectedItem < rows.Count)
            {
                var item = rows[selectedItem];
                container.Add(new CuiLabel
                {
                    Text = { Text = $"Selected item #{selectedItem + 1}: {item.ShortName}", FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "0.95 0.95 0.95 1" },
                    RectTransform = { AnchorMin = "0.02 0.83", AnchorMax = "0.70 0.97" }
                }, UiRoot + ".detail");

                AddItemAdjustRow(container, "Min amount", item.MinAmount.ToString(), "min", "0.02 0.62");
                AddItemAdjustRow(container, "Max amount", item.MaxAmount.ToString(), "max", "0.02 0.42");
                AddItemAdjustRow(container, "Chance", item.Chance.ToString("0.##", CultureInfo.InvariantCulture), "chance", "0.02 0.22", 0.05f, 0.1f);
                AddItemAdjustRow(container, "Weight", item.Weight.ToString("0.##", CultureInfo.InvariantCulture), "weight", "0.02 0.02", 0.25f, 1f);
            }
            else
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = "Select item in list above to edit.", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0.82 0.86 0.90 1" },
                    RectTransform = { AnchorMin = "0.02 0.1", AnchorMax = "0.98 0.9" }
                }, UiRoot + ".detail");
            }

            CuiHelper.AddUi(player, container);
        }

        private void RefreshUi(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            if (!uiViewers.Contains(player.userID)) return;
            OpenUi(player);
        }

        private void RefreshUiViewers()
        {
            if (uiViewers.Count == 0) return;
            var stale = new List<ulong>();
            foreach (var userId in uiViewers)
            {
                var player = BasePlayer.FindByID(userId);
                if (player == null || !player.IsConnected)
                {
                    stale.Add(userId);
                    continue;
                }

                OpenUi(player);
            }

            foreach (var userId in stale)
            {
                uiViewers.Remove(userId);
                uiSelectedRuleKey.Remove(userId);
                uiSelectedItemIndex.Remove(userId);
            }
        }

        private void DestroyUi(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, UiRoot);
        }

        private static void AddUiButton(CuiElementContainer container, string parent, string min, string max, string color, string text, string command, int fontSize = 12)
        {
            container.Add(new CuiButton
            {
                Button = { Color = color, Command = command },
                RectTransform = { AnchorMin = min, AnchorMax = max },
                Text = { Text = text, FontSize = fontSize, Align = TextAnchor.MiddleCenter, Color = "0.97 0.97 0.97 1" }
            }, parent);
        }

        private void AddToggleButton(CuiElementContainer container, string parent, string label, bool value, string command, string min, string max)
        {
            var color = value ? "0.20 0.56 0.24 0.95" : "0.56 0.22 0.22 0.95";
            AddUiButton(container, parent, min, max, color, $"{label}: {(value ? "ON" : "OFF")}", command);
        }

        private void AddAdjustRow(CuiElementContainer container, string parent, string label, string value, string field, string anchorMin, bool integer)
        {
            var split = anchorMin.Split(' ');
            var x = split[0];
            var y = split[1];
            var yMin = float.Parse(y, CultureInfo.InvariantCulture);
            var yMax = yMin + 0.14f;

            container.Add(new CuiLabel
            {
                Text = { Text = $"{label}: {value}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.94 0.94 0.94 1" },
                RectTransform = { AnchorMin = $"{x} {yMin:0.00}", AnchorMax = "0.54 " + yMax.ToString("0.00", CultureInfo.InvariantCulture) }
            }, parent);

            var small = integer ? "1" : "0.1";
            var big = integer ? "5" : "1";
            AddUiButton(container, parent, $"0.56 {yMin:0.00}", $"0.64 {yMax:0.00}", "0.56 0.22 0.22 0.95", "-" + small, $"loot.ui.adjust {field} -{small}");
            AddUiButton(container, parent, $"0.65 {yMin:0.00}", $"0.73 {yMax:0.00}", "0.28 0.42 0.62 0.95", "+" + small, $"loot.ui.adjust {field} {small}");
            AddUiButton(container, parent, $"0.75 {yMin:0.00}", $"0.83 {yMax:0.00}", "0.56 0.22 0.22 0.95", "-" + big, $"loot.ui.adjust {field} -{big}");
            AddUiButton(container, parent, $"0.84 {yMin:0.00}", $"0.92 {yMax:0.00}", "0.24 0.56 0.28 0.95", "+" + big, $"loot.ui.adjust {field} {big}");
        }

        private void AddItemAdjustRow(CuiElementContainer container, string label, string value, string field, string minAnchor, float small = 1f, float big = 5f)
        {
            var split = minAnchor.Split(' ');
            var yMin = float.Parse(split[1], CultureInfo.InvariantCulture);
            var yMax = yMin + 0.17f;
            var smallPos = small.ToString("0.##", CultureInfo.InvariantCulture);
            var smallNeg = (-small).ToString("0.##", CultureInfo.InvariantCulture);
            var bigPos = big.ToString("0.##", CultureInfo.InvariantCulture);
            var bigNeg = (-big).ToString("0.##", CultureInfo.InvariantCulture);

            container.Add(new CuiLabel
            {
                Text = { Text = $"{label}: {value}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.94 0.94 0.94 1" },
                RectTransform = { AnchorMin = $"0.02 {yMin:0.00}", AnchorMax = $"0.43 {yMax:0.00}" }
            }, UiRoot + ".detail");

            AddUiButton(container, UiRoot + ".detail", $"0.45 {yMin:0.00}", $"0.54 {yMax:0.00}", "0.56 0.22 0.22 0.95", "-" + smallPos, $"loot.ui.item.adjust {field} {smallNeg}");
            AddUiButton(container, UiRoot + ".detail", $"0.55 {yMin:0.00}", $"0.64 {yMax:0.00}", "0.28 0.42 0.62 0.95", "+" + smallPos, $"loot.ui.item.adjust {field} {smallPos}");
            AddUiButton(container, UiRoot + ".detail", $"0.66 {yMin:0.00}", $"0.75 {yMax:0.00}", "0.56 0.22 0.22 0.95", "-" + bigPos, $"loot.ui.item.adjust {field} {bigNeg}");
            AddUiButton(container, UiRoot + ".detail", $"0.76 {yMin:0.00}", $"0.85 {yMax:0.00}", "0.24 0.56 0.28 0.95", "+" + bigPos, $"loot.ui.item.adjust {field} {bigPos}");
        }

        [ChatCommand("lootcfg")]
        private void LootConfigChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player))
            {
                SendReply(player, "No access.");
                return;
            }

            if (args == null || args.Length == 0)
            {
                SendLootHelp(player);
                return;
            }

            var sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "help":
                    SendLootHelp(player);
                    return;
                case "where":
                    CommandWhere(player);
                    return;
                case "ui":
                    OpenUi(player);
                    return;
                case "exportcatalog":
                    CommandExportCatalog(player);
                    return;
                case "reroll":
                    CommandReroll(player);
                    return;
                case "respawnall":
                    CommandRespawnAll(player, args.Length >= 2 ? args[1] : null);
                    return;
                case "nearby":
                    CommandNearby(player, args);
                    return;
                case "list":
                    CommandListRules(player);
                    return;
                case "show":
                    CommandShowRule(player, args);
                    return;
                case "additem":
                    CommandAddItem(player, args);
                    return;
                case "delitem":
                    CommandDeleteItem(player, args);
                    return;
                case "clear":
                    CommandClearRuleItems(player, args);
                    return;
                case "draws":
                    CommandSetDraws(player, args);
                    return;
                case "enabled":
                    CommandSetEnabled(player, args);
                    return;
                case "override":
                    CommandSetOverride(player, args);
                    return;
                case "duplicates":
                    CommandSetDuplicates(player, args);
                    return;
                case "forceone":
                    CommandSetForceOne(player, args);
                    return;
                case "maxstacks":
                    CommandSetMaxStacks(player, args);
                    return;
                default:
                    SendReply(player, "Unknown subcommand. Use /lootcfg help");
                    return;
            }
        }

        private void SendLootHelp(BasePlayer player)
        {
            SendReply(player, "ContainerLootManager:");
            SendReply(player, "/lootui - open visual CUI editor");
            SendReply(player, "/lootcfg ui - same as /lootui");
            SendReply(player, "/lootcfg exportcatalog - export full item/catalog file");
            SendReply(player, "/lootcfg where - info about looked container");
            SendReply(player, "/lootcfg reroll - refill looked container now");
            SendReply(player, "/lootcfg respawnall [all|custom] - respawn loot on all map containers");
            SendReply(player, "/lootcfg nearby [radius] - nearby container keys");
            SendReply(player, "/lootcfg list - list all configured rules");
            SendReply(player, "/lootcfg show <containerKey> - show rule content");
            SendReply(player, "/lootcfg additem <key> <shortname> <min> <max> <chance> [weight]");
            SendReply(player, "/lootcfg delitem <key> <index>");
            SendReply(player, "/lootcfg clear <key> - clear all items in rule");
            SendReply(player, "/lootcfg draws <key> <minRolls> <maxRolls>");
            SendReply(player, "/lootcfg enabled <key> on|off");
            SendReply(player, "/lootcfg override <key> on|off");
            SendReply(player, "/lootcfg duplicates <key> on|off");
            SendReply(player, "/lootcfg forceone <key> on|off");
            SendReply(player, "/lootcfg maxstacks <key> <number>");
            SendReply(player, "Tip: chance can be 0-1 or 0-100 (percent).");
        }

        [ChatCommand("lootui")]
        private void LootUiChatCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasAccess(player))
            {
                SendReply(player, "No access.");
                return;
            }

            OpenUi(player);
        }

        private void CommandWhere(BasePlayer player)
        {
            var target = FindLookAtLootContainer(player, 12f);
            if (target == null)
            {
                SendReply(player, "No loot container in sight.");
                return;
            }

            var shortKey = NormalizeKey(target.ShortPrefabName);
            var prefabFileKey = GetPrefabFileKey(target);
            var prefabFull = NormalizeKey(target.PrefabName);

            string matchedKey;
            ContainerRule rule;
            var hasRule = TryFindRule(target, out matchedKey, out rule);

            SendReply(player, $"Short key: {shortKey}");
            SendReply(player, $"Prefab file key: {prefabFileKey}");
            SendReply(player, $"Prefab full key: {prefabFull}");
            SendReply(player, hasRule ? $"Matched rule: {matchedKey}" : "Matched rule: none");
        }

        private void CommandReroll(BasePlayer player)
        {
            var target = FindLookAtLootContainer(player, 12f);
            if (target == null)
            {
                SendReply(player, "No loot container in sight.");
                return;
            }

            OnLootSpawn(target);
            SendReply(player, $"Rerolled loot for: {target.ShortPrefabName}");
        }

        private void CommandRespawnAll(BasePlayer player, string modeArg)
        {
            var mode = string.IsNullOrWhiteSpace(modeArg) ? "all" : modeArg.Trim().ToLowerInvariant();
            var includeVanillaNoRule = mode != "custom";
            if (mode != "all" && mode != "custom")
            {
                ReplyTo(player, "Unknown mode. Use: /lootcfg respawnall [all|custom]");
                return;
            }

            var total = 0;
            var customApplied = 0;
            var vanillaApplied = 0;
            var clearedNoRule = 0;
            var skippedNoRule = 0;
            var failedVanilla = 0;

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var lootContainer = networkable as LootContainer;
                if (lootContainer == null || lootContainer.IsDestroyed || lootContainer.inventory == null)
                {
                    continue;
                }

                total++;

                string matchedKey;
                ContainerRule rule;
                var hasRule = TryFindRule(lootContainer, out matchedKey, out rule);
                if (hasRule)
                {
                    OnLootSpawn(lootContainer);
                    customApplied++;
                    continue;
                }

                if (!config.UseVanillaWhenNoRule)
                {
                    OnLootSpawn(lootContainer);
                    clearedNoRule++;
                    continue;
                }

                if (!includeVanillaNoRule)
                {
                    skippedNoRule++;
                    continue;
                }

                if (TryRespawnVanillaLoot(lootContainer))
                {
                    vanillaApplied++;
                }
                else
                {
                    failedVanilla++;
                }
            }

            ReplyTo(player, $"Loot respawn complete ({mode}). Containers: {total}, custom: {customApplied}, vanilla: {vanillaApplied}, cleared(no-rule): {clearedNoRule}, skipped(no-rule): {skippedNoRule}, vanilla-failed: {failedVanilla}.");
        }

        private void CommandExportCatalog(BasePlayer player)
        {
            ExportCatalog(player);
        }

        private void CommandNearby(BasePlayer player, string[] args)
        {
            var radius = 25f;
            if (args.Length >= 2)
            {
                float parsed;
                if (TryParseFloat(args[1], out parsed))
                {
                    radius = Mathf.Clamp(parsed, 5f, 200f);
                }
            }

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var lootContainer = networkable as LootContainer;
                if (lootContainer == null || lootContainer.IsDestroyed) continue;
                if (lootContainer.transform == null) continue;
                if (Vector3.Distance(player.transform.position, lootContainer.transform.position) > radius) continue;

                var key = NormalizeKey(lootContainer.ShortPrefabName);
                if (string.IsNullOrEmpty(key))
                {
                    key = GetPrefabFileKey(lootContainer);
                }

                if (string.IsNullOrEmpty(key)) continue;

                int count;
                counts.TryGetValue(key, out count);
                counts[key] = count + 1;
            }

            if (counts.Count == 0)
            {
                SendReply(player, $"No loot containers found within {radius:0.#}m.");
                return;
            }

            var keys = new List<string>(counts.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);

            SendReply(player, $"Nearby container keys within {radius:0.#}m:");
            var shown = 0;
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                SendReply(player, $"{key} (x{counts[key]})");
                shown++;
                if (shown >= 20)
                {
                    break;
                }
            }
        }

        private void CommandListRules(BasePlayer player)
        {
            if (config.Rules == null || config.Rules.Count == 0)
            {
                SendReply(player, "No configured rules.");
                return;
            }

            var keys = new List<string>(config.Rules.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            SendReply(player, $"Configured rules: {keys.Count}");
            foreach (var key in keys)
            {
                ContainerRule rule;
                if (!config.Rules.TryGetValue(key, out rule) || rule == null) continue;
                var itemCount = rule.Items?.Count ?? 0;
                SendReply(player, $"{key} | enabled:{rule.Enabled} override:{rule.OverrideDefaultLoot} draws:{rule.MinRolls}-{rule.MaxRolls} items:{itemCount}");
            }
        }

        private void CommandShowRule(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                SendReply(player, "Usage: /lootcfg show <containerKey>");
                return;
            }

            var key = NormalizeKey(args[1]);
            ContainerRule rule;
            if (!config.Rules.TryGetValue(key, out rule) || rule == null)
            {
                SendReply(player, $"Rule '{key}' not found.");
                return;
            }

            SendReply(player, $"{key}: enabled={rule.Enabled}, override={rule.OverrideDefaultLoot}, draws={rule.MinRolls}-{rule.MaxRolls}, duplicates={rule.AllowDuplicates}, forceone={rule.ForceAtLeastOneItem}, maxstacks={rule.MaxStacks}");
            if (rule.Items == null || rule.Items.Count == 0)
            {
                SendReply(player, "No items.");
                return;
            }

            for (var i = 0; i < rule.Items.Count; i++)
            {
                var item = rule.Items[i];
                if (item == null) continue;
                SendReply(player, $"#{i + 1}: {item.ShortName} amount {item.MinAmount}-{item.MaxAmount}, chance {item.Chance:0.###}, weight {item.Weight:0.###}, skin {item.Skin}");
            }
        }

        private void CommandAddItem(BasePlayer player, string[] args)
        {
            if (args.Length < 6)
            {
                SendReply(player, "Usage: /lootcfg additem <key> <shortname> <min> <max> <chance> [weight]");
                return;
            }

            var key = NormalizeKey(args[1]);
            if (string.IsNullOrEmpty(key))
            {
                SendReply(player, "Invalid key.");
                return;
            }

            var shortname = args[2].Trim().ToLowerInvariant();
            if (ItemManager.FindItemDefinition(shortname) == null)
            {
                SendReply(player, $"Unknown item shortname: {shortname}");
                return;
            }

            int minAmount;
            int maxAmount;
            float chance;
            if (!int.TryParse(args[3], out minAmount) || !int.TryParse(args[4], out maxAmount) || !TryParseFloat(args[5], out chance))
            {
                SendReply(player, "Invalid min/max/chance.");
                return;
            }

            if (chance > 1f)
            {
                chance /= 100f;
            }

            chance = Mathf.Clamp01(chance);
            minAmount = Mathf.Max(1, minAmount);
            maxAmount = Mathf.Max(minAmount, maxAmount);

            var weight = 1f;
            if (args.Length >= 7)
            {
                float parsedWeight;
                if (TryParseFloat(args[6], out parsedWeight))
                {
                    weight = Mathf.Max(0.01f, parsedWeight);
                }
            }

            var rule = GetOrCreateRule(key);
            rule.Items.Add(new LootItemEntry
            {
                ShortName = shortname,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                Chance = chance,
                Weight = weight
            });

            SaveConfig();
            SendReply(player, $"Added item to '{key}': {shortname} {minAmount}-{maxAmount}, chance={chance:0.###}, weight={weight:0.###}");
        }

        private void CommandDeleteItem(BasePlayer player, string[] args)
        {
            if (args.Length < 3)
            {
                SendReply(player, "Usage: /lootcfg delitem <key> <index>");
                return;
            }

            var key = NormalizeKey(args[1]);
            int index;
            if (!int.TryParse(args[2], out index))
            {
                SendReply(player, "Index must be a number.");
                return;
            }

            ContainerRule rule;
            if (!config.Rules.TryGetValue(key, out rule) || rule == null || rule.Items == null)
            {
                SendReply(player, $"Rule '{key}' not found.");
                return;
            }

            index -= 1;
            if (index < 0 || index >= rule.Items.Count)
            {
                SendReply(player, "Index out of range.");
                return;
            }

            var removed = rule.Items[index];
            rule.Items.RemoveAt(index);
            SaveConfig();
            SendReply(player, $"Removed item from '{key}': {removed.ShortName}");
        }

        private void CommandClearRuleItems(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                SendReply(player, "Usage: /lootcfg clear <key>");
                return;
            }

            var key = NormalizeKey(args[1]);
            var rule = GetOrCreateRule(key);
            rule.Items.Clear();
            SaveConfig();
            SendReply(player, $"Cleared items for '{key}'.");
        }

        private void CommandSetDraws(BasePlayer player, string[] args)
        {
            if (args.Length < 4)
            {
                SendReply(player, "Usage: /lootcfg draws <key> <minRolls> <maxRolls>");
                return;
            }

            var key = NormalizeKey(args[1]);
            int minRolls;
            int maxRolls;
            if (!int.TryParse(args[2], out minRolls) || !int.TryParse(args[3], out maxRolls))
            {
                SendReply(player, "Invalid draw values.");
                return;
            }

            minRolls = Mathf.Clamp(minRolls, 0, 64);
            maxRolls = Mathf.Clamp(maxRolls, 0, 64);
            if (maxRolls < minRolls) maxRolls = minRolls;

            var rule = GetOrCreateRule(key);
            rule.MinRolls = minRolls;
            rule.MaxRolls = maxRolls;
            SaveConfig();
            SendReply(player, $"Rule '{key}' draws set to {minRolls}-{maxRolls}.");
        }

        private void CommandSetEnabled(BasePlayer player, string[] args)
        {
            SetBooleanRuleProperty(player, args, "enabled");
        }

        private void CommandSetOverride(BasePlayer player, string[] args)
        {
            SetBooleanRuleProperty(player, args, "override");
        }

        private void CommandSetDuplicates(BasePlayer player, string[] args)
        {
            SetBooleanRuleProperty(player, args, "duplicates");
        }

        private void CommandSetForceOne(BasePlayer player, string[] args)
        {
            SetBooleanRuleProperty(player, args, "forceone");
        }

        private void SetBooleanRuleProperty(BasePlayer player, string[] args, string property)
        {
            if (args.Length < 3)
            {
                SendReply(player, $"Usage: /lootcfg {property} <key> on|off");
                return;
            }

            var key = NormalizeKey(args[1]);
            bool value;
            if (!TryParseOnOff(args[2], out value))
            {
                SendReply(player, "Use on|off.");
                return;
            }

            var rule = GetOrCreateRule(key);
            switch (property)
            {
                case "enabled":
                    rule.Enabled = value;
                    break;
                case "override":
                    rule.OverrideDefaultLoot = value;
                    break;
                case "duplicates":
                    rule.AllowDuplicates = value;
                    break;
                case "forceone":
                    rule.ForceAtLeastOneItem = value;
                    break;
            }

            SaveConfig();
            SendReply(player, $"Rule '{key}' {property} set to {(value ? "on" : "off")}.");
        }

        private void CommandSetMaxStacks(BasePlayer player, string[] args)
        {
            if (args.Length < 3)
            {
                SendReply(player, "Usage: /lootcfg maxstacks <key> <number>");
                return;
            }

            var key = NormalizeKey(args[1]);
            int maxStacks;
            if (!int.TryParse(args[2], out maxStacks))
            {
                SendReply(player, "Invalid number.");
                return;
            }

            maxStacks = Mathf.Max(0, maxStacks);

            var rule = GetOrCreateRule(key);
            rule.MaxStacks = maxStacks;
            SaveConfig();
            SendReply(player, $"Rule '{key}' max stacks set to {maxStacks}.");
        }

        [ConsoleCommand("loot.ui.open")]
        private void LootUiOpenConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;
            OpenUi(player);
        }

        [ConsoleCommand("loot.ui.close")]
        private void LootUiCloseConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            DestroyUi(player);
            uiViewers.Remove(player.userID);
            uiSelectedRuleKey.Remove(player.userID);
            uiSelectedItemIndex.Remove(player.userID);
        }

        [ConsoleCommand("loot.ui.save")]
        private void LootUiSaveConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;
            SaveConfig();
            SendReply(player, "ContainerLootManager config saved.");
            RefreshUi(player);
        }

        [ConsoleCommand("loot.ui.rule.prev")]
        private void LootUiRulePrevConsole(ConsoleSystem.Arg arg)
        {
            ShiftSelectedRule(arg.Player(), -1);
        }

        [ConsoleCommand("loot.ui.rule.next")]
        private void LootUiRuleNextConsole(ConsoleSystem.Arg arg)
        {
            ShiftSelectedRule(arg.Player(), 1);
        }

        private void ShiftSelectedRule(BasePlayer player, int direction)
        {
            if (player == null || !HasAccess(player)) return;

            var keys = GetSortedRuleKeys();
            if (keys.Count == 0)
            {
                var fallback = EnsureUiSelectedRule(player);
                uiSelectedRuleKey[player.userID] = fallback;
                uiSelectedItemIndex[player.userID] = 0;
                RefreshUi(player);
                return;
            }

            var current = EnsureUiSelectedRule(player);
            var index = keys.IndexOf(current);
            if (index < 0) index = 0;
            index += direction;
            if (index < 0) index = keys.Count - 1;
            if (index >= keys.Count) index = 0;

            uiSelectedRuleKey[player.userID] = keys[index];
            uiSelectedItemIndex[player.userID] = 0;
            RefreshUi(player);
        }

        [ConsoleCommand("loot.ui.rule.look")]
        private void LootUiRuleLookConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;

            var key = GetLookedContainerKey(player);
            if (string.IsNullOrEmpty(key))
            {
                SendReply(player, "Look at a loot container first.");
                return;
            }

            GetOrCreateRule(key);
            uiSelectedRuleKey[player.userID] = key;
            uiSelectedItemIndex[player.userID] = 0;
            SaveConfig();
            RefreshUi(player);
        }

        [ConsoleCommand("loot.ui.toggle")]
        private void LootUiToggleConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 1) return;

            var field = arg.Args[0].ToLowerInvariant();
            var key = EnsureUiSelectedRule(player);
            var rule = GetOrCreateRule(key);

            switch (field)
            {
                case "enabled":
                    rule.Enabled = !rule.Enabled;
                    break;
                case "override":
                    rule.OverrideDefaultLoot = !rule.OverrideDefaultLoot;
                    break;
                case "duplicates":
                    rule.AllowDuplicates = !rule.AllowDuplicates;
                    break;
                case "forceone":
                    rule.ForceAtLeastOneItem = !rule.ForceAtLeastOneItem;
                    break;
                default:
                    return;
            }

            SaveConfig();
            RefreshUi(player);
        }

        [ConsoleCommand("loot.ui.adjust")]
        private void LootUiAdjustConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 2) return;

            var field = arg.Args[0].ToLowerInvariant();
            float delta;
            if (!TryParseFloat(arg.Args[1], out delta)) return;

            var key = EnsureUiSelectedRule(player);
            var rule = GetOrCreateRule(key);

            switch (field)
            {
                case "minrolls":
                    rule.MinRolls = Mathf.Clamp(rule.MinRolls + Mathf.RoundToInt(delta), 0, 64);
                    if (rule.MaxRolls < rule.MinRolls) rule.MaxRolls = rule.MinRolls;
                    break;
                case "maxrolls":
                    rule.MaxRolls = Mathf.Clamp(rule.MaxRolls + Mathf.RoundToInt(delta), 0, 64);
                    if (rule.MaxRolls < rule.MinRolls) rule.MinRolls = rule.MaxRolls;
                    break;
                case "maxstacks":
                    rule.MaxStacks = Mathf.Max(0, rule.MaxStacks + Mathf.RoundToInt(delta));
                    break;
                default:
                    return;
            }

            SaveConfig();
            RefreshUi(player);
        }

        [ConsoleCommand("loot.ui.item.select")]
        private void LootUiItemSelectConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 1) return;

            int index;
            if (!int.TryParse(arg.Args[0], out index)) return;

            var key = EnsureUiSelectedRule(player);
            var rule = GetOrCreateRule(key);
            if (rule.Items == null || rule.Items.Count == 0) return;

            index = Mathf.Clamp(index, 0, rule.Items.Count - 1);
            uiSelectedItemIndex[player.userID] = index;
            RefreshUi(player);
        }

        [ConsoleCommand("loot.ui.item.addheld")]
        private void LootUiItemAddHeldConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;

            var activeItem = player.GetActiveItem();
            if (activeItem == null || activeItem.info == null)
            {
                SendReply(player, "Hold an item in hands first.");
                return;
            }

            var key = EnsureUiSelectedRule(player);
            var rule = GetOrCreateRule(key);
            var amount = Mathf.Max(1, activeItem.amount);

            rule.Items.Add(new LootItemEntry
            {
                ShortName = activeItem.info.shortname,
                MinAmount = 1,
                MaxAmount = amount,
                Chance = 0.5f,
                Weight = 1f,
                Skin = activeItem.skin
            });

            uiSelectedItemIndex[player.userID] = rule.Items.Count - 1;
            SaveConfig();
            RefreshUi(player);
        }

        [ConsoleCommand("loot.ui.item.remove")]
        private void LootUiItemRemoveConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;

            var key = EnsureUiSelectedRule(player);
            var rule = GetOrCreateRule(key);
            var index = GetSelectedItemIndex(player, rule);
            if (index < 0 || index >= rule.Items.Count) return;

            rule.Items.RemoveAt(index);
            if (rule.Items.Count == 0)
            {
                uiSelectedItemIndex[player.userID] = -1;
            }
            else if (index >= rule.Items.Count)
            {
                uiSelectedItemIndex[player.userID] = rule.Items.Count - 1;
            }

            SaveConfig();
            RefreshUi(player);
        }

        [ConsoleCommand("loot.ui.item.adjust")]
        private void LootUiItemAdjustConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 2) return;

            var field = arg.Args[0].ToLowerInvariant();
            float delta;
            if (!TryParseFloat(arg.Args[1], out delta)) return;

            var key = EnsureUiSelectedRule(player);
            var rule = GetOrCreateRule(key);
            var index = GetSelectedItemIndex(player, rule);
            if (index < 0 || index >= rule.Items.Count) return;

            var item = rule.Items[index];
            if (item == null) return;

            switch (field)
            {
                case "min":
                    item.MinAmount = Mathf.Max(1, item.MinAmount + Mathf.RoundToInt(delta));
                    if (item.MaxAmount < item.MinAmount) item.MaxAmount = item.MinAmount;
                    break;
                case "max":
                    item.MaxAmount = Mathf.Max(1, item.MaxAmount + Mathf.RoundToInt(delta));
                    if (item.MaxAmount < item.MinAmount) item.MinAmount = item.MaxAmount;
                    break;
                case "chance":
                    item.Chance = Mathf.Clamp01(item.Chance + delta);
                    break;
                case "weight":
                    item.Weight = Mathf.Max(0.01f, item.Weight + delta);
                    break;
                default:
                    return;
            }

            SaveConfig();
            RefreshUi(player);
        }

        [ConsoleCommand("loot.ui.reroll")]
        private void LootUiRerollConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAccess(player)) return;
            CommandReroll(player);
            RefreshUi(player);
        }

        [ConsoleCommand("loot.respawnall")]
        private void LootRespawnAllConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !HasAccess(player))
            {
                SendReply(player, "No access.");
                return;
            }

            var mode = (arg.Args != null && arg.Args.Length >= 1) ? arg.Args[0] : null;
            CommandRespawnAll(player, mode);
            if (player != null)
            {
                RefreshUi(player);
            }
        }

        [ConsoleCommand("loot.catalog.export")]
        private void LootCatalogExportConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !HasAccess(player))
            {
                return;
            }

            ExportCatalog(player);
        }

        private ContainerRule GetOrCreateRule(string key)
        {
            key = NormalizeKey(key);
            ContainerRule rule;
            if (!config.Rules.TryGetValue(key, out rule) || rule == null)
            {
                rule = new ContainerRule();
                config.Rules[key] = rule;
            }

            if (rule.Items == null)
            {
                rule.Items = new List<LootItemEntry>();
            }

            return rule;
        }

        private static bool TryParseOnOff(string value, out bool result)
        {
            result = false;
            if (string.IsNullOrWhiteSpace(value)) return false;

            switch (value.Trim().ToLowerInvariant())
            {
                case "on":
                case "1":
                case "true":
                case "yes":
                    result = true;
                    return true;
                case "off":
                case "0":
                case "false":
                case "no":
                    result = false;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseFloat(string value, out float result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = 0f;
                return false;
            }

            value = value.Trim().Replace(",", ".");
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static LootContainer FindLookAtLootContainer(BasePlayer player, float maxDistance)
        {
            if (player == null || player.eyes == null)
            {
                return null;
            }

            RaycastHit hit;
            var ray = player.eyes.HeadRay();
            if (!Physics.Raycast(ray, out hit, maxDistance))
            {
                return null;
            }

            var entity = hit.GetEntity();
            return entity as LootContainer;
        }

        private void ExportCatalog(BasePlayer requester)
        {
            var catalog = new CatalogData
            {
                GeneratedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            foreach (var definition in ItemManager.itemList)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.shortname))
                {
                    continue;
                }

                var displayName = definition.displayName?.english ?? string.Empty;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = definition.shortname;
                }

                catalog.Items.Add(new CatalogItemEntry
                {
                    ShortName = definition.shortname,
                    DisplayName = displayName,
                    Category = definition.category.ToString(),
                    StackSize = definition.stackable
                });
            }

            catalog.Items.Sort((a, b) => string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase));

            var seenContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var observedByContainerKey = new Dictionary<string, ObservedContainerStat>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var lootContainer = entity as LootContainer;
                if (lootContainer == null || lootContainer.IsDestroyed)
                {
                    continue;
                }

                var shortName = NormalizeKey(lootContainer.ShortPrefabName);
                var prefabName = lootContainer.PrefabName ?? string.Empty;

                var dedupeKey = shortName + "|" + prefabName;
                if (!seenContainers.Add(dedupeKey))
                {
                    continue;
                }

                catalog.Containers.Add(new CatalogContainerEntry
                {
                    ShortPrefabName = shortName,
                    PrefabName = prefabName
                });

                var containerRuleKey = GetContainerRuleKey(lootContainer);
                if (string.IsNullOrEmpty(containerRuleKey))
                {
                    continue;
                }

                ObservedContainerStat containerStat;
                if (!observedByContainerKey.TryGetValue(containerRuleKey, out containerStat))
                {
                    containerStat = new ObservedContainerStat();
                    observedByContainerKey[containerRuleKey] = containerStat;
                }

                containerStat.SampleCount++;

                var stackCount = lootContainer.inventory?.itemList?.Count ?? 0;
                if (stackCount < containerStat.MinStacks) containerStat.MinStacks = stackCount;
                if (stackCount > containerStat.MaxStacks) containerStat.MaxStacks = stackCount;

                if (lootContainer.inventory == null || lootContainer.inventory.itemList == null)
                {
                    continue;
                }

                var seenItemsInThisContainer = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in lootContainer.inventory.itemList)
                {
                    if (item == null || item.info == null || string.IsNullOrWhiteSpace(item.info.shortname))
                    {
                        continue;
                    }

                    var itemShortName = NormalizeKey(item.info.shortname);
                    if (string.IsNullOrEmpty(itemShortName))
                    {
                        continue;
                    }

                    ObservedItemStat itemStat;
                    if (!containerStat.Items.TryGetValue(itemShortName, out itemStat))
                    {
                        itemStat = new ObservedItemStat();
                        containerStat.Items[itemShortName] = itemStat;
                    }

                    if (item.amount < itemStat.MinAmount) itemStat.MinAmount = item.amount;
                    if (item.amount > itemStat.MaxAmount) itemStat.MaxAmount = item.amount;
                    if (seenItemsInThisContainer.Add(itemShortName))
                    {
                        itemStat.ContainerOccurrences++;
                    }
                }
            }

            catalog.Containers.Sort((a, b) => string.Compare(a.ShortPrefabName, b.ShortPrefabName, StringComparison.OrdinalIgnoreCase));

            foreach (var pair in observedByContainerKey)
            {
                var key = pair.Key;
                var stat = pair.Value;
                if (stat == null || stat.SampleCount <= 0) continue;

                var minRolls = stat.MinStacks == int.MaxValue ? 0 : Mathf.Max(0, stat.MinStacks);
                var maxRolls = Mathf.Max(minRolls, stat.MaxStacks);

                var rule = new ContainerRule
                {
                    Enabled = true,
                    OverrideDefaultLoot = true,
                    MinRolls = minRolls,
                    MaxRolls = maxRolls,
                    AllowDuplicates = true,
                    ForceAtLeastOneItem = true,
                    MaxStacks = 0,
                    Items = new List<LootItemEntry>()
                };

                foreach (var itemPair in stat.Items)
                {
                    var itemShortName = itemPair.Key;
                    var itemStat = itemPair.Value;
                    if (itemStat == null) continue;
                    if (string.IsNullOrWhiteSpace(itemShortName)) continue;
                    if (itemStat.MaxAmount <= 0) continue;

                    var minAmount = itemStat.MinAmount == int.MaxValue ? 1 : Mathf.Max(1, itemStat.MinAmount);
                    var maxAmount = Mathf.Max(minAmount, itemStat.MaxAmount);
                    var chance = Mathf.Clamp01((float)itemStat.ContainerOccurrences / stat.SampleCount);

                    rule.Items.Add(new LootItemEntry
                    {
                        ShortName = itemShortName,
                        MinAmount = minAmount,
                        MaxAmount = maxAmount,
                        Chance = chance,
                        Weight = 1f,
                        Skin = 0
                    });
                }

                rule.Items.Sort((a, b) => string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase));

                catalog.ObservedRulesByContainerKey[key] = rule;
                catalog.ObservedSampleCountByContainerKey[key] = stat.SampleCount;
            }

            Interface.Oxide.DataFileSystem.WriteObject("ContainerLootCatalog", catalog);

            var message = $"Exported catalog to oxide/data/ContainerLootCatalog.json (items: {catalog.Items.Count}, containers: {catalog.Containers.Count}, observed rules: {catalog.ObservedRulesByContainerKey.Count}).";
            if (requester != null)
            {
                SendReply(requester, message);
            }
            Puts(message);
        }

        private static string GetContainerRuleKey(LootContainer lootContainer)
        {
            if (lootContainer == null)
            {
                return string.Empty;
            }

            var key = NormalizeKey(lootContainer.ShortPrefabName);
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }

            key = GetPrefabFileKey(lootContainer);
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }

            return NormalizeKey(lootContainer.PrefabName);
        }
    }
}
