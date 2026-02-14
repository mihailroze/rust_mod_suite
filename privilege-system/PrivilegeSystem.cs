using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("PrivilegeSystem", "Shmatko", "0.9.1")]
    [Description("Timed rank/privilege management for Rust servers (VIP, Premium, Elite).")]
    public class PrivilegeSystem : RustPlugin
    {
        private const string PermissionAdmin = "privilegesystem.admin";
        private const string DataFileName = "PrivilegeSystem_Data";
        private const string UiRoot = "PrivilegeSystem.UI.Root";
        private const string RemoveModeUiRoot = "PrivilegeSystem.UI.RemoveMode";
        private const float RemoveMaxDistanceMeters = 6f;
        private const float PersonalRecyclerBaseTickSeconds = 5f;
        private const float PersonalRecyclerLifetimeSeconds = 300f;
        private const float PersonalRecyclerHiddenOffsetY = -8f;
        private const int RecyclerInputSlotCount = 6;
        private const int RecyclerOutputStartSlot = RecyclerInputSlotCount;

        [PluginReference] private Plugin Economics;
        [PluginReference] private Plugin ServerRewards;

        private ConfigData config;
        private StoredData storedData;
        private DynamicConfigFile dataFile;
        private Timer expiryTimer;
        private Timer delayedReapplyTimer;
        private Timer webShopPollTimer;
        private bool webShopPollInProgress;
        private readonly HashSet<ulong> webShopPlayerActivationInProgress = new HashSet<ulong>();
        private readonly HashSet<string> warnedMissingPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<ulong> uiViewers = new HashSet<ulong>();
        private readonly Dictionary<ulong, ulong> uiSelectedTarget = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, string> uiSelectedRank = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, string> uiActiveTab = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, HashSet<ulong>> containerLootBonusClaimed = new Dictionary<ulong, HashSet<ulong>>();
        private readonly Dictionary<ulong, ulong> barrelLastAttacker = new Dictionary<ulong, ulong>();
        private readonly HashSet<ulong> barrelLootAdjusted = new HashSet<ulong>();
        private readonly Dictionary<ulong, PendingHomeTeleport> pendingHomeTeleports = new Dictionary<ulong, PendingHomeTeleport>();
        private readonly HashSet<ulong> removeModePlayers = new HashSet<ulong>();
        private readonly Dictionary<ulong, Timer> removeModeTimers = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, Timer> removeModeUiTimers = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, long> removeModeUiEndsUnix = new Dictionary<ulong, long>();
        private readonly Dictionary<ulong, Recycler> personalRecyclerByPlayer = new Dictionary<ulong, Recycler>();
        private readonly Dictionary<ulong, ulong> personalRecyclerOwnerByEntityId = new Dictionary<ulong, ulong>();
        private readonly Dictionary<ulong, Timer> personalRecyclerLifetimeTimers = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, Timer> personalRecyclerSpeedTimers = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, Timer> personalRecyclerCloseWatchTimers = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, long> personalRecyclerCloseGuardUntilUnix = new Dictionary<ulong, long>();
        private readonly Dictionary<ulong, float> personalRecyclerWorkSoundNextTime = new Dictionary<ulong, float>();
        private MethodInfo recyclerThinkMethod;

        private class KitItemEntry
        {
            [JsonProperty("Shortname")]
            public string ShortName = "scrap";

            [JsonProperty("Amount")]
            public int Amount = 100;
        }

        private class RankDefinition
        {
            [JsonProperty("Display name")]
            public string DisplayName = "VIP";

            [JsonProperty("Chat tag")]
            public string ChatTag = "[VIP]";

            [JsonProperty("Chat color")]
            public string ChatColor = "#f4c542";

            [JsonProperty("Oxide group (optional)")]
            public string OxideGroup = "vip";

            [JsonProperty("Permissions")]
            public List<string> Permissions = new List<string>();

            [JsonProperty("Allow teleport")]
            public bool AllowTeleport = false;

            [JsonProperty("Home points")]
            public int HomePoints = 0;

            [JsonProperty("Allow pocket recycler")]
            public bool AllowPocketRecycler = false;

            [JsonProperty("Pocket recycler speed multiplier")]
            public float PocketRecyclerSpeedMultiplier = 1f;

            [JsonProperty("Pocket recycler output multiplier")]
            public float PocketRecyclerOutputMultiplier = 1f;

            [JsonProperty("Allow remove command")]
            public bool AllowRemoveCommand = false;

            [JsonProperty("Daily reward multiplier")]
            public float DailyRewardMultiplier = 1f;

            [JsonProperty("Home teleport cooldown reduction (seconds)")]
            public int HomeTeleportCooldownReductionSeconds = 0;

            [JsonProperty("Team teleport cooldown reduction (seconds)")]
            public int TeamTeleportCooldownReductionSeconds = 0;

            [JsonProperty("Town teleport daily limit bonus")]
            public int TownTeleportDailyLimitBonus = 0;

            [JsonProperty("Gather multiplier")]
            public float GatherMultiplier = 1f;

            [JsonProperty("Ground pickup multiplier")]
            public float GroundPickupMultiplier = 1f;

            [JsonProperty("Container loot multiplier")]
            public float ContainerLootMultiplier = 1f;

            [JsonProperty("NPC kill scrap reward")]
            public int NpcKillScrapReward = 0;

            [JsonProperty("Rank kit cooldown seconds")]
            public int RankKitCooldownSeconds = 0;

            [JsonProperty("Rank kit amount multiplier")]
            public float RankKitAmountMultiplier = 1f;

            [JsonProperty("Rank kit items")]
            public List<KitItemEntry> RankKitItems = new List<KitItemEntry>();
        }

        private class FeaturePermissionsConfig
        {
            [JsonProperty("Teleport permissions")]
            public List<string> TeleportPermissions = new List<string>
            {
                "nteleportation.tp",
                "nteleportation.tpr"
            };

            [JsonProperty("Home base permissions")]
            public List<string> HomeBasePermissions = new List<string>
            {
                "nteleportation.home"
            };

            [JsonProperty("Home limit permission template (use {0} for points, empty = disabled)")]
            public string HomeLimitPermissionTemplate = "nteleportation.home.{0}";

            [JsonProperty("Pocket recycler permissions")]
            public List<string> PocketRecyclerPermissions = new List<string>
            {
                "recycler.use"
            };

            [JsonProperty("Remove command permissions")]
            public List<string> RemoveCommandPermissions = new List<string>
            {
                "removertool.remove"
            };
        }

        private class DailyRewardsConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Cooldown seconds")]
            public int CooldownSeconds = 86400;

            [JsonProperty("Allow without active rank")]
            public bool AllowWithoutRank = false;

            [JsonProperty("Base items")]
            public List<KitItemEntry> BaseItems = new List<KitItemEntry>
            {
                new KitItemEntry { ShortName = "scrap", Amount = 150 },
                new KitItemEntry { ShortName = "cloth", Amount = 100 },
                new KitItemEntry { ShortName = "lowgradefuel", Amount = 40 }
            };
        }

        private class TeleportFeaturesConfig
        {
            [JsonProperty("Home command template (use {0} for home name)")]
            public string HomeCommandTemplate = "home {0}";

            [JsonProperty("Town command")]
            public string TownCommand = "town";

            [JsonProperty("Team command template (use {0} for target name/id)")]
            public string TeamCommandTemplate = "tpr {0}";

            [JsonProperty("Home base cooldown seconds")]
            public int HomeBaseCooldownSeconds = 30;

            [JsonProperty("Home activation base delay seconds")]
            public int HomeActivationBaseDelaySeconds = 15;

            [JsonProperty("Home points without privilege")]
            public int HomePointsWithoutPrivilege = 1;

            [JsonProperty("Remove mode duration seconds")]
            public int RemoveModeDurationSeconds = 30;

            [JsonProperty("Pocket recycler speed multiplier")]
            public float PocketRecyclerSpeedMultiplier = 1f;

            [JsonProperty("Pocket recycler command cooldown seconds")]
            public int PocketRecyclerCommandCooldownSeconds = 10;

            [JsonProperty("Pocket recycler auto close on menu close")]
            public bool PocketRecyclerAutoCloseOnMenuClose = true;

            [JsonProperty("Pocket recycler local sounds enabled")]
            public bool PocketRecyclerLocalSoundsEnabled = true;

            [JsonProperty("Pocket recycler open sound effect prefab")]
            public string PocketRecyclerOpenSoundEffectPrefab = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab";

            [JsonProperty("Pocket recycler close sound effect prefab")]
            public string PocketRecyclerCloseSoundEffectPrefab = "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab";

            [JsonProperty("Pocket recycler working sound effect prefab")]
            public string PocketRecyclerWorkingSoundEffectPrefab = "assets/bundled/prefabs/fx/notice/item.select.fx.prefab";

            [JsonProperty("Pocket recycler working sound interval seconds")]
            public float PocketRecyclerWorkingSoundIntervalSeconds = 1.2f;

            [JsonProperty("Team base cooldown seconds")]
            public int TeamBaseCooldownSeconds = 15;

            [JsonProperty("Town base daily limit")]
            public int TownBaseDailyLimit = 10;

            [JsonProperty("Show debug replies")]
            public bool ShowDebugReplies = false;
        }

        private class ShopPackageConfig
        {
            [JsonProperty("Display name")]
            public string DisplayName = "VIP 30d";

            [JsonProperty("Rank")]
            public string Rank = "vip";

            [JsonProperty("Days (0 = permanent)")]
            public int Days = 30;

            [JsonProperty("Economics price")]
            public double EconomicsPrice = 500d;

            [JsonProperty("ServerRewards price")]
            public int ServerRewardsPrice = 0;
        }

        private class ShopConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Currency (economics/serverrewards)")]
            public string Currency = "economics";

            [JsonProperty("Packages")]
            public Dictionary<string, ShopPackageConfig> Packages = new Dictionary<string, ShopPackageConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["vip_30d"] = new ShopPackageConfig
                {
                    DisplayName = "VIP 30d",
                    Rank = "vip",
                    Days = 30,
                    EconomicsPrice = 500d,
                    ServerRewardsPrice = 500
                },
                ["premium_30d"] = new ShopPackageConfig
                {
                    DisplayName = "Premium 30d",
                    Rank = "premium",
                    Days = 30,
                    EconomicsPrice = 1200d,
                    ServerRewardsPrice = 1200
                },
                ["elite_30d"] = new ShopPackageConfig
                {
                    DisplayName = "Elite 30d",
                    Rank = "elite",
                    Days = 30,
                    EconomicsPrice = 2500d,
                    ServerRewardsPrice = 2500
                }
            };
        }

        private class AuditConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("Max entries")]
            public int MaxEntries = 500;

            [JsonProperty("Echo to console")]
            public bool EchoToConsole = true;
        }

        private class WebShopBridgeConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = false;

            [JsonProperty("Api base url")]
            public string ApiBaseUrl = "http://127.0.0.1:8001/api/v1";

            [JsonProperty("Server id")]
            public string ServerId = "local-rust-1";

            [JsonProperty("Server key")]
            public string ServerKey = "change-me-server-key";

            [JsonProperty("Poll interval seconds")]
            public float PollIntervalSeconds = 10f;

            [JsonProperty("Batch size")]
            public int BatchSize = 10;

            [JsonProperty("Request timeout seconds")]
            public int RequestTimeoutSeconds = 10;

            [JsonProperty("Grant source label")]
            public string GrantSourceLabel = "WebShop";
        }

        private class WebShopClaimRequest
        {
            [JsonProperty("server_id")]
            public string ServerId = string.Empty;

            [JsonProperty("limit")]
            public int Limit = 10;

            [JsonProperty("steam_id")]
            public ulong? SteamId = null;
        }

        private class WebShopClaimOrder
        {
            [JsonProperty("order_id")]
            public string OrderId = string.Empty;

            [JsonProperty("steam_id")]
            public ulong SteamId;

            [JsonProperty("buyer_name")]
            public string BuyerName = string.Empty;

            [JsonProperty("package_key")]
            public string PackageKey = string.Empty;

            [JsonProperty("rank")]
            public string Rank = string.Empty;

            [JsonProperty("days")]
            public int Days;

            [JsonProperty("source")]
            public string Source = string.Empty;
        }

        private class WebShopClaimResponse
        {
            [JsonProperty("server_id")]
            public string ServerId = string.Empty;

            [JsonProperty("count")]
            public int Count;

            [JsonProperty("orders")]
            public List<WebShopClaimOrder> Orders = new List<WebShopClaimOrder>();
        }

        private class WebShopCompleteRequest
        {
            [JsonProperty("server_id")]
            public string ServerId = string.Empty;

            [JsonProperty("order_id")]
            public string OrderId = string.Empty;

            [JsonProperty("success")]
            public bool Success;

            [JsonProperty("message")]
            public string Message = string.Empty;
        }

        private class ConfigData
        {
            [JsonProperty("Notify player on connect")]
            public bool NotifyOnConnect = true;

            [JsonProperty("Expiry check interval (seconds)")]
            public float ExpiryCheckIntervalSeconds = 60f;

            [JsonProperty("Feature permissions")]
            public FeaturePermissionsConfig FeaturePermissions = new FeaturePermissionsConfig();

            [JsonProperty("Daily rewards")]
            public DailyRewardsConfig DailyRewards = new DailyRewardsConfig();

            [JsonProperty("Teleport features")]
            public TeleportFeaturesConfig TeleportFeatures = new TeleportFeaturesConfig();

            [JsonProperty("Shop")]
            public ShopConfig Shop = new ShopConfig();

            [JsonProperty("Audit")]
            public AuditConfig Audit = new AuditConfig();

            [JsonProperty("Web shop bridge")]
            public WebShopBridgeConfig WebShopBridge = new WebShopBridgeConfig();

            [JsonProperty("Ranks")]
            public Dictionary<string, RankDefinition> Ranks = new Dictionary<string, RankDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["vip"] = new RankDefinition
                {
                    DisplayName = "VIP",
                    ChatTag = "[VIP]",
                    ChatColor = "#f4c542",
                    OxideGroup = "vip",
                    GatherMultiplier = 1.3f,
                    GroundPickupMultiplier = 1.2f,
                    ContainerLootMultiplier = 1.15f,
                    AllowTeleport = true,
                    HomePoints = 1,
                    PocketRecyclerSpeedMultiplier = 1.25f,
                    PocketRecyclerOutputMultiplier = 1.3f,
                    DailyRewardMultiplier = 1.2f,
                    HomeTeleportCooldownReductionSeconds = 5,
                    TeamTeleportCooldownReductionSeconds = 3,
                    TownTeleportDailyLimitBonus = 2,
                    NpcKillScrapReward = 3,
                    RankKitCooldownSeconds = 86400,
                    RankKitAmountMultiplier = 1.0f,
                    RankKitItems = new List<KitItemEntry>
                    {
                        new KitItemEntry { ShortName = "scrap", Amount = 300 },
                        new KitItemEntry { ShortName = "metal.fragments", Amount = 1000 },
                        new KitItemEntry { ShortName = "lowgradefuel", Amount = 120 }
                    },
                    Permissions = new List<string>
                    {
                        "privilegesystem.rank.vip"
                    }
                },
                ["premium"] = new RankDefinition
                {
                    DisplayName = "Premium",
                    ChatTag = "[PREMIUM]",
                    ChatColor = "#6ed3ff",
                    OxideGroup = "premium",
                    GatherMultiplier = 1.6f,
                    GroundPickupMultiplier = 1.4f,
                    ContainerLootMultiplier = 1.3f,
                    AllowTeleport = true,
                    HomePoints = 2,
                    AllowPocketRecycler = true,
                    PocketRecyclerSpeedMultiplier = 1.75f,
                    PocketRecyclerOutputMultiplier = 1.6f,
                    DailyRewardMultiplier = 1.5f,
                    HomeTeleportCooldownReductionSeconds = 10,
                    TeamTeleportCooldownReductionSeconds = 6,
                    TownTeleportDailyLimitBonus = 5,
                    NpcKillScrapReward = 6,
                    RankKitCooldownSeconds = 72000,
                    RankKitAmountMultiplier = 1.2f,
                    RankKitItems = new List<KitItemEntry>
                    {
                        new KitItemEntry { ShortName = "scrap", Amount = 600 },
                        new KitItemEntry { ShortName = "metal.fragments", Amount = 2500 },
                        new KitItemEntry { ShortName = "lowgradefuel", Amount = 250 },
                        new KitItemEntry { ShortName = "metal.refined", Amount = 20 }
                    },
                    Permissions = new List<string>
                    {
                        "privilegesystem.rank.vip",
                        "privilegesystem.rank.premium"
                    }
                },
                ["elite"] = new RankDefinition
                {
                    DisplayName = "Elite",
                    ChatTag = "[ELITE]",
                    ChatColor = "#ff9f43",
                    OxideGroup = "elite",
                    GatherMultiplier = 2.0f,
                    GroundPickupMultiplier = 1.8f,
                    ContainerLootMultiplier = 1.5f,
                    AllowTeleport = true,
                    HomePoints = 3,
                    AllowPocketRecycler = true,
                    PocketRecyclerSpeedMultiplier = 2.5f,
                    PocketRecyclerOutputMultiplier = 2f,
                    AllowRemoveCommand = true,
                    DailyRewardMultiplier = 2.0f,
                    HomeTeleportCooldownReductionSeconds = 15,
                    TeamTeleportCooldownReductionSeconds = 9,
                    TownTeleportDailyLimitBonus = 10,
                    NpcKillScrapReward = 10,
                    RankKitCooldownSeconds = 57600,
                    RankKitAmountMultiplier = 1.4f,
                    RankKitItems = new List<KitItemEntry>
                    {
                        new KitItemEntry { ShortName = "scrap", Amount = 1000 },
                        new KitItemEntry { ShortName = "metal.fragments", Amount = 4000 },
                        new KitItemEntry { ShortName = "lowgradefuel", Amount = 400 },
                        new KitItemEntry { ShortName = "metal.refined", Amount = 40 },
                        new KitItemEntry { ShortName = "cloth", Amount = 500 }
                    },
                    Permissions = new List<string>
                    {
                        "privilegesystem.rank.vip",
                        "privilegesystem.rank.premium",
                        "privilegesystem.rank.elite"
                    }
                }
            };
        }

        private class PrivilegeRecord
        {
            [JsonProperty("Rank")]
            public string Rank = string.Empty;

            [JsonProperty("Expires at unix (0 = permanent)")]
            public long ExpiresAtUnix;

            [JsonProperty("Granted at unix")]
            public long GrantedAtUnix;

            [JsonProperty("Granted by")]
            public string GrantedBy = "System";

            [JsonProperty("Last known name")]
            public string LastKnownName = string.Empty;
        }

        private class TeleportPoint
        {
            [JsonProperty("X")]
            public float X;

            [JsonProperty("Y")]
            public float Y;

            [JsonProperty("Z")]
            public float Z;

            public TeleportPoint()
            {
            }

            public TeleportPoint(Vector3 value)
            {
                X = value.x;
                Y = value.y;
                Z = value.z;
            }

            public Vector3 ToVector3()
            {
                return new Vector3(X, Y, Z);
            }
        }

        private class HomePointEntry
        {
            [JsonProperty("Point")]
            public TeleportPoint Point = new TeleportPoint();

            [JsonProperty("Updated at unix")]
            public long UpdatedAtUnix;
        }

        private class StoredData
        {
            [JsonProperty("Players")]
            public Dictionary<string, PrivilegeRecord> Players = new Dictionary<string, PrivilegeRecord>();

            [JsonProperty("Rank kit next claim unix by user")]
            public Dictionary<string, long> RankKitNextClaimUnix = new Dictionary<string, long>();

            [JsonProperty("Daily next claim unix by user")]
            public Dictionary<string, long> DailyNextClaimUnix = new Dictionary<string, long>();

            [JsonProperty("Home tp next use unix by user")]
            public Dictionary<string, long> HomeTpNextUseUnix = new Dictionary<string, long>();

            [JsonProperty("Team tp next use unix by user")]
            public Dictionary<string, long> TeamTpNextUseUnix = new Dictionary<string, long>();

            [JsonProperty("Town tp usage by user")]
            public Dictionary<string, TownTpUsageEntry> TownTpUsageByUser = new Dictionary<string, TownTpUsageEntry>();

            [JsonProperty("Pocket recycler next use unix by user")]
            public Dictionary<string, long> PocketRecyclerNextUseUnix = new Dictionary<string, long>();

            [JsonProperty("Homes by user")]
            public Dictionary<string, Dictionary<string, HomePointEntry>> HomesByUser = new Dictionary<string, Dictionary<string, HomePointEntry>>();

            [JsonProperty("Town teleport point")]
            public TeleportPoint TownTeleportPoint = null;

            [JsonProperty("Audit log")]
            public List<AuditLogEntry> AuditLog = new List<AuditLogEntry>();
        }

        private class TownTpUsageEntry
        {
            [JsonProperty("Utc day key")]
            public string UtcDayKey = string.Empty;

            [JsonProperty("Used")]
            public int Used = 0;
        }

        private class PendingHomeTeleport
        {
            public Timer Timer;
            public long ExecuteAtUnix;
            public string HomeName = string.Empty;
            public TeleportPoint Destination = null;
        }

        private class AuditLogEntry
        {
            [JsonProperty("Unix")]
            public long Unix;

            [JsonProperty("Action")]
            public string Action = string.Empty;

            [JsonProperty("Actor id")]
            public ulong ActorId;

            [JsonProperty("Actor name")]
            public string ActorName = string.Empty;

            [JsonProperty("Target id")]
            public ulong TargetId;

            [JsonProperty("Target name")]
            public string TargetName = string.Empty;

            [JsonProperty("Details")]
            public string Details = string.Empty;
        }

        private void Init()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            dataFile = Interface.Oxide.DataFileSystem.GetFile(DataFileName);
            LoadData();
            RegisterConfigPermissions();
            recyclerThinkMethod = typeof(Recycler).GetMethod("RecycleThink", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private void OnServerInitialized()
        {
            CleanupExpired(false);
            ReapplyPrivileges();
            StartExpiryTimer();
            StartWebShopPollTimer();
        }

        private void Unload()
        {
            expiryTimer?.Destroy();
            expiryTimer = null;
            delayedReapplyTimer?.Destroy();
            delayedReapplyTimer = null;
            webShopPollTimer?.Destroy();
            webShopPollTimer = null;
            webShopPollInProgress = false;

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyAdminUi(player);
                CuiHelper.DestroyUi(player, RemoveModeUiRoot);
            }

            uiViewers.Clear();
            uiSelectedTarget.Clear();
            uiSelectedRank.Clear();
            uiActiveTab.Clear();
            containerLootBonusClaimed.Clear();
            barrelLastAttacker.Clear();
            barrelLootAdjusted.Clear();
            foreach (var pending in pendingHomeTeleports.Values)
            {
                pending?.Timer?.Destroy();
            }
            pendingHomeTeleports.Clear();
            foreach (var modeTimer in removeModeTimers.Values)
            {
                modeTimer?.Destroy();
            }
            removeModeTimers.Clear();
            foreach (var removeUiTimer in removeModeUiTimers.Values)
            {
                removeUiTimer?.Destroy();
            }
            removeModeUiTimers.Clear();
            removeModeUiEndsUnix.Clear();
            removeModePlayers.Clear();

             foreach (var timerEntry in personalRecyclerLifetimeTimers.Values)
             {
                 timerEntry?.Destroy();
             }
             personalRecyclerLifetimeTimers.Clear();
             foreach (var speedTimer in personalRecyclerSpeedTimers.Values)
             {
                 speedTimer?.Destroy();
             }
             personalRecyclerSpeedTimers.Clear();
             foreach (var watchTimer in personalRecyclerCloseWatchTimers.Values)
             {
                 watchTimer?.Destroy();
             }
             personalRecyclerCloseWatchTimers.Clear();
             foreach (var recycler in personalRecyclerByPlayer.Values)
             {
                 if (recycler != null && !recycler.IsDestroyed)
                 {
                     recycler.Kill();
                 }
             }
             personalRecyclerByPlayer.Clear();
             personalRecyclerOwnerByEntityId.Clear();
             personalRecyclerCloseGuardUntilUnix.Clear();
             personalRecyclerWorkSoundNextTime.Clear();
            SaveData();
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
            if (config.ExpiryCheckIntervalSeconds < 15f)
            {
                config.ExpiryCheckIntervalSeconds = 15f;
            }

            if (config.FeaturePermissions == null)
            {
                config.FeaturePermissions = new FeaturePermissionsConfig();
            }

            config.FeaturePermissions.TeleportPermissions = NormalizePermissionList(config.FeaturePermissions.TeleportPermissions);
            config.FeaturePermissions.HomeBasePermissions = NormalizePermissionList(config.FeaturePermissions.HomeBasePermissions);
            config.FeaturePermissions.PocketRecyclerPermissions = NormalizePermissionList(config.FeaturePermissions.PocketRecyclerPermissions);
            config.FeaturePermissions.RemoveCommandPermissions = NormalizePermissionList(config.FeaturePermissions.RemoveCommandPermissions);
            config.FeaturePermissions.HomeLimitPermissionTemplate =
                string.IsNullOrWhiteSpace(config.FeaturePermissions.HomeLimitPermissionTemplate)
                    ? string.Empty
                    : config.FeaturePermissions.HomeLimitPermissionTemplate.Trim();

            if (config.DailyRewards == null)
            {
                config.DailyRewards = new DailyRewardsConfig();
            }
            if (config.DailyRewards.CooldownSeconds < 60)
            {
                config.DailyRewards.CooldownSeconds = 60;
            }
            if (config.DailyRewards.CooldownSeconds > 1000000000)
            {
                config.DailyRewards.CooldownSeconds = 1000000000;
            }
            if (config.DailyRewards.BaseItems == null)
            {
                config.DailyRewards.BaseItems = new List<KitItemEntry>();
            }
            config.DailyRewards.BaseItems.RemoveAll(item =>
                item == null ||
                string.IsNullOrWhiteSpace(item.ShortName) ||
                item.Amount <= 0);
            if (config.DailyRewards.BaseItems.Count == 0)
            {
                config.DailyRewards.BaseItems = new DailyRewardsConfig().BaseItems;
            }

            if (config.TeleportFeatures == null)
            {
                config.TeleportFeatures = new TeleportFeaturesConfig();
            }
            config.TeleportFeatures.HomeCommandTemplate =
                string.IsNullOrWhiteSpace(config.TeleportFeatures.HomeCommandTemplate)
                    ? "home {0}"
                    : config.TeleportFeatures.HomeCommandTemplate.Trim();
            config.TeleportFeatures.TownCommand =
                string.IsNullOrWhiteSpace(config.TeleportFeatures.TownCommand)
                    ? "town"
                    : config.TeleportFeatures.TownCommand.Trim();
            config.TeleportFeatures.TeamCommandTemplate =
                string.IsNullOrWhiteSpace(config.TeleportFeatures.TeamCommandTemplate)
                    ? "tpr {0}"
                    : config.TeleportFeatures.TeamCommandTemplate.Trim();
            if (config.TeleportFeatures.HomeBaseCooldownSeconds < 0)
            {
                config.TeleportFeatures.HomeBaseCooldownSeconds = 0;
            }
            if (config.TeleportFeatures.HomeActivationBaseDelaySeconds < 0)
            {
                config.TeleportFeatures.HomeActivationBaseDelaySeconds = 0;
            }
            if (config.TeleportFeatures.HomeActivationBaseDelaySeconds > 60)
            {
                config.TeleportFeatures.HomeActivationBaseDelaySeconds = 60;
            }
            if (config.TeleportFeatures.HomePointsWithoutPrivilege < 0)
            {
                config.TeleportFeatures.HomePointsWithoutPrivilege = 0;
            }
            if (config.TeleportFeatures.HomePointsWithoutPrivilege > 20)
            {
                config.TeleportFeatures.HomePointsWithoutPrivilege = 20;
            }
            if (config.TeleportFeatures.RemoveModeDurationSeconds < 5)
            {
                config.TeleportFeatures.RemoveModeDurationSeconds = 5;
            }
            if (config.TeleportFeatures.RemoveModeDurationSeconds > 600)
            {
                config.TeleportFeatures.RemoveModeDurationSeconds = 600;
            }
            if (config.TeleportFeatures.PocketRecyclerSpeedMultiplier < 1f)
            {
                config.TeleportFeatures.PocketRecyclerSpeedMultiplier = 1f;
            }
            if (config.TeleportFeatures.PocketRecyclerSpeedMultiplier > 20f)
            {
                config.TeleportFeatures.PocketRecyclerSpeedMultiplier = 20f;
            }
            config.TeleportFeatures.PocketRecyclerOpenSoundEffectPrefab =
                string.IsNullOrWhiteSpace(config.TeleportFeatures.PocketRecyclerOpenSoundEffectPrefab)
                    ? string.Empty
                    : config.TeleportFeatures.PocketRecyclerOpenSoundEffectPrefab.Trim();
            config.TeleportFeatures.PocketRecyclerCloseSoundEffectPrefab =
                string.IsNullOrWhiteSpace(config.TeleportFeatures.PocketRecyclerCloseSoundEffectPrefab)
                    ? string.Empty
                    : config.TeleportFeatures.PocketRecyclerCloseSoundEffectPrefab.Trim();
            config.TeleportFeatures.PocketRecyclerWorkingSoundEffectPrefab =
                string.IsNullOrWhiteSpace(config.TeleportFeatures.PocketRecyclerWorkingSoundEffectPrefab)
                    ? string.Empty
                    : config.TeleportFeatures.PocketRecyclerWorkingSoundEffectPrefab.Trim();
            if (config.TeleportFeatures.PocketRecyclerWorkingSoundIntervalSeconds < 0.1f)
            {
                config.TeleportFeatures.PocketRecyclerWorkingSoundIntervalSeconds = 0.1f;
            }
            if (config.TeleportFeatures.PocketRecyclerWorkingSoundIntervalSeconds > 10f)
            {
                config.TeleportFeatures.PocketRecyclerWorkingSoundIntervalSeconds = 10f;
            }
            if (config.TeleportFeatures.PocketRecyclerCommandCooldownSeconds < 0)
            {
                config.TeleportFeatures.PocketRecyclerCommandCooldownSeconds = 0;
            }
            if (config.TeleportFeatures.PocketRecyclerCommandCooldownSeconds > 600)
            {
                config.TeleportFeatures.PocketRecyclerCommandCooldownSeconds = 600;
            }
            if (config.TeleportFeatures.TeamBaseCooldownSeconds < 0)
            {
                config.TeleportFeatures.TeamBaseCooldownSeconds = 0;
            }
            if (config.TeleportFeatures.TownBaseDailyLimit < 0)
            {
                config.TeleportFeatures.TownBaseDailyLimit = 0;
            }
            if (config.TeleportFeatures.TownBaseDailyLimit > 1000)
            {
                config.TeleportFeatures.TownBaseDailyLimit = 1000;
            }

            if (config.Shop == null)
            {
                config.Shop = new ShopConfig();
            }
            config.Shop.Currency = NormalizeShopCurrency(config.Shop.Currency);
            if (config.Shop.Packages == null || config.Shop.Packages.Count == 0)
            {
                config.Shop.Packages = new ShopConfig().Packages;
            }
            var normalizedPackages = new Dictionary<string, ShopPackageConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in config.Shop.Packages)
            {
                if (pair.Value == null) continue;
                var key = NormalizeRankKey(pair.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;

                var pkg = pair.Value;
                if (string.IsNullOrWhiteSpace(pkg.DisplayName))
                {
                    pkg.DisplayName = key.ToUpperInvariant();
                }
                pkg.Rank = NormalizeRankKey(pkg.Rank ?? string.Empty);
                if (pkg.Days < 0) pkg.Days = 0;
                if (pkg.Days > 3650) pkg.Days = 3650;
                if (pkg.EconomicsPrice < 0d) pkg.EconomicsPrice = 0d;
                if (pkg.ServerRewardsPrice < 0) pkg.ServerRewardsPrice = 0;
                normalizedPackages[key] = pkg;
            }
            if (normalizedPackages.Count == 0)
            {
                normalizedPackages = new ShopConfig().Packages;
            }
            config.Shop.Packages = normalizedPackages;

            if (config.Audit == null)
            {
                config.Audit = new AuditConfig();
            }
            if (config.Audit.MaxEntries < 50)
            {
                config.Audit.MaxEntries = 50;
            }
            if (config.Audit.MaxEntries > 5000)
            {
                config.Audit.MaxEntries = 5000;
            }

            if (config.WebShopBridge == null)
            {
                config.WebShopBridge = new WebShopBridgeConfig();
            }
            config.WebShopBridge.ApiBaseUrl =
                string.IsNullOrWhiteSpace(config.WebShopBridge.ApiBaseUrl)
                    ? "http://127.0.0.1:8001/api/v1"
                    : config.WebShopBridge.ApiBaseUrl.Trim().TrimEnd('/');
            config.WebShopBridge.ServerId =
                string.IsNullOrWhiteSpace(config.WebShopBridge.ServerId)
                    ? "local-rust-1"
                    : config.WebShopBridge.ServerId.Trim();
            config.WebShopBridge.ServerKey =
                string.IsNullOrWhiteSpace(config.WebShopBridge.ServerKey)
                    ? "change-me-server-key"
                    : config.WebShopBridge.ServerKey.Trim();
            config.WebShopBridge.GrantSourceLabel =
                string.IsNullOrWhiteSpace(config.WebShopBridge.GrantSourceLabel)
                    ? "WebShop"
                    : config.WebShopBridge.GrantSourceLabel.Trim();
            if (config.WebShopBridge.PollIntervalSeconds < 2f)
            {
                config.WebShopBridge.PollIntervalSeconds = 2f;
            }
            if (config.WebShopBridge.PollIntervalSeconds > 300f)
            {
                config.WebShopBridge.PollIntervalSeconds = 300f;
            }
            if (config.WebShopBridge.BatchSize < 1)
            {
                config.WebShopBridge.BatchSize = 1;
            }
            if (config.WebShopBridge.BatchSize > 100)
            {
                config.WebShopBridge.BatchSize = 100;
            }
            if (config.WebShopBridge.RequestTimeoutSeconds < 3)
            {
                config.WebShopBridge.RequestTimeoutSeconds = 3;
            }
            if (config.WebShopBridge.RequestTimeoutSeconds > 60)
            {
                config.WebShopBridge.RequestTimeoutSeconds = 60;
            }

            if (config.Ranks == null || config.Ranks.Count == 0)
            {
                config.Ranks = new ConfigData().Ranks;
            }

            var normalized = new Dictionary<string, RankDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in config.Ranks)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) continue;
                var rankKey = NormalizeRankKey(pair.Key);
                var rank = pair.Value;

                if (string.IsNullOrWhiteSpace(rank.DisplayName))
                {
                    rank.DisplayName = rankKey.ToUpperInvariant();
                }

                if (rank.Permissions == null)
                {
                    rank.Permissions = new List<string>();
                }
                rank.Permissions = NormalizePermissionList(rank.Permissions);

                if (rank.HomePoints < 0)
                {
                    rank.HomePoints = 0;
                }
                if (rank.HomePoints > 100)
                {
                    rank.HomePoints = 100;
                }

                if (rank.DailyRewardMultiplier < 0.1f)
                {
                    rank.DailyRewardMultiplier = 0.1f;
                }
                if (rank.DailyRewardMultiplier > 20f)
                {
                    rank.DailyRewardMultiplier = 20f;
                }

                if (rank.HomeTeleportCooldownReductionSeconds < 0)
                {
                    rank.HomeTeleportCooldownReductionSeconds = 0;
                }
                if (rank.HomeTeleportCooldownReductionSeconds > 600)
                {
                    rank.HomeTeleportCooldownReductionSeconds = 600;
                }

                if (rank.TeamTeleportCooldownReductionSeconds < 0)
                {
                    rank.TeamTeleportCooldownReductionSeconds = 0;
                }
                if (rank.TeamTeleportCooldownReductionSeconds > 600)
                {
                    rank.TeamTeleportCooldownReductionSeconds = 600;
                }

                if (rank.TownTeleportDailyLimitBonus < 0)
                {
                    rank.TownTeleportDailyLimitBonus = 0;
                }
                if (rank.TownTeleportDailyLimitBonus > 1000)
                {
                    rank.TownTeleportDailyLimitBonus = 1000;
                }

                if (rank.GatherMultiplier < 1f)
                {
                    rank.GatherMultiplier = 1f;
                }
                if (rank.GatherMultiplier > 10f)
                {
                    rank.GatherMultiplier = 10f;
                }

                if (rank.GroundPickupMultiplier < 1f)
                {
                    rank.GroundPickupMultiplier = 1f;
                }
                if (rank.GroundPickupMultiplier > 10f)
                {
                    rank.GroundPickupMultiplier = 10f;
                }

                if (rank.ContainerLootMultiplier < 1f)
                {
                    rank.ContainerLootMultiplier = 1f;
                }
                if (rank.ContainerLootMultiplier > 10f)
                {
                    rank.ContainerLootMultiplier = 10f;
                }

                if (rank.PocketRecyclerSpeedMultiplier < 1f)
                {
                    rank.PocketRecyclerSpeedMultiplier = 1f;
                }
                if (rank.PocketRecyclerSpeedMultiplier > 20f)
                {
                    rank.PocketRecyclerSpeedMultiplier = 20f;
                }

                // Migration fallback for existing configs that don't have output rate configured yet.
                // Keep recycler output rate aligned with rank stage speed by default.
                if (rank.AllowPocketRecycler &&
                    rank.PocketRecyclerOutputMultiplier <= 1.0001f &&
                    rank.PocketRecyclerSpeedMultiplier > 1.0001f)
                {
                    rank.PocketRecyclerOutputMultiplier = rank.PocketRecyclerSpeedMultiplier;
                }

                if (rank.PocketRecyclerOutputMultiplier < 1f)
                {
                    rank.PocketRecyclerOutputMultiplier = 1f;
                }
                if (rank.PocketRecyclerOutputMultiplier > 20f)
                {
                    rank.PocketRecyclerOutputMultiplier = 20f;
                }

                if (rank.NpcKillScrapReward < 0)
                {
                    rank.NpcKillScrapReward = 0;
                }

                if (rank.RankKitCooldownSeconds < 0)
                {
                    rank.RankKitCooldownSeconds = 0;
                }

                if (rank.RankKitAmountMultiplier < 1f)
                {
                    rank.RankKitAmountMultiplier = 1f;
                }
                if (rank.RankKitAmountMultiplier > 20f)
                {
                    rank.RankKitAmountMultiplier = 20f;
                }

                if (rank.RankKitItems == null)
                {
                    rank.RankKitItems = new List<KitItemEntry>();
                }

                rank.RankKitItems.RemoveAll(item =>
                    item == null ||
                    string.IsNullOrWhiteSpace(item.ShortName) ||
                    item.Amount <= 0);

                normalized[rankKey] = rank;
            }

            config.Ranks = normalized;
        }

        private void LoadData()
        {
            try
            {
                storedData = dataFile.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }

            if (storedData == null)
            {
                storedData = new StoredData();
            }

            if (storedData.Players == null)
            {
                storedData.Players = new Dictionary<string, PrivilegeRecord>();
            }

            if (storedData.RankKitNextClaimUnix == null)
            {
                storedData.RankKitNextClaimUnix = new Dictionary<string, long>();
            }
            if (storedData.DailyNextClaimUnix == null)
            {
                storedData.DailyNextClaimUnix = new Dictionary<string, long>();
            }
            if (storedData.HomeTpNextUseUnix == null)
            {
                storedData.HomeTpNextUseUnix = new Dictionary<string, long>();
            }
            if (storedData.TeamTpNextUseUnix == null)
            {
                storedData.TeamTpNextUseUnix = new Dictionary<string, long>();
            }
            if (storedData.TownTpUsageByUser == null)
            {
                storedData.TownTpUsageByUser = new Dictionary<string, TownTpUsageEntry>();
            }
            if (storedData.PocketRecyclerNextUseUnix == null)
            {
                storedData.PocketRecyclerNextUseUnix = new Dictionary<string, long>();
            }
            if (storedData.HomesByUser == null)
            {
                storedData.HomesByUser = new Dictionary<string, Dictionary<string, HomePointEntry>>();
            }
            else
            {
                var normalizedHomesByUser = new Dictionary<string, Dictionary<string, HomePointEntry>>();
                foreach (var pair in storedData.HomesByUser)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key)) continue;

                    var homes = new Dictionary<string, HomePointEntry>(StringComparer.OrdinalIgnoreCase);
                    if (pair.Value != null)
                    {
                        foreach (var homePair in pair.Value)
                        {
                            var homeName = NormalizeHomeName(homePair.Key);
                            if (string.IsNullOrWhiteSpace(homeName)) continue;
                            if (homePair.Value == null || homePair.Value.Point == null) continue;
                            homes[homeName] = homePair.Value;
                        }
                    }

                    normalizedHomesByUser[pair.Key] = homes;
                }

                storedData.HomesByUser = normalizedHomesByUser;
            }
            if (storedData.AuditLog == null)
            {
                storedData.AuditLog = new List<AuditLogEntry>();
            }
        }

        private void SaveData()
        {
            dataFile.WriteObject(storedData);
        }

        private void RegisterConfigPermissions()
        {
            foreach (var rank in config.Ranks.Values)
            {
                foreach (var permissionName in GetManagedPermissions(rank))
                {
                    if (string.IsNullOrWhiteSpace(permissionName)) continue;
                    if (permission.PermissionExists(permissionName)) continue;
                    if (permissionName.StartsWith("privilegesystem.", StringComparison.OrdinalIgnoreCase))
                    {
                        permission.RegisterPermission(permissionName, this);
                    }
                }
            }
        }

        private void StartExpiryTimer()
        {
            expiryTimer?.Destroy();
            expiryTimer = timer.Every(config.ExpiryCheckIntervalSeconds, () =>
            {
                CleanupExpired(true);
            });
        }

        private void StartWebShopPollTimer()
        {
            webShopPollTimer?.Destroy();
            webShopPollTimer = null;
            webShopPollInProgress = false;

            if (config == null || config.WebShopBridge == null || !config.WebShopBridge.Enabled)
            {
                return;
            }

            webShopPollTimer = timer.Every(config.WebShopBridge.PollIntervalSeconds, PollWebShopOrders);
            timer.Once(2f, PollWebShopOrders);
            Puts($"Web shop bridge enabled: {config.WebShopBridge.ApiBaseUrl}, server={config.WebShopBridge.ServerId}, poll={config.WebShopBridge.PollIntervalSeconds:0.##}s");
        }

        private Dictionary<string, string> BuildWebShopHeaders()
        {
            return new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["X-Server-Key"] = config.WebShopBridge.ServerKey
            };
        }

        private bool TryGetWebShopBridgeConfig(out WebShopBridgeConfig bridge, out string error)
        {
            bridge = null;
            error = string.Empty;

            if (config == null || config.WebShopBridge == null || !config.WebShopBridge.Enabled)
            {
                error = "Мост веб-шопа отключен.";
                return false;
            }

            bridge = config.WebShopBridge;
            if (string.IsNullOrWhiteSpace(bridge.ApiBaseUrl) || string.IsNullOrWhiteSpace(bridge.ServerId) || string.IsNullOrWhiteSpace(bridge.ServerKey))
            {
                error = "Мост веб-шопа не настроен (ApiBaseUrl/ServerId/ServerKey).";
                return false;
            }

            return true;
        }

        private void SendReplyIfOnline(ulong userId, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var target = BasePlayer.FindByID(userId);
            if (target != null && target.IsConnected)
            {
                SendReply(target, message);
            }
        }

        private void ActivateWebShopOrdersForPlayer(BasePlayer player)
        {
            if (player == null) return;

            WebShopBridgeConfig bridge;
            string bridgeError;
            if (!TryGetWebShopBridgeConfig(out bridge, out bridgeError))
            {
                SendReply(player, bridgeError);
                return;
            }

            if (webShopPollInProgress)
            {
                SendReply(player, "Синхронизация веб-шопа уже выполняется. Повторите через несколько секунд.");
                return;
            }

            if (webShopPlayerActivationInProgress.Contains(player.userID))
            {
                SendReply(player, "Активация уже выполняется.");
                return;
            }

            var requesterId = player.userID;
            webShopPollInProgress = true;
            webShopPlayerActivationInProgress.Add(requesterId);

            var url = bridge.ApiBaseUrl.TrimEnd('/') + "/server/orders/claim";
            var payload = JsonConvert.SerializeObject(new WebShopClaimRequest
            {
                ServerId = bridge.ServerId,
                Limit = bridge.BatchSize,
                SteamId = requesterId
            });
            var headers = BuildWebShopHeaders();
            var timeoutMs = bridge.RequestTimeoutSeconds * 1000;

            SendReply(player, "Проверяю ваши оплаченные покупки...");

            webrequest.Enqueue(url, payload, (code, response) =>
            {
                webShopPollInProgress = false;
                webShopPlayerActivationInProgress.Remove(requesterId);

                if (code != 200 || string.IsNullOrWhiteSpace(response))
                {
                    if (code == 204)
                    {
                        SendReplyIfOnline(requesterId, "Ожидающих покупок не найдено.");
                    }
                    else
                    {
                        SendReplyIfOnline(requesterId, $"Запрос активации завершился ошибкой (code={code}).");
                        PrintWarning($"Web shop player activation failed: steam={requesterId}, code={code}, response={response}");
                    }
                    return;
                }

                WebShopClaimResponse claim;
                try
                {
                    claim = JsonConvert.DeserializeObject<WebShopClaimResponse>(response);
                }
                catch (Exception ex)
                {
                    SendReplyIfOnline(requesterId, "Ошибка разбора ответа активации.");
                    PrintWarning("Web shop player activation parse error: " + ex.Message);
                    return;
                }

                if (claim == null || claim.Orders == null || claim.Orders.Count == 0)
                {
                    SendReplyIfOnline(requesterId, "Ожидающих покупок не найдено.");
                    return;
                }

                var ownOrders = new List<WebShopClaimOrder>();
                foreach (var order in claim.Orders)
                {
                    if (order == null) continue;
                    if (order.SteamId != requesterId)
                    {
                        PrintWarning($"Web shop activation steam mismatch: requested={requesterId}, order={order.OrderId}, orderSteam={order.SteamId}");
                        SendWebShopOrderComplete(order.OrderId, false, "steam mismatch for activation");
                        continue;
                    }

                    ownOrders.Add(order);
                }

                if (ownOrders.Count == 0)
                {
                    SendReplyIfOnline(requesterId, "Для вашего SteamID подходящих покупок не найдено.");
                    return;
                }

                ProcessClaimedWebOrders(ownOrders);
                SendReplyIfOnline(requesterId, $"Активация выполнена: обработано заказов {ownOrders.Count}.");
            }, this, RequestMethod.POST, headers, timeoutMs);
        }

        private void PollWebShopOrders()
        {
            if (config == null || config.WebShopBridge == null || !config.WebShopBridge.Enabled)
            {
                return;
            }

            if (webShopPollInProgress)
            {
                return;
            }

            var bridge = config.WebShopBridge;
            if (string.IsNullOrWhiteSpace(bridge.ApiBaseUrl) || string.IsNullOrWhiteSpace(bridge.ServerId) || string.IsNullOrWhiteSpace(bridge.ServerKey))
            {
                PrintWarning("Web shop bridge is enabled but ApiBaseUrl/ServerId/ServerKey is missing.");
                return;
            }

            webShopPollInProgress = true;

            var url = bridge.ApiBaseUrl.TrimEnd('/') + "/server/orders/claim";
            var payload = JsonConvert.SerializeObject(new WebShopClaimRequest
            {
                ServerId = bridge.ServerId,
                Limit = bridge.BatchSize
            });
            var headers = BuildWebShopHeaders();
            var timeoutMs = bridge.RequestTimeoutSeconds * 1000;

            webrequest.Enqueue(url, payload, (code, response) =>
            {
                webShopPollInProgress = false;

                if (code != 200 || string.IsNullOrWhiteSpace(response))
                {
                    if (code != 204)
                    {
                        PrintWarning($"Web shop claim failed: code={code}, response={response}");
                    }
                    return;
                }

                WebShopClaimResponse claim;
                try
                {
                    claim = JsonConvert.DeserializeObject<WebShopClaimResponse>(response);
                }
                catch (Exception ex)
                {
                    PrintWarning("Web shop claim parse error: " + ex.Message);
                    return;
                }

                if (claim == null || claim.Orders == null || claim.Orders.Count == 0)
                {
                    return;
                }

                ProcessClaimedWebOrders(claim.Orders);
            }, this, RequestMethod.POST, headers, timeoutMs);
        }

        private void ProcessClaimedWebOrders(List<WebShopClaimOrder> orders)
        {
            if (orders == null || orders.Count == 0) return;

            foreach (var order in orders)
            {
                if (order == null)
                {
                    continue;
                }

                if (order.SteamId == 0 || string.IsNullOrWhiteSpace(order.Rank))
                {
                    SendWebShopOrderComplete(order.OrderId, false, "invalid order payload");
                    continue;
                }

                string error;
                var targetName = ResolvePlayerName(order.SteamId);
                if (!SetPrivilege(
                        order.SteamId,
                        targetName,
                        order.Rank,
                        Math.Max(0, order.Days),
                        config.WebShopBridge.GrantSourceLabel,
                        0,
                        out error))
                {
                    PrintWarning($"Web shop order apply failed: order={order.OrderId}, steam={order.SteamId}, rank={order.Rank}, err={error}");
                    SendWebShopOrderComplete(order.OrderId, false, error);
                    continue;
                }

                var player = BasePlayer.FindByID(order.SteamId);
                if (player != null && player.IsConnected)
                {
                    SendReply(player, $"Покупка выдана: {BuildPlayerStatus(order.SteamId)}");
                }

                WriteAudit(
                    "webshop.order.applied",
                    0,
                    "WebShop",
                    order.SteamId,
                    targetName,
                    $"order={order.OrderId}; package={order.PackageKey}; rank={NormalizeRankKey(order.Rank)}; days={order.Days}",
                    false);
                SaveData();

                SendWebShopOrderComplete(order.OrderId, true, "applied");
            }
        }

        private void SendWebShopOrderComplete(string orderId, bool success, string message)
        {
            if (config == null || config.WebShopBridge == null || !config.WebShopBridge.Enabled) return;
            if (string.IsNullOrWhiteSpace(orderId)) return;

            var url = config.WebShopBridge.ApiBaseUrl.TrimEnd('/') + "/server/orders/complete";
            var payload = JsonConvert.SerializeObject(new WebShopCompleteRequest
            {
                ServerId = config.WebShopBridge.ServerId,
                OrderId = orderId,
                Success = success,
                Message = string.IsNullOrWhiteSpace(message) ? (success ? "ok" : "failed") : message.Trim()
            });
            var headers = BuildWebShopHeaders();
            var timeoutMs = config.WebShopBridge.RequestTimeoutSeconds * 1000;

            webrequest.Enqueue(url, payload, (code, response) =>
            {
                if (code != 200)
                {
                    PrintWarning($"Web shop complete failed: code={code}, order={orderId}, response={response}");
                }
            }, this, RequestMethod.POST, headers, timeoutMs);
        }

        private static long UtcNowUnix()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static string NormalizeRankKey(string rank)
        {
            return rank.Trim().ToLowerInvariant();
        }

        private static string NormalizeShopCurrency(string currency)
        {
            var normalized = string.IsNullOrWhiteSpace(currency) ? "economics" : currency.Trim().ToLowerInvariant();
            return normalized == "serverrewards" ? "serverrewards" : "economics";
        }

        private static string GetUtcDayKey()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd");
        }

        private static List<string> NormalizePermissionList(List<string> raw)
        {
            var normalized = new List<string>();
            if (raw == null || raw.Count == 0) return normalized;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var permissionName in raw)
            {
                if (string.IsNullOrWhiteSpace(permissionName)) continue;
                var trimmed = permissionName.Trim();
                if (!seen.Add(trimmed)) continue;
                normalized.Add(trimmed);
            }

            return normalized;
        }

        private string BuildHomeLimitPermission(int homePoints)
        {
            if (homePoints <= 0) return string.Empty;
            var template = config?.FeaturePermissions?.HomeLimitPermissionTemplate;
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;

            template = template.Trim();
            try
            {
                return string.Format(template, homePoints);
            }
            catch
            {
                return template.Replace("{0}", homePoints.ToString());
            }
        }

        private List<string> GetManagedPermissions(RankDefinition rank)
        {
            var result = new List<string>();
            if (rank == null) return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (rank.Permissions != null)
            {
                foreach (var permissionName in rank.Permissions)
                {
                    AddPermissionUnique(result, seen, permissionName);
                }
            }

            var featurePermissions = config?.FeaturePermissions;
            if (featurePermissions == null) return result;

            if (rank.AllowTeleport && featurePermissions.TeleportPermissions != null)
            {
                foreach (var permissionName in featurePermissions.TeleportPermissions)
                {
                    AddPermissionUnique(result, seen, permissionName);
                }
            }

            if (rank.HomePoints > 0)
            {
                if (featurePermissions.HomeBasePermissions != null)
                {
                    foreach (var permissionName in featurePermissions.HomeBasePermissions)
                    {
                        AddPermissionUnique(result, seen, permissionName);
                    }
                }

                AddPermissionUnique(result, seen, BuildHomeLimitPermission(rank.HomePoints));
            }

            if (rank.AllowPocketRecycler && featurePermissions.PocketRecyclerPermissions != null)
            {
                foreach (var permissionName in featurePermissions.PocketRecyclerPermissions)
                {
                    AddPermissionUnique(result, seen, permissionName);
                }
            }

            if (rank.AllowRemoveCommand && featurePermissions.RemoveCommandPermissions != null)
            {
                foreach (var permissionName in featurePermissions.RemoveCommandPermissions)
                {
                    AddPermissionUnique(result, seen, permissionName);
                }
            }

            return result;
        }

        private static void AddPermissionUnique(List<string> target, HashSet<string> seen, string permissionName)
        {
            if (target == null || seen == null) return;
            if (string.IsNullOrWhiteSpace(permissionName)) return;

            var trimmed = permissionName.Trim();
            if (!seen.Add(trimmed)) return;
            target.Add(trimmed);
        }

        private bool HasAdminAccess(BasePlayer player)
        {
            if (player == null) return false;
            return player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermissionAdmin);
        }

        private RankDefinition FindRank(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            RankDefinition rank;
            return config.Ranks.TryGetValue(NormalizeRankKey(key), out rank) ? rank : null;
        }

        private bool TryGetActivePrivilege(ulong userId, out PrivilegeRecord record, out RankDefinition rank)
        {
            record = null;
            rank = null;

            var userIdString = userId.ToString();
            if (!storedData.Players.TryGetValue(userIdString, out record) || record == null)
            {
                return false;
            }

            var now = UtcNowUnix();
            if (record.ExpiresAtUnix != 0 && record.ExpiresAtUnix <= now)
            {
                RemoveRecord(userIdString, 0, "System", "expired while checking active rank");
                return false;
            }

            rank = FindRank(record.Rank);
            return rank != null;
        }

        private string BuildPerkSummary(RankDefinition rank)
        {
            if (rank == null) return "без бонусов";
            var chunks = new List<string>();

            if (rank.GatherMultiplier > 1.01f)
            {
                chunks.Add($"добыча с нод x{rank.GatherMultiplier:0.##}");
            }

            if (rank.GroundPickupMultiplier > 1.01f)
            {
                chunks.Add($"подбор с земли x{rank.GroundPickupMultiplier:0.##}");
            }

            if (rank.ContainerLootMultiplier > 1.01f)
            {
                chunks.Add($"лут контейнеров x{rank.ContainerLootMultiplier:0.##}");
            }

            if (rank.AllowTeleport)
            {
                chunks.Add("телепорты");
            }

            if (rank.HomePoints > 0)
            {
                chunks.Add($"домов: {rank.HomePoints}");
            }

            if (rank.AllowPocketRecycler)
            {
                var recyclerSpeed = ClampRecyclerSpeedMultiplier(rank.PocketRecyclerSpeedMultiplier);
                var recyclerRate = ClampRecyclerOutputMultiplier(rank.PocketRecyclerOutputMultiplier);
                chunks.Add($"карманный переработчик скорость x{recyclerSpeed:0.##}, рейт x{recyclerRate:0.##}");
            }

            if (rank.AllowRemoveCommand)
            {
                chunks.Add("/remove");
            }

            if (rank.DailyRewardMultiplier > 1.01f)
            {
                chunks.Add($"/daily x{rank.DailyRewardMultiplier:0.##}");
            }

            if (rank.HomeTeleportCooldownReductionSeconds > 0)
            {
                chunks.Add($"кд home -{rank.HomeTeleportCooldownReductionSeconds}с");
            }

            if (rank.TeamTeleportCooldownReductionSeconds > 0)
            {
                chunks.Add($"кд team -{rank.TeamTeleportCooldownReductionSeconds}с");
            }

            if (rank.TownTeleportDailyLimitBonus > 0)
            {
                chunks.Add($"town +{rank.TownTeleportDailyLimitBonus}/день");
            }

            if (rank.NpcKillScrapReward > 0)
            {
                chunks.Add($"+{rank.NpcKillScrapReward} скрапа за NPC");
            }

            if (rank.RankKitItems != null && rank.RankKitItems.Count > 0)
            {
                chunks.Add($"/rankkit x{rank.RankKitAmountMultiplier:0.##} ({FormatDurationSeconds(rank.RankKitCooldownSeconds)})");
            }

            return chunks.Count == 0 ? "только кастомные бонусы" : string.Join(", ", chunks.ToArray());
        }

        private static string FormatDurationSeconds(int seconds)
        {
            if (seconds <= 0) return "без кд";
            var span = TimeSpan.FromSeconds(seconds);
            if (span.TotalDays >= 1d) return $"{(int)span.TotalDays}д {span.Hours}ч";
            if (span.TotalHours >= 1d) return $"{(int)span.TotalHours}ч {span.Minutes}м";
            return $"{span.Minutes}м {span.Seconds}с";
        }

        private bool TryGiveRankKit(BasePlayer player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Игрок не найден.";
                return false;
            }

            PrivilegeRecord record;
            RankDefinition rank;
            if (!TryGetActivePrivilege(player.userID, out record, out rank))
            {
                message = "Активная привилегия не найдена.";
                return false;
            }

            if (rank.RankKitItems == null || rank.RankKitItems.Count == 0)
            {
                message = "Для вашего ранга rank kit не настроен.";
                return false;
            }

            var userId = player.UserIDString;
            var now = UtcNowUnix();
            long nextClaimUnix;
            if (storedData.RankKitNextClaimUnix.TryGetValue(userId, out nextClaimUnix) && nextClaimUnix > now)
            {
                message = $"Rank kit на перезарядке. Осталось: {FormatRemaining(nextClaimUnix)}";
                return false;
            }

            var granted = 0;
            foreach (var entry in rank.RankKitItems)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ShortName) || entry.Amount <= 0) continue;
                var scaledAmount = Mathf.Max(1, Mathf.CeilToInt(entry.Amount * rank.RankKitAmountMultiplier));
                var item = ItemManager.CreateByName(entry.ShortName, scaledAmount);
                if (item == null)
                {
                    PrintWarning($"Invalid rank kit item shortname: {entry.ShortName}");
                    continue;
                }

                if (!item.MoveToContainer(player.inventory.containerMain) &&
                    !item.MoveToContainer(player.inventory.containerBelt) &&
                    !item.MoveToContainer(player.inventory.containerWear))
                {
                    item.Drop(player.transform.position + Vector3.up, Vector3.zero);
                }

                granted++;
            }

            if (granted == 0)
            {
                message = "Rank kit настроен некорректно.";
                return false;
            }

            if (rank.RankKitCooldownSeconds > 0)
            {
                storedData.RankKitNextClaimUnix[userId] = now + rank.RankKitCooldownSeconds;
            }

            SaveData();
            message = $"Rank kit получен ({granted} стаков).";
            return true;
        }

        private bool TryGiveDailyReward(BasePlayer player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Игрок не найден.";
                return false;
            }

            if (config.DailyRewards == null || !config.DailyRewards.Enabled)
            {
                message = "Ежедневные награды отключены.";
                return false;
            }

            PrivilegeRecord record;
            RankDefinition rank;
            var hasActiveRank = TryGetActivePrivilege(player.userID, out record, out rank);
            if (!hasActiveRank && !config.DailyRewards.AllowWithoutRank)
            {
                message = "Нет активной привилегии для ежедневной награды.";
                return false;
            }

            var userId = player.UserIDString;
            var now = UtcNowUnix();
            long nextClaimUnix;
            if (storedData.DailyNextClaimUnix.TryGetValue(userId, out nextClaimUnix) && nextClaimUnix > now)
            {
                message = $"Ежедневная награда на перезарядке: {FormatRemaining(nextClaimUnix)}";
                return false;
            }

            var baseItems = config.DailyRewards.BaseItems ?? new List<KitItemEntry>();
            var multiplier = rank != null ? Mathf.Clamp(rank.DailyRewardMultiplier, 0.1f, 20f) : 1f;
            var grantedStacks = 0;
            foreach (var entry in baseItems)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ShortName) || entry.Amount <= 0) continue;
                var amount = Mathf.Max(1, Mathf.CeilToInt(entry.Amount * multiplier));
                var item = ItemManager.CreateByName(entry.ShortName, amount);
                if (item == null) continue;

                if (!item.MoveToContainer(player.inventory.containerMain) &&
                    !item.MoveToContainer(player.inventory.containerBelt) &&
                    !item.MoveToContainer(player.inventory.containerWear))
                {
                    item.Drop(player.transform.position + Vector3.up, Vector3.zero);
                }

                grantedStacks++;
            }

            if (grantedStacks <= 0)
            {
                message = "Конфиг ежедневной награды пустой или некорректный.";
                return false;
            }

            storedData.DailyNextClaimUnix[userId] = now + config.DailyRewards.CooldownSeconds;
            WriteAudit(
                "daily.claim",
                player.userID,
                player.displayName,
                player.userID,
                player.displayName,
                $"stacks={grantedStacks}; multiplier={multiplier:0.##}",
                false);
            SaveData();

            message = $"Ежедневная награда получена ({grantedStacks} стаков, x{multiplier:0.##}).";
            return true;
        }

        private bool TryResolveOnlinePlayer(string input, out BasePlayer target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            ulong userId;
            string _;
            if (!TryResolvePlayerIdentity(input, out userId, out _))
            {
                return false;
            }

            target = BasePlayer.FindByID(userId);
            return target != null && target.IsConnected;
        }

        private static bool IsSameTeam(BasePlayer a, BasePlayer b)
        {
            if (a == null || b == null) return false;
            return a.currentTeam != 0 && a.currentTeam == b.currentTeam;
        }

        private static string NormalizeHomeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static bool IsValidHomeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value.Length > 32) return false;

            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') continue;
                return false;
            }

            return true;
        }

        private Dictionary<string, HomePointEntry> GetPlayerHomes(string userId, bool create)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;
            if (storedData.HomesByUser == null)
            {
                storedData.HomesByUser = new Dictionary<string, Dictionary<string, HomePointEntry>>();
            }

            Dictionary<string, HomePointEntry> homes;
            if (!storedData.HomesByUser.TryGetValue(userId, out homes) || homes == null)
            {
                if (!create) return null;
                homes = new Dictionary<string, HomePointEntry>(StringComparer.OrdinalIgnoreCase);
                storedData.HomesByUser[userId] = homes;
            }

            return homes;
        }

        private int GetHomePointsLimit(BasePlayer player, RankDefinition rank)
        {
            if (player == null) return 0;
            if (rank == null)
            {
                TryGetActiveRank(player.userID, out rank);
            }

            var baseWithoutRank = config?.TeleportFeatures?.HomePointsWithoutPrivilege ?? 0;
            var rankPoints = rank?.HomePoints ?? 0;
            return Math.Max(0, Math.Max(baseWithoutRank, rankPoints));
        }

        private static bool IsHomeAnchorPrefab(BuildingBlock block)
        {
            if (block == null) return false;
            var prefab = (block.ShortPrefabName ?? string.Empty).ToLowerInvariant();
            return prefab == "foundation" ||
                   prefab == "foundation.triangle" ||
                   prefab == "floor" ||
                   prefab == "floor.triangle";
        }

        private static BuildingBlock FindBuildingBlockBelow(Vector3 origin, float maxDistance)
        {
            var hits = Physics.RaycastAll(origin, Vector3.down, maxDistance);
            if (hits == null || hits.Length == 0) return null;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                var block = hit.collider.GetComponentInParent<BuildingBlock>();
                if (block == null || block.IsDestroyed) continue;
                return block;
            }

            return null;
        }

        private bool HasPlayerRespawnPointOnBlock(ulong playerId, BuildingBlock anchorBlock)
        {
            if (playerId == 0 || anchorBlock == null || anchorBlock.net == null) return false;

            var anchorPos = anchorBlock.transform.position;
            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var bag = networkable as SleepingBag;
                if (bag == null || bag.IsDestroyed) continue;
                if (bag.OwnerID != playerId) continue;

                var bagPos = bag.transform.position;
                if (Mathf.Abs(bagPos.y - anchorPos.y) > 3f) continue;
                if ((new Vector2(bagPos.x - anchorPos.x, bagPos.z - anchorPos.z)).sqrMagnitude > 16f) continue;

                var bagAnchor = FindBuildingBlockBelow(bagPos + new Vector3(0f, 0.4f, 0f), 4f);
                if (bagAnchor == null || bagAnchor.net == null) continue;
                if (bagAnchor.net.ID == anchorBlock.net.ID)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetPlayerHomeAnchorBlock(BasePlayer player, out BuildingBlock block, out string error)
        {
            block = null;
            error = string.Empty;

            if (player == null)
            {
                error = "Игрок не найден.";
                return false;
            }

            block = FindBuildingBlockBelow(player.transform.position + new Vector3(0f, 1.2f, 0f), 6f);
            if (block == null)
            {
                error = "Ставить home можно только на своем фундаменте/полу, где есть ваш спальник или кровать.";
                return false;
            }

            if (!IsHomeAnchorPrefab(block))
            {
                error = "Ставить home можно только на фундаменте или полу.";
                return false;
            }

            if (block.OwnerID != player.userID)
            {
                error = "Ставить home можно только на своем фундаменте/полу.";
                return false;
            }

            if (!HasPlayerRespawnPointOnBlock(player.userID, block))
            {
                error = "На этом блоке нет вашего спальника/кровати. Сначала поставьте его.";
                return false;
            }

            return true;
        }

        private int GetHomeTeleportActivationDelaySeconds(RankDefinition rank)
        {
            var baseDelay = config?.TeleportFeatures?.HomeActivationBaseDelaySeconds ?? 15;
            var reduction = rank?.HomeTeleportCooldownReductionSeconds ?? 0;
            return Math.Max(0, baseDelay - reduction);
        }

        private void PlayTeleportEffects(Vector3 position)
        {
            try
            {
                Effect.server.Run("assets/bundled/prefabs/fx/notice/item.select.fx.prefab", position);
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.updated.prefab", position);
            }
            catch
            {
                // Do not block teleport flow if effect prefab is unavailable after a game update.
            }
        }

        private void CancelPendingHomeTeleport(ulong userId, string reason = null)
        {
            PendingHomeTeleport pending;
            if (!pendingHomeTeleports.TryGetValue(userId, out pending) || pending == null) return;

            pending.Timer?.Destroy();
            pendingHomeTeleports.Remove(userId);

            if (string.IsNullOrWhiteSpace(reason)) return;
            var player = BasePlayer.FindByID(userId);
            if (player != null && player.IsConnected)
            {
                SendReply(player, reason);
            }
        }

        private bool FinishHomeTeleport(BasePlayer player, string homeName, TeleportPoint destination, int cooldown, out string message)
        {
            message = string.Empty;
            string teleportError;
            if (!TryTeleportToPoint(player, destination, out teleportError))
            {
                message = teleportError;
                return false;
            }

            var userId = player.UserIDString;
            var now = UtcNowUnix();
            if (cooldown > 0)
            {
                storedData.HomeTpNextUseUnix[userId] = now + cooldown;
            }
            else
            {
                storedData.HomeTpNextUseUnix.Remove(userId);
            }
            SaveData();

            message = cooldown > 0 ? $"Телепортация в home '{homeName}' выполнена. КД: {cooldown}с." : $"Телепортация в home '{homeName}' выполнена.";
            return true;
        }

        private bool CanUseRemoveFeature(BasePlayer player, out string error)
        {
            error = string.Empty;
            if (player == null)
            {
                error = "Игрок не найден.";
                return false;
            }

            if (HasAdminAccess(player)) return true;

            RankDefinition rank;
            if (TryGetActiveRank(player.userID, out rank) && rank != null && rank.AllowRemoveCommand)
            {
                return true;
            }

            error = "Команда /remove недоступна для вашей активной привилегии.";
            return false;
        }

        private bool HasHammerInHands(BasePlayer player)
        {
            if (player == null) return false;
            var active = player.GetActiveItem();
            return active != null &&
                   active.info != null &&
                   string.Equals(active.info.shortname, "hammer", StringComparison.OrdinalIgnoreCase);
        }

        private int GetRemoveModeDurationSeconds()
        {
            var value = config?.TeleportFeatures?.RemoveModeDurationSeconds ?? 30;
            return Math.Max(5, Math.Min(600, value));
        }

        private static float ClampRecyclerSpeedMultiplier(float value)
        {
            if (value < 1f) return 1f;
            if (value > 20f) return 20f;
            return value;
        }

        private static float ClampRecyclerOutputMultiplier(float value)
        {
            if (value < 1f) return 1f;
            if (value > 20f) return 20f;
            return value;
        }

        private float GetPocketRecyclerEffectiveSpeedMultiplier(ulong userId)
        {
            var baseSpeed = ClampRecyclerSpeedMultiplier(config?.TeleportFeatures?.PocketRecyclerSpeedMultiplier ?? 1f);
            RankDefinition activeRank;
            if (!TryGetActiveRank(userId, out activeRank) || activeRank == null)
            {
                return baseSpeed;
            }

            var rankStageSpeed = ClampRecyclerSpeedMultiplier(activeRank.PocketRecyclerSpeedMultiplier);
            return Mathf.Clamp(baseSpeed * rankStageSpeed, 1f, 100f);
        }

        private float GetPocketRecyclerTickIntervalSeconds(ulong userId)
        {
            var speed = GetPocketRecyclerEffectiveSpeedMultiplier(userId);
            return Mathf.Clamp(PersonalRecyclerBaseTickSeconds / speed, 0.05f, PersonalRecyclerBaseTickSeconds);
        }

        private float GetPocketRecyclerEffectiveOutputMultiplier(ulong userId)
        {
            RankDefinition activeRank;
            if (!TryGetActiveRank(userId, out activeRank) || activeRank == null)
            {
                return 1f;
            }

            return ClampRecyclerOutputMultiplier(activeRank.PocketRecyclerOutputMultiplier);
        }

        private int GetPocketRecyclerCommandCooldownSeconds()
        {
            var value = config?.TeleportFeatures?.PocketRecyclerCommandCooldownSeconds ?? 10;
            return Math.Max(0, Math.Min(600, value));
        }

        private float GetPocketRecyclerWorkingSoundIntervalSeconds()
        {
            var value = config?.TeleportFeatures?.PocketRecyclerWorkingSoundIntervalSeconds ?? 1.2f;
            return Mathf.Clamp(value, 0.1f, 10f);
        }

        private bool CanUsePocketRecyclerFeature(BasePlayer player, out string error)
        {
            error = string.Empty;
            if (player == null)
            {
                error = "Игрок не найден.";
                return false;
            }

            if (HasAdminAccess(player)) return true;

            RankDefinition rank;
            if (TryGetActiveRank(player.userID, out rank) && rank != null && rank.AllowPocketRecycler)
            {
                return true;
            }

            error = "Команда /recycler недоступна для вашей активной привилегии.";
            return false;
        }

        private static string FormatCountdownClock(int seconds)
        {
            if (seconds <= 0) return "00:00";
            var span = TimeSpan.FromSeconds(seconds);
            if (span.TotalHours >= 1d)
            {
                return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
            }
            return $"{span.Minutes:00}:{span.Seconds:00}";
        }

        private void StopRemoveModeUiTimer(ulong userId)
        {
            Timer existingUiTimer;
            if (removeModeUiTimers.TryGetValue(userId, out existingUiTimer))
            {
                existingUiTimer?.Destroy();
                removeModeUiTimers.Remove(userId);
            }
            removeModeUiEndsUnix.Remove(userId);
        }

        private void DestroyRemoveModeUi(ulong userId)
        {
            var player = BasePlayer.FindByID(userId);
            if (player != null && player.IsConnected)
            {
                CuiHelper.DestroyUi(player, RemoveModeUiRoot);
            }
        }

        private void RefreshRemoveModeUi(ulong userId)
        {
            if (!removeModePlayers.Contains(userId))
            {
                StopRemoveModeUiTimer(userId);
                DestroyRemoveModeUi(userId);
                return;
            }

            var player = BasePlayer.FindByID(userId);
            if (player == null || !player.IsConnected) return;

            long endsAtUnix;
            if (!removeModeUiEndsUnix.TryGetValue(userId, out endsAtUnix))
            {
                CuiHelper.DestroyUi(player, RemoveModeUiRoot);
                return;
            }

            var remainingSeconds = (int)Math.Max(0, endsAtUnix - UtcNowUnix());
            if (remainingSeconds <= 0)
            {
                CuiHelper.DestroyUi(player, RemoveModeUiRoot);
                return;
            }

            var container = new CuiElementContainer();
            var panel = container.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.08 0.08 0.78" },
                RectTransform = { AnchorMin = "0.015 0.92", AnchorMax = "0.18 0.975" },
                CursorEnabled = false
            }, "Hud", RemoveModeUiRoot);

            var iconPanel = container.Add(new CuiPanel
            {
                Image = { Color = "0.82 0.21 0.17 0.95" },
                RectTransform = { AnchorMin = "0.02 0.15", AnchorMax = "0.12 0.85" },
                CursorEnabled = false
            }, panel, RemoveModeUiRoot + ".Icon");

            container.Add(new CuiLabel
            {
                Text = { Text = "R", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, iconPanel);

            container.Add(new CuiLabel
            {
                Text = { Text = "REMOVE", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 0.9 0.85 1" },
                RectTransform = { AnchorMin = "0.16 0.52", AnchorMax = "0.98 0.95" }
            }, panel);

            container.Add(new CuiLabel
            {
                Text = { Text = FormatCountdownClock(remainingSeconds), FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.16 0.04", AnchorMax = "0.98 0.58" }
            }, panel);

            CuiHelper.DestroyUi(player, RemoveModeUiRoot);
            CuiHelper.AddUi(player, container);
        }

        private void StartRemoveModeUi(ulong userId, int durationSeconds)
        {
            StopRemoveModeUiTimer(userId);
            removeModeUiEndsUnix[userId] = UtcNowUnix() + Math.Max(1, durationSeconds);
            RefreshRemoveModeUi(userId);
            removeModeUiTimers[userId] = timer.Every(1f, () =>
            {
                RefreshRemoveModeUi(userId);
            });
        }

        private void DisableRemoveMode(ulong userId, string reason = null)
        {
            Timer existingTimer;
            if (removeModeTimers.TryGetValue(userId, out existingTimer))
            {
                existingTimer?.Destroy();
                removeModeTimers.Remove(userId);
            }

            StopRemoveModeUiTimer(userId);
            DestroyRemoveModeUi(userId);

            var wasActive = removeModePlayers.Remove(userId);
            if (!wasActive)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(reason)) return;
            var player = BasePlayer.FindByID(userId);
            if (player != null && player.IsConnected)
            {
                SendReply(player, reason);
            }
        }

        private void EnableRemoveMode(BasePlayer player)
        {
            if (player == null) return;

            DisableRemoveMode(player.userID);
            removeModePlayers.Add(player.userID);

            var modeSeconds = GetRemoveModeDurationSeconds();
            StartRemoveModeUi(player.userID, modeSeconds);
            removeModeTimers[player.userID] = timer.Once(modeSeconds, () =>
            {
                DisableRemoveMode(player.userID, "Время режима ремува истекло.");
            });
        }

        private bool CanRemoveTargetEntity(BasePlayer player, BaseEntity entity, out string error)
        {
            error = string.Empty;
            if (player == null || entity == null)
            {
                error = "Некорректная цель.";
                return false;
            }

            if (entity.IsDestroyed || entity.net == null)
            {
                error = "Эту цель нельзя удалить.";
                return false;
            }

            if (entity is BasePlayer)
            {
                error = "Игроков удалять нельзя.";
                return false;
            }

            var distance = Vector3.Distance(player.transform.position, entity.transform.position);
            if (distance > RemoveMaxDistanceMeters + 0.1f)
            {
                error = $"Слишком далеко. Макс. дистанция: {RemoveMaxDistanceMeters:0.#}м.";
                return false;
            }

            if (!HasAdminAccess(player) && entity.OwnerID != player.userID)
            {
                error = "Можно удалять только свои сущности.";
                return false;
            }

            if (!HasAdminAccess(player) && entity.OwnerID == 0)
            {
                error = "У этой сущности нет владельца, удалить нельзя.";
                return false;
            }

            return true;
        }

        private bool TrySpawnPersonalRecycler(BasePlayer player, out Recycler recycler, out string error)
        {
            recycler = null;
            error = string.Empty;
            if (player == null)
            {
                error = "Игрок не найден.";
                return false;
            }

            // Spawn below player so model is hidden, but still near enough for stable UI opening.
            var spawnPos = player.transform.position + new Vector3(0f, PersonalRecyclerHiddenOffsetY, 0f);
            var spawnRot = Quaternion.identity;

            var entity = GameManager.server.CreateEntity("assets/bundled/prefabs/static/recycler_static.prefab", spawnPos, spawnRot, true);
            recycler = entity as Recycler;
            if (recycler == null)
            {
                error = "Не удалось создать сущность переработчика.";
                entity?.Kill();
                return false;
            }

            recycler.OwnerID = player.userID;
            recycler.enableSaving = false;
            recycler.Spawn();
            MoveRecyclerToHiddenPosition(player, recycler);
            recycler.SendNetworkUpdateImmediate();

            if (recycler.net != null)
            {
                personalRecyclerOwnerByEntityId[recycler.net.ID.Value] = player.userID;
            }

            personalRecyclerByPlayer[player.userID] = recycler;
            StartPersonalRecyclerLifetime(recycler);
            StartPersonalRecyclerSpeedTimer(recycler);
            return true;
        }

        private void StartPersonalRecyclerLifetime(Recycler recycler)
        {
            if (recycler == null || recycler.net == null) return;
            var entityId = recycler.net.ID.Value;
            Timer previous;
            if (personalRecyclerLifetimeTimers.TryGetValue(entityId, out previous))
            {
                previous?.Destroy();
            }

            personalRecyclerLifetimeTimers[entityId] = timer.Once(PersonalRecyclerLifetimeSeconds, () =>
            {
                ulong ownerId;
                if (!personalRecyclerOwnerByEntityId.TryGetValue(entityId, out ownerId)) return;
                DestroyPersonalRecycler(ownerId, "Карманный переработчик закрыт (таймаут).");
            });
        }

        private void StopPersonalRecyclerSpeedTimer(ulong entityId)
        {
            Timer existing;
            if (!personalRecyclerSpeedTimers.TryGetValue(entityId, out existing)) return;
            existing?.Destroy();
            personalRecyclerSpeedTimers.Remove(entityId);
        }

        private void StopPersonalRecyclerCloseWatchTimer(ulong ownerId)
        {
            Timer existing;
            if (!personalRecyclerCloseWatchTimers.TryGetValue(ownerId, out existing)) return;
            existing?.Destroy();
            personalRecyclerCloseWatchTimers.Remove(ownerId);
        }

        private void StartPersonalRecyclerCloseWatchTimer(ulong ownerId)
        {
            if (ownerId == 0) return;
            StopPersonalRecyclerCloseWatchTimer(ownerId);

            personalRecyclerCloseWatchTimers[ownerId] = timer.Every(0.25f, () =>
            {
                if (!IsPocketRecyclerAutoCloseOnMenuCloseEnabled()) return;

                Recycler recycler;
                if (!personalRecyclerByPlayer.TryGetValue(ownerId, out recycler) || recycler == null || recycler.IsDestroyed || recycler.net == null)
                {
                    StopPersonalRecyclerCloseWatchTimer(ownerId);
                    return;
                }

                var player = BasePlayer.FindByID(ownerId);
                if (player == null || !player.IsConnected || player.inventory == null || player.inventory.loot == null)
                {
                    return;
                }

                long guardUntil;
                var now = UtcNowUnix();
                if (personalRecyclerCloseGuardUntilUnix.TryGetValue(ownerId, out guardUntil) && guardUntil > now)
                {
                    return;
                }

                if (player.inventory.loot.entitySource == recycler)
                {
                    return;
                }

                DestroyPersonalRecycler(ownerId, "Карманный переработчик закрыт.");
            });
        }

        private void StartPersonalRecyclerSpeedTimer(Recycler recycler)
        {
            if (recycler == null || recycler.net == null || recyclerThinkMethod == null) return;

            var entityId = recycler.net.ID.Value;
            var ownerId = recycler.OwnerID;
            if (ownerId == 0)
            {
                personalRecyclerOwnerByEntityId.TryGetValue(entityId, out ownerId);
            }
            StopPersonalRecyclerSpeedTimer(entityId);
            var interval = GetPocketRecyclerTickIntervalSeconds(ownerId);
            if (interval <= 0f) return;

            personalRecyclerSpeedTimers[entityId] = timer.Every(interval, () =>
            {
                if (recycler == null || recycler.IsDestroyed || recycler.net == null)
                {
                    StopPersonalRecyclerSpeedTimer(entityId);
                    return;
                }

                try
                {
                    var outputBefore = SnapshotRecyclerOutputAmounts(recycler);
                    recyclerThinkMethod.Invoke(recycler, null);
                    var producedTotal = ApplyPocketRecyclerOutputRate(recycler, ownerId, outputBefore);
                    if (producedTotal > 0)
                    {
                        TryPlayPocketRecyclerWorkSound(recycler, ownerId);
                    }
                }
                catch
                {
                    StopPersonalRecyclerSpeedTimer(entityId);
                }
            });
        }

        private void DestroyPersonalRecycler(ulong ownerId, string reason = null)
        {
            personalRecyclerCloseGuardUntilUnix.Remove(ownerId);
            personalRecyclerWorkSoundNextTime.Remove(ownerId);
            StopPersonalRecyclerCloseWatchTimer(ownerId);
            Recycler recycler;
            if (!personalRecyclerByPlayer.TryGetValue(ownerId, out recycler))
            {
                return;
            }
            personalRecyclerByPlayer.Remove(ownerId);

            ulong entityId = 0;
            if (recycler != null && recycler.net != null)
            {
                entityId = recycler.net.ID.Value;
                personalRecyclerOwnerByEntityId.Remove(entityId);
                Timer lifetimeTimer;
                if (personalRecyclerLifetimeTimers.TryGetValue(entityId, out lifetimeTimer))
                {
                    lifetimeTimer?.Destroy();
                    personalRecyclerLifetimeTimers.Remove(entityId);
                }
                StopPersonalRecyclerSpeedTimer(entityId);
            }

            var owner = BasePlayer.FindByID(ownerId);
            if (owner != null && owner.IsConnected && owner.inventory != null && owner.inventory.loot != null)
            {
                if (owner.inventory.loot.entitySource == recycler)
                {
                    owner.inventory.loot.Clear();
                    owner.ClientRPCPlayer(null, owner, "RPC_CloseLootPanel");
                }
                PlayPocketRecyclerLocalSound(owner, false);
            }

            if (recycler != null && !recycler.IsDestroyed)
            {
                recycler.Kill();
            }

            if (!string.IsNullOrWhiteSpace(reason) && owner != null && owner.IsConnected)
            {
                SendReply(owner, reason);
            }
        }

        private static void MoveRecyclerToHiddenPosition(BasePlayer player, Recycler recycler)
        {
            if (player == null || recycler == null || recycler.IsDestroyed) return;

            recycler.transform.position = player.transform.position + new Vector3(0f, PersonalRecyclerHiddenOffsetY, 0f);
            recycler.transform.rotation = Quaternion.identity;
        }

        private bool IsPocketRecyclerAutoCloseOnMenuCloseEnabled()
        {
            return config?.TeleportFeatures?.PocketRecyclerAutoCloseOnMenuClose ?? true;
        }

        private void PlayEffectToSinglePlayer(BasePlayer player, string prefab)
        {
            if (player == null || !player.IsConnected || player.net == null || player.net.connection == null) return;
            if (string.IsNullOrWhiteSpace(prefab)) return;

            var fx = new Effect(prefab, player, 0u, Vector3.zero, Vector3.forward);
            EffectNetwork.Send(fx, player.net.connection);
        }

        private void PlayPocketRecyclerLocalSound(BasePlayer player, bool open)
        {
            if (player == null || !player.IsConnected || player.net == null || player.net.connection == null) return;
            if (config?.TeleportFeatures == null || !config.TeleportFeatures.PocketRecyclerLocalSoundsEnabled) return;

            var prefab = open
                ? config.TeleportFeatures.PocketRecyclerOpenSoundEffectPrefab
                : config.TeleportFeatures.PocketRecyclerCloseSoundEffectPrefab;
            if (string.IsNullOrWhiteSpace(prefab)) return;

            try
            {
                PlayEffectToSinglePlayer(player, prefab);
            }
            catch
            {
                if (config.TeleportFeatures.ShowDebugReplies)
                {
                    SendReply(player, "Не удалось воспроизвести локальный звук переработчика.");
                }
            }
        }

        private void TryPlayPocketRecyclerWorkSound(Recycler recycler, ulong ownerId)
        {
            if (recycler == null || recycler.IsDestroyed) return;
            if (config?.TeleportFeatures == null || !config.TeleportFeatures.PocketRecyclerLocalSoundsEnabled) return;

            var prefab = config.TeleportFeatures.PocketRecyclerWorkingSoundEffectPrefab;
            if (string.IsNullOrWhiteSpace(prefab)) return;

            var now = UnityEngine.Time.realtimeSinceStartup;
            float nextTime;
            if (personalRecyclerWorkSoundNextTime.TryGetValue(ownerId, out nextTime) && now < nextTime)
            {
                return;
            }

            var owner = BasePlayer.FindByID(ownerId);
            if (owner == null || !owner.IsConnected) return;

            try
            {
                PlayEffectToSinglePlayer(owner, prefab);
                personalRecyclerWorkSoundNextTime[ownerId] = now + GetPocketRecyclerWorkingSoundIntervalSeconds();
            }
            catch
            {
                if (config.TeleportFeatures.ShowDebugReplies)
                {
                    SendReply(owner, "Не удалось воспроизвести звук работы переработчика.");
                }
            }
        }

        private static string BuildRecyclerOutputKey(int itemId, ulong skin)
        {
            return itemId.ToString() + "|" + skin.ToString();
        }

        private static string BuildRecyclerOutputKey(Item item)
        {
            if (item == null || item.info == null) return string.Empty;
            return BuildRecyclerOutputKey(item.info.itemid, item.skin);
        }

        private static bool TryParseRecyclerOutputKey(string key, out int itemId, out ulong skin)
        {
            itemId = 0;
            skin = 0;
            if (string.IsNullOrWhiteSpace(key)) return false;

            var parts = key.Split('|');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out itemId)) return false;
            if (!ulong.TryParse(parts[1], out skin)) return false;
            return true;
        }

        private static Dictionary<string, int> SnapshotRecyclerOutputAmounts(Recycler recycler)
        {
            var snapshot = new Dictionary<string, int>(StringComparer.Ordinal);
            if (recycler == null || recycler.IsDestroyed || recycler.inventory == null || recycler.inventory.itemList == null)
            {
                return snapshot;
            }

            foreach (var item in recycler.inventory.itemList)
            {
                if (item == null || item.info == null || item.amount <= 0) continue;
                if (item.position < RecyclerOutputStartSlot) continue;

                var key = BuildRecyclerOutputKey(item);
                if (string.IsNullOrEmpty(key)) continue;

                int existing;
                if (snapshot.TryGetValue(key, out existing))
                {
                    snapshot[key] = existing + item.amount;
                }
                else
                {
                    snapshot[key] = item.amount;
                }
            }

            return snapshot;
        }

        private bool TryMoveItemToRecyclerOutput(Recycler recycler, Item item)
        {
            if (recycler == null || recycler.inventory == null || item == null) return false;

            var container = recycler.inventory;
            var capacity = Math.Max(container.capacity, RecyclerOutputStartSlot + 1);
            for (var slot = RecyclerOutputStartSlot; slot < capacity; slot++)
            {
                if (item.MoveToContainer(container, slot, true))
                {
                    return true;
                }
            }

            return item.MoveToContainer(container, -1, true);
        }

        private void GrantPocketRecyclerExtraOutput(Recycler recycler, ulong ownerId, int itemId, ulong skin, int amount)
        {
            if (recycler == null || recycler.inventory == null || amount <= 0) return;

            var itemDef = ItemManager.FindItemDefinition(itemId);
            if (itemDef == null) return;

            var owner = BasePlayer.FindByID(ownerId);
            var stackSize = Math.Max(1, itemDef.stackable);
            while (amount > 0)
            {
                var chunk = Math.Min(stackSize, amount);
                var created = ItemManager.CreateByItemID(itemId, chunk, skin);
                if (created == null) break;

                var moved = TryMoveItemToRecyclerOutput(recycler, created);
                if (!moved && owner != null && owner.IsConnected && owner.inventory != null)
                {
                    moved = created.MoveToContainer(owner.inventory.containerMain) ||
                            created.MoveToContainer(owner.inventory.containerBelt) ||
                            created.MoveToContainer(owner.inventory.containerWear);
                }

                if (!moved)
                {
                    var dropPos = owner != null && owner.IsConnected
                        ? owner.transform.position + Vector3.up
                        : recycler.transform.position + Vector3.up;
                    created.Drop(dropPos, Vector3.zero);
                }

                amount -= chunk;
            }
        }

        private int ApplyPocketRecyclerOutputRate(Recycler recycler, ulong ownerId, Dictionary<string, int> beforeOutput)
        {
            if (recycler == null || recycler.IsDestroyed || beforeOutput == null) return 0;

            var afterOutput = SnapshotRecyclerOutputAmounts(recycler);
            var outputMultiplier = GetPocketRecyclerEffectiveOutputMultiplier(ownerId);
            var applyRate = outputMultiplier > 1.01f;
            var producedTotal = 0;

            foreach (var pair in afterOutput)
            {
                int beforeAmount;
                if (!beforeOutput.TryGetValue(pair.Key, out beforeAmount))
                {
                    beforeAmount = 0;
                }

                var producedAmount = pair.Value - beforeAmount;
                if (producedAmount <= 0) continue;
                producedTotal += producedAmount;

                if (!applyRate) continue;
                var extraAmount = Mathf.CeilToInt(producedAmount * (outputMultiplier - 1f));
                if (extraAmount <= 0) continue;

                int itemId;
                ulong skin;
                if (!TryParseRecyclerOutputKey(pair.Key, out itemId, out skin)) continue;
                GrantPocketRecyclerExtraOutput(recycler, ownerId, itemId, skin, extraAmount);
            }

            return producedTotal;
        }

        private bool TryOpenRecyclerLootUi(BasePlayer player, Recycler recycler, out string error)
        {
            error = string.Empty;
            if (player == null || recycler == null || recycler.IsDestroyed || recycler.inventory == null ||
                player.inventory == null || player.inventory.loot == null)
            {
                error = "Переработчик недоступен.";
                return false;
            }

            try
            {
                player.inventory.loot.Clear();
                player.inventory.loot.PositionChecks = false;
                player.inventory.loot.StartLootingEntity(recycler, false);
                player.inventory.loot.AddContainer(recycler.inventory);
                recycler.SetFlag(BaseEntity.Flags.Open, true);
                recycler.SendNetworkUpdateImmediate();
                player.inventory.loot.SendImmediate();
                var panelName = string.IsNullOrWhiteSpace(recycler.panelName) ? "recycler" : recycler.panelName;
                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", panelName);
                if (!string.Equals(panelName, "recycler", StringComparison.OrdinalIgnoreCase))
                {
                    player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", "recycler");
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Не удалось открыть меню переработчика: " + ex.Message;
                return false;
            }
        }

        private void QueueRecyclerUiOpenRetry(ulong userId, float delaySeconds = 0.12f)
        {
            timer.Once(delaySeconds, () =>
            {
                var online = BasePlayer.FindByID(userId);
                if (online == null || !online.IsConnected || online.inventory == null || online.inventory.loot == null) return;

                Recycler recycler;
                if (!personalRecyclerByPlayer.TryGetValue(userId, out recycler) || recycler == null || recycler.IsDestroyed || recycler.net == null) return;

                if (online.inventory.loot.entitySource == recycler)
                {
                    return;
                }

                MoveRecyclerToHiddenPosition(online, recycler);
                recycler.SendNetworkUpdateImmediate();
                personalRecyclerCloseGuardUntilUnix[userId] = UtcNowUnix() + 1L;

                string retryError;
                if (TryOpenRecyclerLootUi(online, recycler, out retryError))
                {
                    PlayPocketRecyclerLocalSound(online, true);
                }
            });
        }

        private bool TryOpenPersonalRecycler(BasePlayer player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Игрок не найден.";
                return false;
            }

            string accessError;
            if (!CanUsePocketRecyclerFeature(player, out accessError))
            {
                message = accessError;
                return false;
            }

            var commandCooldownSeconds = GetPocketRecyclerCommandCooldownSeconds();
            var userId = player.UserIDString;
            var nowUnix = UtcNowUnix();
            if (commandCooldownSeconds > 0)
            {
                long nextUseUnix;
                if (storedData.PocketRecyclerNextUseUnix.TryGetValue(userId, out nextUseUnix) && nextUseUnix > nowUnix)
                {
                    message = $"Команда переработчика на перезарядке: {nextUseUnix - nowUnix}с.";
                    return false;
                }
            }

            Recycler recycler;
            if (!personalRecyclerByPlayer.TryGetValue(player.userID, out recycler) || recycler == null || recycler.IsDestroyed || recycler.net == null)
            {
                if (!TrySpawnPersonalRecycler(player, out recycler, out message))
                {
                    return false;
                }
            }
            else
            {
                StartPersonalRecyclerLifetime(recycler);
                StartPersonalRecyclerSpeedTimer(recycler);
            }

            MoveRecyclerToHiddenPosition(player, recycler);
            recycler.SendNetworkUpdateImmediate();
            personalRecyclerCloseGuardUntilUnix[player.userID] = UtcNowUnix() + 1L;
            StartPersonalRecyclerCloseWatchTimer(player.userID);

            string openError;
            var openedNow = TryOpenRecyclerLootUi(player, recycler, out openError);
            QueueRecyclerUiOpenRetry(player.userID);
            if (openedNow)
            {
                PlayPocketRecyclerLocalSound(player, true);
            }
            if (!openedNow && config.TeleportFeatures != null && config.TeleportFeatures.ShowDebugReplies)
            {
                SendReply(player, "Первое открытие меню не удалось, выполняю повторную попытку.");
            }

            if (commandCooldownSeconds > 0)
            {
                storedData.PocketRecyclerNextUseUnix[userId] = nowUnix + commandCooldownSeconds;
            }
            else
            {
                storedData.PocketRecyclerNextUseUnix.Remove(userId);
            }
            SaveData();

            var effectiveSpeed = GetPocketRecyclerEffectiveSpeedMultiplier(player.userID);
            var effectiveRate = GetPocketRecyclerEffectiveOutputMultiplier(player.userID);
            message = openedNow
                ? $"Карманный переработчик открыт (скорость x{effectiveSpeed:0.##}, рейт x{effectiveRate:0.##}). Используй меню переработчика. Закрыть: /recycler off."
                : $"Открываю карманный переработчик (скорость x{effectiveSpeed:0.##}, рейт x{effectiveRate:0.##})... Меню будет открыто автоматически.";
            return true;
        }

        private bool TrySetHomePoint(BasePlayer player, string homeName, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Игрок не найден.";
                return false;
            }

            homeName = NormalizeHomeName(homeName);
            if (!IsValidHomeName(homeName))
            {
                message = "Имя home: 1-32 символа, только буквы/цифры/_/-.";
                return false;
            }

            RankDefinition rank;
            if (!TryGetActiveRank(player.userID, out rank))
            {
                rank = null;
            }

            var homeLimit = GetHomePointsLimit(player, rank);
            if (homeLimit <= 0)
            {
                message = "В вашей активной привилегии нет доступных home-точек.";
                return false;
            }

            BuildingBlock anchorBlock;
            string anchorError;
            if (!TryGetPlayerHomeAnchorBlock(player, out anchorBlock, out anchorError))
            {
                message = anchorError;
                return false;
            }

            var homes = GetPlayerHomes(player.UserIDString, true);
            var isUpdate = homes.ContainsKey(homeName);
            if (!isUpdate && homes.Count >= homeLimit)
            {
                message = $"Лимит домов достигнут: {homes.Count}/{homeLimit}. Удалите один: /removehome <name>.";
                return false;
            }

            homes[homeName] = new HomePointEntry
            {
                Point = new TeleportPoint(player.transform.position),
                UpdatedAtUnix = UtcNowUnix()
            };

            SaveData();
            var anchorType = string.IsNullOrWhiteSpace(anchorBlock.ShortPrefabName) ? "блок" : anchorBlock.ShortPrefabName;
            message = isUpdate
                ? $"Home '{homeName}' обновлен на {anchorType} ({homes.Count}/{homeLimit})."
                : $"Home '{homeName}' сохранен на {anchorType} ({homes.Count}/{homeLimit}).";
            return true;
        }

        private bool TryRemoveHomePoint(BasePlayer player, string homeName, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Игрок не найден.";
                return false;
            }

            homeName = NormalizeHomeName(homeName);
            if (!IsValidHomeName(homeName))
            {
                message = "Использование: /removehome <home_name>";
                return false;
            }

            var homes = GetPlayerHomes(player.UserIDString, false);
            if (homes == null || !homes.Remove(homeName))
            {
                message = $"Home '{homeName}' не найден.";
                return false;
            }

            if (homes.Count == 0)
            {
                storedData.HomesByUser.Remove(player.UserIDString);
            }

            SaveData();
            message = $"Home '{homeName}' удален.";
            return true;
        }

        private bool TryGetHomePoint(BasePlayer player, string homeName, out HomePointEntry homeEntry, out string error)
        {
            homeEntry = null;
            error = string.Empty;
            if (player == null)
            {
                error = "Игрок не найден.";
                return false;
            }

            homeName = NormalizeHomeName(homeName);
            if (!IsValidHomeName(homeName))
            {
                error = "Использование: /hometp <home_name>";
                return false;
            }

            RankDefinition rank;
            if (!TryGetActiveRank(player.userID, out rank))
            {
                rank = null;
            }

            var homeLimit = GetHomePointsLimit(player, rank);
            if (homeLimit <= 0)
            {
                error = "В вашей активной привилегии нет доступных home-точек.";
                return false;
            }

            var homes = GetPlayerHomes(player.UserIDString, false);
            if (homes == null || !homes.TryGetValue(homeName, out homeEntry) || homeEntry == null || homeEntry.Point == null)
            {
                error = $"Home '{homeName}' не найден. Создайте его: /sethome {homeName}.";
                return false;
            }

            return true;
        }

        private bool TryTeleportToPoint(BasePlayer player, TeleportPoint point, out string error)
        {
            error = string.Empty;
            if (player == null || !player.IsConnected)
            {
                error = "Игрок не найден.";
                return false;
            }

            if (player.IsDead())
            {
                error = "Нельзя телепортироваться, пока вы мертвы.";
                return false;
            }

            if (point == null)
            {
                error = "Точка телепорта не задана.";
                return false;
            }

            var destination = point.ToVector3() + new Vector3(0f, 0.15f, 0f);
            if (float.IsNaN(destination.x) || float.IsNaN(destination.y) || float.IsNaN(destination.z))
            {
                error = "Точка телепорта некорректна.";
                return false;
            }

            var fromPos = player.transform.position;
            PlayTeleportEffects(fromPos);
            player.Teleport(destination);
            PlayTeleportEffects(destination);
            return true;
        }

        private string BuildHomesSummary(BasePlayer player)
        {
            if (player == null) return "Игрок не найден.";

            RankDefinition rank;
            if (!TryGetActiveRank(player.userID, out rank))
            {
                rank = null;
            }

            var homeLimit = GetHomePointsLimit(player, rank);
            var homes = GetPlayerHomes(player.UserIDString, false);
            if (homes == null || homes.Count == 0)
            {
                return $"Дома: 0/{homeLimit}. Создайте: /sethome <name>.";
            }

            var names = new List<string>(homes.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return $"Дома: {homes.Count}/{homeLimit} -> {string.Join(", ", names.ToArray())}";
        }

        private string FormatTeleportPoint(TeleportPoint point)
        {
            if (point == null) return "не задана";
            return $"{point.X:0.0} {point.Y:0.0} {point.Z:0.0}";
        }

        private int GetHomeTeleportCooldownSeconds(RankDefinition rank)
        {
            var baseCd = config?.TeleportFeatures?.HomeBaseCooldownSeconds ?? 30;
            var reduction = rank?.HomeTeleportCooldownReductionSeconds ?? 0;
            return Math.Max(0, baseCd - reduction);
        }

        private int GetTeamTeleportCooldownSeconds(RankDefinition rank)
        {
            var baseCd = config?.TeleportFeatures?.TeamBaseCooldownSeconds ?? 15;
            var reduction = rank?.TeamTeleportCooldownReductionSeconds ?? 0;
            return Math.Max(0, baseCd - reduction);
        }

        private int GetTownTeleportDailyLimit(RankDefinition rank)
        {
            var baseLimit = config?.TeleportFeatures?.TownBaseDailyLimit ?? 10;
            var bonus = rank?.TownTeleportDailyLimitBonus ?? 0;
            return Math.Max(0, baseLimit + bonus);
        }

        private static string BuildRelayCommand(string template, string arg)
        {
            template = string.IsNullOrWhiteSpace(template) ? string.Empty : template.Trim();
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;

            if (!string.IsNullOrWhiteSpace(arg))
            {
                if (template.Contains("{0}"))
                {
                    return string.Format(template, arg);
                }

                return $"{template} {arg}";
            }

            return template;
        }

        private bool TryRelayChatCommand(BasePlayer player, string commandText, out string error)
        {
            error = string.Empty;
            if (player == null)
            {
                error = "Игрок не найден.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(commandText))
            {
                error = "Команда телепорта не настроена.";
                return false;
            }

            commandText = commandText.Trim().TrimStart('/');
            if (string.IsNullOrWhiteSpace(commandText))
            {
                error = "Команда телепорта пуста.";
                return false;
            }

            // Prevent accidental recursion if template points to this plugin commands.
            if (commandText.StartsWith("hometp", StringComparison.OrdinalIgnoreCase) ||
                commandText.StartsWith("towntp", StringComparison.OrdinalIgnoreCase) ||
                commandText.StartsWith("teamtp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(commandText, "priv", StringComparison.OrdinalIgnoreCase) ||
                commandText.StartsWith("priv ", StringComparison.OrdinalIgnoreCase))
            {
                error = "Шаблон телепорта указывает на команду PrivilegeSystem.";
                return false;
            }

            player.SendConsoleCommand("chat.say", "/" + commandText);
            if (config.TeleportFeatures != null && config.TeleportFeatures.ShowDebugReplies)
            {
                SendReply(player, $"Выполнена внешняя команда: /{commandText}");
            }
            return true;
        }

        private bool TryUseHomeTeleport(BasePlayer player, string homeName, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Игрок не найден.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(homeName))
            {
                message = "Использование: /hometp <home_name>";
                return false;
            }

            RankDefinition rank;
            if (!TryGetActiveRank(player.userID, out rank))
            {
                rank = null;
            }

            HomePointEntry homeEntry;
            string homeLookupError;
            if (!TryGetHomePoint(player, homeName, out homeEntry, out homeLookupError))
            {
                message = homeLookupError;
                return false;
            }

            var userId = player.UserIDString;
            var now = UtcNowUnix();
            var cooldown = GetHomeTeleportCooldownSeconds(rank);
            long nextUnix;
            if (storedData.HomeTpNextUseUnix.TryGetValue(userId, out nextUnix) && nextUnix > now)
            {
                message = $"КД home TP: {FormatRemaining(nextUnix)}";
                return false;
            }

            PendingHomeTeleport existingPending;
            if (pendingHomeTeleports.TryGetValue(player.userID, out existingPending) && existingPending != null)
            {
                var remaining = Math.Max(1L, existingPending.ExecuteAtUnix - UtcNowUnix());
                message = $"Home TP уже активируется. Осталось: {remaining}с.";
                return false;
            }

            var activationDelay = GetHomeTeleportActivationDelaySeconds(rank);
            if (activationDelay <= 0)
            {
                return FinishHomeTeleport(player, homeName, homeEntry.Point, cooldown, out message);
            }

            var executeAtUnix = UtcNowUnix() + activationDelay;
            var pending = new PendingHomeTeleport
            {
                ExecuteAtUnix = executeAtUnix,
                HomeName = homeName,
                Destination = homeEntry.Point
            };

            pending.Timer = timer.Once(activationDelay, () =>
            {
                PendingHomeTeleport active;
                if (!pendingHomeTeleports.TryGetValue(player.userID, out active) || active == null) return;
                pendingHomeTeleports.Remove(player.userID);

                var online = BasePlayer.FindByID(player.userID);
                if (online == null || !online.IsConnected) return;

                string finishMessage;
                if (!FinishHomeTeleport(online, active.HomeName, active.Destination, cooldown, out finishMessage))
                {
                    SendReply(online, finishMessage);
                    return;
                }

                SendReply(online, finishMessage);
            });

            pendingHomeTeleports[player.userID] = pending;
            PlayTeleportEffects(player.transform.position);
            message = $"Home TP активирован. Телепорт через {activationDelay}с.";
            return true;
        }

        private bool TryUseTeamTeleport(BasePlayer player, string teammateArg, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Игрок не найден.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(teammateArg))
            {
                message = "Использование: /teamtp <teammate>";
                return false;
            }

            BasePlayer teammate;
            if (!TryResolveOnlinePlayer(teammateArg, out teammate))
            {
                message = "Тиммейт не найден онлайн.";
                return false;
            }

            if (teammate.userID == player.userID)
            {
                message = "Нельзя телепортироваться к себе.";
                return false;
            }

            if (!IsSameTeam(player, teammate))
            {
                message = "Целевой игрок не в вашей команде.";
                return false;
            }

            RankDefinition rank;
            if (!TryGetActiveRank(player.userID, out rank))
            {
                rank = null;
            }

            var userId = player.UserIDString;
            var now = UtcNowUnix();
            var cooldown = GetTeamTeleportCooldownSeconds(rank);
            long nextUnix;
            if (storedData.TeamTpNextUseUnix.TryGetValue(userId, out nextUnix) && nextUnix > now)
            {
                message = $"КД team TP: {FormatRemaining(nextUnix)}";
                return false;
            }

            string teleportError;
            if (!TryTeleportToPoint(player, new TeleportPoint(teammate.transform.position + new Vector3(1.5f, 0f, 0f)), out teleportError))
            {
                message = teleportError;
                return false;
            }

            if (cooldown > 0)
            {
                storedData.TeamTpNextUseUnix[userId] = now + cooldown;
            }
            else
            {
                storedData.TeamTpNextUseUnix.Remove(userId);
            }
            SaveData();

            message = cooldown > 0 ? $"Телепортация к тиммейту выполнена. КД: {cooldown}с." : "Телепортация к тиммейту выполнена.";
            return true;
        }

        private bool TryUseTownTeleport(BasePlayer player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Игрок не найден.";
                return false;
            }

            RankDefinition rank;
            if (!TryGetActiveRank(player.userID, out rank))
            {
                rank = null;
            }

            var userId = player.UserIDString;
            var limit = GetTownTeleportDailyLimit(rank);
            TownTpUsageEntry usage;
            if (!storedData.TownTpUsageByUser.TryGetValue(userId, out usage) || usage == null)
            {
                usage = new TownTpUsageEntry();
                storedData.TownTpUsageByUser[userId] = usage;
            }

            var today = GetUtcDayKey();
            if (!string.Equals(usage.UtcDayKey, today, StringComparison.Ordinal))
            {
                usage.UtcDayKey = today;
                usage.Used = 0;
            }

            if (usage.Used >= limit)
            {
                message = $"Дневной лимит town TP достигнут: {usage.Used}/{limit}.";
                return false;
            }

            if (storedData.TownTeleportPoint == null)
            {
                message = "Точка town не настроена. Попросите админа выполнить /priv settown.";
                return false;
            }

            string teleportError;
            if (!TryTeleportToPoint(player, storedData.TownTeleportPoint, out teleportError))
            {
                message = teleportError;
                return false;
            }

            usage.Used++;
            SaveData();

            message = $"Town TP запущен. Использовано сегодня: {usage.Used}/{limit}.";
            return true;
        }

        private bool TryGetActiveRank(ulong userId, out RankDefinition rank)
        {
            rank = null;
            PrivilegeRecord record;
            return TryGetActivePrivilege(userId, out record, out rank) && rank != null;
        }

        private static int ToIntSafe(object value, int fallback = 0)
        {
            if (value == null) return fallback;
            if (value is int) return (int)value;
            if (value is long) return (int)(long)value;
            if (value is double) return (int)(double)value;
            if (value is float) return (int)(float)value;

            int parsedInt;
            if (int.TryParse(value.ToString(), out parsedInt))
            {
                return parsedInt;
            }

            return fallback;
        }

        private static double ToDoubleSafe(object value, double fallback = 0d)
        {
            if (value == null) return fallback;
            if (value is double) return (double)value;
            if (value is float) return (float)value;
            if (value is int) return (int)value;
            if (value is long) return (long)value;

            double parsed;
            if (double.TryParse(value.ToString(), out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static bool ToBoolSafe(object value, bool fallback = false)
        {
            if (value == null) return fallback;
            if (value is bool) return (bool)value;

            var raw = value.ToString();
            bool parsedBool;
            if (bool.TryParse(raw, out parsedBool))
            {
                return parsedBool;
            }

            int parsedInt;
            if (int.TryParse(raw, out parsedInt))
            {
                return parsedInt != 0;
            }

            return fallback;
        }

        private double GetEconomicsBalance(ulong userId)
        {
            if (Economics == null) return -1d;
            var result = Economics.Call("Balance", userId);
            if (result == null) result = Economics.Call("Balance", userId.ToString());
            return ToDoubleSafe(result, 0d);
        }

        private bool TryEconomicsWithdraw(ulong userId, double amount)
        {
            if (Economics == null) return false;

            var before = GetEconomicsBalance(userId);
            var result = Economics.Call("Withdraw", userId, amount);
            if (result == null) result = Economics.Call("Withdraw", userId.ToString(), amount);
            if (result != null && ToBoolSafe(result, false))
            {
                return true;
            }

            var after = GetEconomicsBalance(userId);
            return before >= 0d && after >= 0d && after <= before - amount + 0.0001d;
        }

        private int GetServerRewardsPoints(ulong userId)
        {
            if (ServerRewards == null) return -1;
            var result = ServerRewards.Call("CheckPoints", userId);
            if (result == null) result = ServerRewards.Call("CheckPoints", userId.ToString());
            return ToIntSafe(result, 0);
        }

        private bool TryTakeServerRewardsPoints(ulong userId, int amount)
        {
            if (ServerRewards == null) return false;

            var before = GetServerRewardsPoints(userId);
            var result = ServerRewards.Call("TakePoints", userId, amount);
            if (result == null) result = ServerRewards.Call("TakePoints", userId.ToString(), amount);
            if (result != null && ToBoolSafe(result, false))
            {
                return true;
            }

            var after = GetServerRewardsPoints(userId);
            return before >= 0 && after >= 0 && after <= before - amount;
        }

        private bool TryGetShopPackage(string key, out string packageKey, out ShopPackageConfig package)
        {
            packageKey = string.Empty;
            package = null;
            if (config.Shop == null || config.Shop.Packages == null)
            {
                return false;
            }

            key = NormalizeRankKey(key ?? string.Empty);
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (!config.Shop.Packages.TryGetValue(key, out package) || package == null)
            {
                return false;
            }

            packageKey = key;
            return true;
        }

        private bool TryBuyShopPackage(BasePlayer player, string key, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Игрок не найден.";
                return false;
            }

            if (config.Shop == null || !config.Shop.Enabled)
            {
                message = "Магазин привилегий отключен.";
                return false;
            }

            string packageKey;
            ShopPackageConfig package;
            if (!TryGetShopPackage(key, out packageKey, out package))
            {
                message = $"Пакет не найден: {key}";
                return false;
            }

            var rankKey = NormalizeRankKey(package.Rank ?? string.Empty);
            if (FindRank(rankKey) == null)
            {
                message = $"Некорректный ранг пакета: {rankKey}";
                return false;
            }

            var currency = NormalizeShopCurrency(config.Shop.Currency);
            var chargedText = string.Empty;
            if (currency == "serverrewards")
            {
                var price = Math.Max(0, package.ServerRewardsPrice);
                if (price <= 0)
                {
                    message = "Цена пакета для ServerRewards не настроена.";
                    return false;
                }

                var points = GetServerRewardsPoints(player.userID);
                if (points < price)
                {
                    message = $"Недостаточно RP. Нужно: {price}, у вас: {points}.";
                    return false;
                }

                if (!TryTakeServerRewardsPoints(player.userID, price))
                {
                    message = "Не удалось списать очки ServerRewards.";
                    return false;
                }

                chargedText = $"{price} RP";
            }
            else
            {
                var price = Math.Max(0d, package.EconomicsPrice);
                if (price <= 0d)
                {
                    message = "Цена пакета для Economics не настроена.";
                    return false;
                }

                var balance = GetEconomicsBalance(player.userID);
                if (balance < price)
                {
                    message = $"Недостаточно денег. Нужно: {price:0.##}, у вас: {balance:0.##}.";
                    return false;
                }

                if (!TryEconomicsWithdraw(player.userID, price))
                {
                    message = "Не удалось списать баланс Economics.";
                    return false;
                }

                chargedText = $"{price:0.##}$";
            }

            string error;
            if (!SetPrivilege(player.userID, player.displayName, rankKey, package.Days, "Shop", player.userID, out error))
            {
                message = "Покупка привилегии не удалась: " + error;
                return false;
            }

            message = $"Куплено {package.DisplayName}: ранг={rankKey}, дней={(package.Days == 0 ? "навсегда" : package.Days.ToString())}, списано={chargedText}.";
            WriteAudit(
                "shop.buy",
                player.userID,
                player.displayName,
                player.userID,
                player.displayName,
                $"package={packageKey}; rank={rankKey}; days={package.Days}; charged={chargedText}",
                true);
            return true;
        }

        private string GetShopPriceText(ShopPackageConfig package, string currency)
        {
            if (package == null) return "-";
            currency = NormalizeShopCurrency(currency);
            if (currency == "serverrewards")
            {
                return $"{Math.Max(0, package.ServerRewardsPrice)} RP";
            }

            return $"{Math.Max(0d, package.EconomicsPrice):0.##}$";
        }

        private void WriteAudit(string action, ulong actorId, string actorName, ulong targetId, string targetName, string details, bool save)
        {
            if (config.Audit == null || !config.Audit.Enabled)
            {
                return;
            }

            var entry = new AuditLogEntry
            {
                Unix = UtcNowUnix(),
                Action = action ?? string.Empty,
                ActorId = actorId,
                ActorName = string.IsNullOrWhiteSpace(actorName) ? actorId.ToString() : actorName,
                TargetId = targetId,
                TargetName = string.IsNullOrWhiteSpace(targetName) ? targetId.ToString() : targetName,
                Details = details ?? string.Empty
            };

            storedData.AuditLog.Add(entry);
            var extra = storedData.AuditLog.Count - config.Audit.MaxEntries;
            if (extra > 0)
            {
                storedData.AuditLog.RemoveRange(0, extra);
            }

            if (config.Audit.EchoToConsole)
            {
                Puts($"AUDIT {entry.Unix} {entry.Action} actor={entry.ActorName}({entry.ActorId}) target={entry.TargetName}({entry.TargetId}) details={entry.Details}");
            }

            if (save)
            {
                SaveData();
            }
        }

        private List<string> BuildAuditTailLines(int count)
        {
            var lines = new List<string>();
            if (storedData.AuditLog == null || storedData.AuditLog.Count == 0)
            {
                lines.Add("Audit is empty.");
                return lines;
            }

            if (count < 1) count = 1;
            if (count > 50) count = 50;

            var start = Math.Max(0, storedData.AuditLog.Count - count);
            for (var i = start; i < storedData.AuditLog.Count; i++)
            {
                var entry = storedData.AuditLog[i];
                if (entry == null) continue;
                lines.Add($"{entry.Unix} | {entry.Action} | {entry.ActorName}({entry.ActorId}) -> {entry.TargetName}({entry.TargetId}) | {entry.Details}");
            }

            return lines;
        }

        private bool TryResolvePlayerIdentity(string input, out ulong userId, out string name)
        {
            userId = 0;
            name = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var exact = BasePlayer.FindAwakeOrSleeping(input);
            if (exact != null)
            {
                userId = exact.userID;
                name = exact.displayName;
                return true;
            }

            var search = input.ToLowerInvariant();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player?.displayName == null) continue;
                if (!player.displayName.ToLowerInvariant().Contains(search)) continue;
                userId = player.userID;
                name = player.displayName;
                return true;
            }

            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                if (player?.displayName == null) continue;
                if (!player.displayName.ToLowerInvariant().Contains(search)) continue;
                userId = player.userID;
                name = player.displayName;
                return true;
            }

            ulong parsedId;
            if (ulong.TryParse(input, out parsedId) && parsedId > 0)
            {
                userId = parsedId;
                name = ResolvePlayerName(parsedId);
                return true;
            }

            return false;
        }

        private string ResolvePlayerName(ulong userId)
        {
            var online = BasePlayer.FindByID(userId) ?? BasePlayer.FindSleeping(userId);
            if (online != null && !string.IsNullOrWhiteSpace(online.displayName))
            {
                return online.displayName;
            }

            PrivilegeRecord existing;
            if (storedData.Players.TryGetValue(userId.ToString(), out existing) && !string.IsNullOrWhiteSpace(existing.LastKnownName))
            {
                return existing.LastKnownName;
            }

            return userId.ToString();
        }

        private void EnsureGroupExists(string groupName, string title)
        {
            if (string.IsNullOrWhiteSpace(groupName)) return;
            if (permission.GroupExists(groupName)) return;
            permission.CreateGroup(groupName, title, 0);
        }

        private void ClearManagedAccess(string userId)
        {
            foreach (var pair in config.Ranks)
            {
                var rank = pair.Value;
                if (rank == null) continue;

                foreach (var permissionName in GetManagedPermissions(rank))
                {
                    if (string.IsNullOrWhiteSpace(permissionName)) continue;
                    if (permission.UserHasPermission(userId, permissionName))
                    {
                        permission.RevokeUserPermission(userId, permissionName);
                    }
                }

                if (!string.IsNullOrWhiteSpace(rank.OxideGroup) && permission.UserHasGroup(userId, rank.OxideGroup))
                {
                    permission.RemoveUserGroup(userId, rank.OxideGroup);
                }
            }
        }

        private bool ApplyRecord(string userId, PrivilegeRecord record, out string error)
        {
            error = string.Empty;
            if (record == null)
            {
                error = "запись привилегии отсутствует";
                return false;
            }

            var rank = FindRank(record.Rank);
            if (rank == null)
            {
                error = $"ранг '{record.Rank}' не найден";
                return false;
            }

            ClearManagedAccess(userId);

            if (!string.IsNullOrWhiteSpace(rank.OxideGroup))
            {
                EnsureGroupExists(rank.OxideGroup, rank.DisplayName);
                permission.AddUserGroup(userId, rank.OxideGroup);
            }

            foreach (var permissionName in GetManagedPermissions(rank))
            {
                if (string.IsNullOrWhiteSpace(permissionName)) continue;
                if (!permission.PermissionExists(permissionName))
                {
                    if (permissionName.StartsWith("privilegesystem.", StringComparison.OrdinalIgnoreCase))
                    {
                        permission.RegisterPermission(permissionName, this);
                    }
                    else
                    {
                        if (warnedMissingPermissions.Add(permissionName))
                        {
                            PrintWarning($"Permission '{permissionName}' is not registered by any plugin. Skipping grant.");
                        }
                        continue;
                    }
                }
                permission.GrantUserPermission(userId, permissionName, this);
            }

            return true;
        }

        private bool RemoveRecord(string userId, ulong actorId, string actorName, string reason)
        {
            PrivilegeRecord existing;
            if (!storedData.Players.TryGetValue(userId, out existing) || existing == null)
            {
                return false;
            }

            storedData.Players.Remove(userId);
            storedData.RankKitNextClaimUnix.Remove(userId);
            storedData.DailyNextClaimUnix.Remove(userId);
            storedData.HomeTpNextUseUnix.Remove(userId);
            storedData.TeamTpNextUseUnix.Remove(userId);
            storedData.TownTpUsageByUser.Remove(userId);
            storedData.PocketRecyclerNextUseUnix.Remove(userId);
            ClearManagedAccess(userId);
            ulong parsedRemoveModeId;
            if (ulong.TryParse(userId, out parsedRemoveModeId))
            {
                DisableRemoveMode(parsedRemoveModeId);
                DestroyPersonalRecycler(parsedRemoveModeId);
            }

            ulong targetId;
            ulong.TryParse(userId, out targetId);
            var targetName = string.IsNullOrWhiteSpace(existing.LastKnownName) ? ResolvePlayerName(targetId) : existing.LastKnownName;
            WriteAudit(
                "priv.remove",
                actorId,
                actorName,
                targetId,
                targetName,
                $"rank={existing.Rank}; reason={reason}",
                false);

            SaveData();
            return true;
        }

        private bool SetPrivilege(ulong userId, string playerName, string rankKey, int days, string grantedBy, ulong grantedById, out string error)
        {
            error = string.Empty;

            if (userId == 0)
            {
                error = "Некорректный user id.";
                return false;
            }

            rankKey = NormalizeRankKey(rankKey);
            var rank = FindRank(rankKey);
            if (rank == null)
            {
                error = $"Ранг '{rankKey}' не существует.";
                return false;
            }

            if (days < 0)
            {
                error = "Количество дней не может быть отрицательным.";
                return false;
            }

            var userIdString = userId.ToString();
            var now = UtcNowUnix();

            var record = new PrivilegeRecord
            {
                Rank = rankKey,
                GrantedAtUnix = now,
                GrantedBy = string.IsNullOrWhiteSpace(grantedBy) ? "System" : grantedBy,
                ExpiresAtUnix = days == 0 ? 0 : now + (days * 86400L),
                LastKnownName = string.IsNullOrWhiteSpace(playerName) ? ResolvePlayerName(userId) : playerName
            };

            string applyError;
            if (!ApplyRecord(userIdString, record, out applyError))
            {
                error = $"Ошибка применения: {applyError}";
                return false;
            }

            PrivilegeRecord previous;
            storedData.Players.TryGetValue(userIdString, out previous);

            storedData.RankKitNextClaimUnix.Remove(userIdString);
            storedData.DailyNextClaimUnix.Remove(userIdString);
            storedData.HomeTpNextUseUnix.Remove(userIdString);
            storedData.TeamTpNextUseUnix.Remove(userIdString);
            storedData.TownTpUsageByUser.Remove(userIdString);
            storedData.PocketRecyclerNextUseUnix.Remove(userIdString);
            DisableRemoveMode(userId);
            DestroyPersonalRecycler(userId);
            storedData.Players[userIdString] = record;

            WriteAudit(
                "priv.set",
                grantedById,
                grantedBy,
                userId,
                record.LastKnownName,
                $"rank={rankKey}; days={days}; prev={(previous != null ? previous.Rank : "none")}",
                false);

            SaveData();
            return true;
        }

        private bool ExtendPrivilege(ulong userId, int days, ulong actorId, string actorName, out string error)
        {
            error = string.Empty;

            if (days <= 0)
            {
                error = "Количество дней должно быть больше нуля.";
                return false;
            }

            PrivilegeRecord record;
            if (!storedData.Players.TryGetValue(userId.ToString(), out record))
            {
                error = "Запись привилегии не найдена.";
                return false;
            }

            if (record.ExpiresAtUnix == 0)
            {
                error = "Привилегия постоянная, продлевать нечего.";
                return false;
            }

            var now = UtcNowUnix();
            if (record.ExpiresAtUnix < now)
            {
                record.ExpiresAtUnix = now;
            }

            var oldExpires = record.ExpiresAtUnix;
            record.ExpiresAtUnix += days * 86400L;

            WriteAudit(
                "priv.extend",
                actorId,
                actorName,
                userId,
                ResolvePlayerName(userId),
                $"days={days}; old={oldExpires}; new={record.ExpiresAtUnix}",
                false);

            SaveData();
            return true;
        }

        private void CleanupExpired(bool notifyOnline)
        {
            var now = UtcNowUnix();
            var expiredUserIds = new List<string>();

            foreach (var pair in storedData.Players)
            {
                var record = pair.Value;
                if (record == null) continue;
                if (record.ExpiresAtUnix == 0) continue;
                if (record.ExpiresAtUnix > now) continue;
                expiredUserIds.Add(pair.Key);
            }

            if (expiredUserIds.Count == 0) return;

            foreach (var userId in expiredUserIds)
            {
                PrivilegeRecord record;
                if (!storedData.Players.TryGetValue(userId, out record)) continue;

                var playerName = string.IsNullOrWhiteSpace(record.LastKnownName) ? userId : record.LastKnownName;
                ClearManagedAccess(userId);
                storedData.Players.Remove(userId);
                storedData.RankKitNextClaimUnix.Remove(userId);
                storedData.DailyNextClaimUnix.Remove(userId);
                storedData.HomeTpNextUseUnix.Remove(userId);
                storedData.TeamTpNextUseUnix.Remove(userId);
                storedData.TownTpUsageByUser.Remove(userId);
                storedData.PocketRecyclerNextUseUnix.Remove(userId);
                ulong expiredId;
                if (ulong.TryParse(userId, out expiredId))
                {
                    DisableRemoveMode(expiredId);
                    DestroyPersonalRecycler(expiredId);
                }
                ulong targetId;
                ulong.TryParse(userId, out targetId);
                WriteAudit(
                    "priv.expire",
                    0,
                    "System",
                    targetId,
                    playerName,
                    $"rank={record.Rank}",
                    false);
                Puts($"Privilege expired for {playerName} ({userId}), rank: {record.Rank}");

                if (!notifyOnline) continue;

                BasePlayer online;
                if (ulong.TryParse(userId, out var parsedId))
                {
                    online = BasePlayer.FindByID(parsedId);
                    if (online != null)
                    {
                        SendReply(online, "Срок действия вашей привилегии истек.");
                    }
                }
            }

            SaveData();
        }

        private void ReapplyPrivileges()
        {
            foreach (var pair in storedData.Players)
            {
                string ignoreError;
                ApplyRecord(pair.Key, pair.Value, out ignoreError);
            }
        }

        private string FormatRemaining(long expiresAtUnix)
        {
            if (expiresAtUnix == 0)
            {
                return "навсегда";
            }

            var seconds = Math.Max(0, expiresAtUnix - UtcNowUnix());
            var span = TimeSpan.FromSeconds(seconds);

            if (span.TotalDays >= 1d)
            {
                return $"{(int)span.TotalDays}д {span.Hours}ч";
            }

            if (span.TotalHours >= 1d)
            {
                return $"{(int)span.TotalHours}ч {span.Minutes}м";
            }

            return $"{span.Minutes}м {span.Seconds}с";
        }

        private string BuildPlayerStatus(ulong userId)
        {
            PrivilegeRecord record;
            if (!storedData.Players.TryGetValue(userId.ToString(), out record) || record == null)
            {
                return "Нет активной привилегии.";
            }

            var rank = FindRank(record.Rank);
            var rankName = rank != null ? rank.DisplayName : record.Rank;
            return $"Ранг: {rankName}, осталось: {FormatRemaining(record.ExpiresAtUnix)}, бонусы: {BuildPerkSummary(rank)}";
        }

        private void EnsureUiSelectedTarget(BasePlayer admin)
        {
            if (admin == null) return;

            ulong selectedId;
            if (uiSelectedTarget.TryGetValue(admin.userID, out selectedId))
            {
                var selectedPlayer = BasePlayer.FindByID(selectedId) ?? BasePlayer.FindSleeping(selectedId);
                if (selectedPlayer != null) return;

                PrivilegeRecord record;
                if (storedData.Players.TryGetValue(selectedId.ToString(), out record) && record != null)
                {
                    return;
                }
            }

            var fallback = admin.userID;
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                fallback = player.userID;
                break;
            }

            uiSelectedTarget[admin.userID] = fallback;
        }

                private void EnsureUiSelectedRank(BasePlayer admin)
        {
            if (admin == null) return;

            string selectedRank;
            if (uiSelectedRank.TryGetValue(admin.userID, out selectedRank))
            {
                if (FindRank(selectedRank) != null)
                {
                    return;
                }
            }

            foreach (var pair in config.Ranks)
            {
                uiSelectedRank[admin.userID] = pair.Key;
                return;
            }

            uiSelectedRank[admin.userID] = "vip";
        }

        private string GetUiActiveTab(BasePlayer admin)
        {
            if (admin == null) return "manage";

            string tab;
            if (!uiActiveTab.TryGetValue(admin.userID, out tab))
            {
                tab = "manage";
                uiActiveTab[admin.userID] = tab;
            }

            return string.Equals(tab, "settings", StringComparison.OrdinalIgnoreCase) ? "settings" : "manage";
        }

        private void SetUiActiveTab(BasePlayer admin, string tab)
        {
            if (admin == null) return;
            uiActiveTab[admin.userID] = string.Equals(tab, "settings", StringComparison.OrdinalIgnoreCase) ? "settings" : "manage";
        }

        private void OpenAdminUi(BasePlayer admin)
        {
            if (admin == null || !HasAdminAccess(admin)) return;

            EnsureUiSelectedTarget(admin);
            EnsureUiSelectedRank(admin);

            DestroyAdminUi(admin);
            uiViewers.Add(admin.userID);

            var activeTab = GetUiActiveTab(admin);

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                Image = { Color = "0.08 0.09 0.12 0.96" },
                RectTransform = { AnchorMin = "0.02 0.08", AnchorMax = "0.61 0.92" },
                CursorEnabled = true
            }, "Overlay", UiRoot);

            container.Add(new CuiLabel
            {
                Text = { Text = "PRIVILEGE ADMIN PANEL", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 0.85 0.45 1" },
                RectTransform = { AnchorMin = "0.02 0.94", AnchorMax = "0.98 0.99" }
            }, UiRoot);

            AddUiButton(
                container,
                UiRoot,
                "0.02 0.89",
                "0.17 0.935",
                activeTab == "manage" ? "0.27 0.54 0.26 0.95" : "0.22 0.34 0.62 0.95",
                "Manage",
                "priv.ui.tab manage");
            AddUiButton(
                container,
                UiRoot,
                "0.18 0.89",
                "0.33 0.935",
                activeTab == "settings" ? "0.27 0.54 0.26 0.95" : "0.22 0.34 0.62 0.95",
                "Settings",
                "priv.ui.tab settings");
            AddUiButton(container, UiRoot, "0.34 0.89", "0.49 0.935", "0.22 0.34 0.62 0.95", "Refresh", "priv.ui.refresh");
            AddUiButton(container, UiRoot, "0.83 0.89", "0.98 0.935", "0.20 0.20 0.20 0.95", "Close", "priv.ui.close");

            if (activeTab == "settings")
            {
                RenderSettingsUi(container, admin);
            }
            else
            {
                RenderManageUi(container, admin);
            }

            CuiHelper.AddUi(admin, container);
        }

        private void RenderManageUi(CuiElementContainer container, BasePlayer admin)
        {
            var selectedId = uiSelectedTarget[admin.userID];
            var targetName = ResolvePlayerName(selectedId);

            container.Add(new CuiPanel
            {
                Image = { Color = "0.13 0.14 0.18 0.98" },
                RectTransform = { AnchorMin = "0.02 0.70", AnchorMax = "0.98 0.88" }
            }, UiRoot, UiRoot + ".status");

            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = $"Selected: {targetName} ({selectedId})\n{BuildPlayerStatus(selectedId)}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.92 0.96 1 1"
                },
                RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.98 0.95" }
            }, UiRoot + ".status");

            AddUiButton(container, UiRoot, "0.02 0.62", "0.33 0.68", "0.21 0.52 0.25 0.95", "VIP 7d", $"priv.ui.grant {selectedId} vip 7");
            AddUiButton(container, UiRoot, "0.345 0.62", "0.655 0.68", "0.21 0.52 0.25 0.95", "VIP 30d", $"priv.ui.grant {selectedId} vip 30");
            AddUiButton(container, UiRoot, "0.67 0.62", "0.98 0.68", "0.21 0.52 0.25 0.95", "VIP PERM", $"priv.ui.grant {selectedId} vip 0");

            AddUiButton(container, UiRoot, "0.02 0.54", "0.33 0.60", "0.22 0.43 0.66 0.95", "PREM 7d", $"priv.ui.grant {selectedId} premium 7");
            AddUiButton(container, UiRoot, "0.345 0.54", "0.655 0.60", "0.22 0.43 0.66 0.95", "PREM 30d", $"priv.ui.grant {selectedId} premium 30");
            AddUiButton(container, UiRoot, "0.67 0.54", "0.98 0.60", "0.22 0.43 0.66 0.95", "PREM PERM", $"priv.ui.grant {selectedId} premium 0");

            AddUiButton(container, UiRoot, "0.02 0.46", "0.33 0.52", "0.64 0.39 0.18 0.95", "ELITE 7d", $"priv.ui.grant {selectedId} elite 7");
            AddUiButton(container, UiRoot, "0.345 0.46", "0.655 0.52", "0.64 0.39 0.18 0.95", "ELITE 30d", $"priv.ui.grant {selectedId} elite 30");
            AddUiButton(container, UiRoot, "0.67 0.46", "0.98 0.52", "0.64 0.39 0.18 0.95", "ELITE PERM", $"priv.ui.grant {selectedId} elite 0");

            AddUiButton(container, UiRoot, "0.02 0.38", "0.33 0.44", "0.30 0.30 0.30 0.95", "Extend +7d", $"priv.ui.extend {selectedId} 7");
            AddUiButton(container, UiRoot, "0.345 0.38", "0.655 0.44", "0.30 0.30 0.30 0.95", "Extend +30d", $"priv.ui.extend {selectedId} 30");
            AddUiButton(container, UiRoot, "0.67 0.38", "0.98 0.44", "0.70 0.20 0.20 0.95", "Remove", $"priv.ui.remove {selectedId}");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.13 0.16 0.98" },
                RectTransform = { AnchorMin = "0.02 0.03", AnchorMax = "0.98 0.34" }
            }, UiRoot, UiRoot + ".players");

            container.Add(new CuiLabel
            {
                Text = { Text = "Online players (click to select)", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.95 0.95 0.95 1" },
                RectTransform = { AnchorMin = "0.02 0.88", AnchorMax = "0.98 0.98" }
            }, UiRoot + ".players");

            var onlinePlayers = new List<BasePlayer>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                onlinePlayers.Add(player);
            }
            onlinePlayers.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));

            var maxRows = 8;
            for (var i = 0; i < onlinePlayers.Count && i < maxRows; i++)
            {
                var player = onlinePlayers[i];
                var rowTop = 0.84f - (i * 0.10f);
                var rowBottom = rowTop - 0.08f;
                var isSelected = player.userID == selectedId;
                var rowColor = isSelected ? "0.24 0.50 0.24 0.95" : "0.20 0.22 0.26 0.95";
                var text = $"{player.displayName} ({player.userID})";
                AddUiButton(
                    container,
                    UiRoot + ".players",
                    $"0.02 {rowBottom:0.00}",
                    $"0.98 {rowTop:0.00}",
                    rowColor,
                    text,
                    $"priv.ui.select {player.userID}",
                    11);
            }

            if (onlinePlayers.Count == 0)
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = "No online players", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.7 0.7 0.7 1" },
                    RectTransform = { AnchorMin = "0.02 0.10", AnchorMax = "0.98 0.80" }
                }, UiRoot + ".players");
            }
        }

        private void RenderSettingsUi(CuiElementContainer container, BasePlayer admin)
        {
            var rankKey = uiSelectedRank[admin.userID];
            var rank = FindRank(rankKey);
            if (rank == null)
            {
                EnsureUiSelectedRank(admin);
                rankKey = uiSelectedRank[admin.userID];
                rank = FindRank(rankKey);
                if (rank == null) return;
            }

            container.Add(new CuiPanel
            {
                Image = { Color = "0.13 0.14 0.18 0.98" },
                RectTransform = { AnchorMin = "0.02 0.66", AnchorMax = "0.98 0.88" }
            }, UiRoot, UiRoot + ".cfgstatus");

            container.Add(new CuiLabel
            {
                Text =
                {
                    Text =
                        $"Editing rank: {rank.DisplayName} ({rankKey})\n" +
                        $"Node gather: x{rank.GatherMultiplier:0.##} | Ground gather: x{rank.GroundPickupMultiplier:0.##}\n" +
                        $"Container loot: x{rank.ContainerLootMultiplier:0.##} | Kit amount: x{rank.RankKitAmountMultiplier:0.##}\n" +
                        $"Kit cooldown: {FormatDurationSeconds(rank.RankKitCooldownSeconds)} | Daily x{rank.DailyRewardMultiplier:0.##}\n" +
                        $"Home cd -{rank.HomeTeleportCooldownReductionSeconds}s | Team cd -{rank.TeamTeleportCooldownReductionSeconds}s | Town +{rank.TownTeleportDailyLimitBonus}",
                    FontSize = 12,
                    Align = TextAnchor.UpperLeft,
                    Color = "0.92 0.96 1 1"
                },
                RectTransform = { AnchorMin = "0.02 0.06", AnchorMax = "0.98 0.95" }
            }, UiRoot + ".cfgstatus");

            var rankKeys = new List<string>(config.Ranks.Keys);
            rankKeys.Sort(StringComparer.OrdinalIgnoreCase);
            var rankButtonWidth = rankKeys.Count > 0 ? 0.96f / rankKeys.Count : 0.96f;
            for (var i = 0; i < rankKeys.Count; i++)
            {
                var key = rankKeys[i];
                var start = 0.02f + (rankButtonWidth * i);
                var end = start + rankButtonWidth - 0.01f;
                var isSelected = string.Equals(key, rankKey, StringComparison.OrdinalIgnoreCase);
                AddUiButton(
                    container,
                    UiRoot,
                    $"{start:0.00} 0.59",
                    $"{end:0.00} 0.64",
                    isSelected ? "0.25 0.50 0.25 0.95" : "0.24 0.33 0.58 0.95",
                    key.ToUpperInvariant(),
                    $"priv.ui.rank {key}");
            }

            AddSettingRow(container, rankKey, "Node Gather", "gather", 0.1f, 0.53f, rank.GatherMultiplier, "x");
            AddSettingRow(container, rankKey, "Ground Gather", "ground", 0.1f, 0.47f, rank.GroundPickupMultiplier, "x");
            AddSettingRow(container, rankKey, "Container Loot", "loot", 0.1f, 0.41f, rank.ContainerLootMultiplier, "x");
            AddSettingRow(container, rankKey, "Kit Amount", "kitmul", 0.1f, 0.35f, rank.RankKitAmountMultiplier, "x");
            AddSettingRow(container, rankKey, "Kit Cooldown (h)", "kitcdh", 1f, 0.29f, rank.RankKitCooldownSeconds / 3600f, "h");
            AddSettingRow(container, rankKey, "Daily Multiplier", "dailymul", 0.1f, 0.23f, rank.DailyRewardMultiplier, "x");
            AddSettingRow(container, rankKey, "Home TP CD Reduction", "homecd", 1f, 0.17f, rank.HomeTeleportCooldownReductionSeconds, "s");
            AddSettingRow(container, rankKey, "Team TP CD Reduction", "teamcd", 1f, 0.11f, rank.TeamTeleportCooldownReductionSeconds, "s");
            AddSettingRow(container, rankKey, "Town TP Daily Bonus", "townlim", 1f, 0.05f, rank.TownTeleportDailyLimitBonus, "");

            container.Add(new CuiPanel
            {
                Image = { Color = "0.12 0.13 0.16 0.98" },
                RectTransform = { AnchorMin = "0.02 0.00", AnchorMax = "0.98 0.04" }
            }, UiRoot, UiRoot + ".cfgnote");

            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = "Settings apply instantly.",
                    FontSize = 11,
                    Align = TextAnchor.MiddleLeft,
                    Color = "0.88 0.9 0.94 1"
                },
                RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.98 0.98" }
            }, UiRoot + ".cfgnote");
        }

        private void AddSettingRow(CuiElementContainer container, string rankKey, string title, string field, float step, float yMin, float currentValue, string suffix)
        {
            var yMax = yMin + 0.055f;
            container.Add(new CuiLabel
            {
                Text = { Text = $"{title}: {currentValue:0.##}{suffix}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.95 0.95 0.95 1" },
                RectTransform = { AnchorMin = $"0.02 {yMin:0.00}", AnchorMax = $"0.52 {yMax:0.00}" }
            }, UiRoot);

            AddUiButton(
                container,
                UiRoot,
                $"0.54 {yMin:0.00}",
                $"0.64 {yMax:0.00}",
                "0.55 0.20 0.20 0.95",
                $"-{step:0.##}",
                $"priv.ui.cfg {rankKey} {field} {-step:0.##}");
            AddUiButton(
                container,
                UiRoot,
                $"0.66 {yMin:0.00}",
                $"0.76 {yMax:0.00}",
                "0.20 0.55 0.25 0.95",
                $"+{step:0.##}",
                $"priv.ui.cfg {rankKey} {field} {step:0.##}");
        }

        private void DestroyAdminUi(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, UiRoot);
        }

        private void RefreshAdminUiForViewers()
        {
            if (uiViewers.Count == 0) return;

            var stale = new List<ulong>();
            foreach (var userId in uiViewers)
            {
                var admin = BasePlayer.FindByID(userId);
                if (admin == null || !admin.IsConnected)
                {
                    stale.Add(userId);
                    continue;
                }

                OpenAdminUi(admin);
            }

            foreach (var userId in stale)
            {
                uiViewers.Remove(userId);
                uiSelectedTarget.Remove(userId);
                uiSelectedRank.Remove(userId);
                uiActiveTab.Remove(userId);
            }
        }

        private void UiGrant(BasePlayer admin, ulong targetId, string rank, int days)
        {
            var targetName = ResolvePlayerName(targetId);
            string error;
            if (!SetPrivilege(targetId, targetName, rank, days, admin.displayName, admin.userID, out error))
            {
                SendReply(admin, "Ошибка: " + error);
                return;
            }

            SendReply(admin, $"Привилегия выдана: {targetName} ({targetId}) -> {NormalizeRankKey(rank)} {(days == 0 ? "(навсегда)" : $"({days}д)")}");
            var target = BasePlayer.FindByID(targetId);
            if (target != null)
            {
                SendReply(target, $"Вам выдана привилегия: {BuildPlayerStatus(targetId)}");
            }
        }

        private void UiExtend(BasePlayer admin, ulong targetId, int days)
        {
            string error;
            if (!ExtendPrivilege(targetId, days, admin.userID, admin.displayName, out error))
            {
                SendReply(admin, "Ошибка: " + error);
                return;
            }

            SendReply(admin, $"Привилегия продлена для {ResolvePlayerName(targetId)} ({targetId}) на {days} дн.");
        }

        private void UiRemove(BasePlayer admin, ulong targetId)
        {
            if (!RemoveRecord(targetId.ToString(), admin.userID, admin.displayName, "ui remove"))
            {
                SendReply(admin, "Нет записи привилегии для удаления.");
                return;
            }

            SendReply(admin, $"Привилегия снята: {ResolvePlayerName(targetId)} ({targetId}).");
            var target = BasePlayer.FindByID(targetId);
            if (target != null)
            {
                SendReply(target, "Ваша привилегия снята.");
            }
        }

        private void UiAdjustRankSetting(BasePlayer admin, string rankKey, string field, float delta)
        {
            if (admin == null) return;

            rankKey = NormalizeRankKey(rankKey);
            var rank = FindRank(rankKey);
            if (rank == null)
            {
                SendReply(admin, $"Ранг не найден: {rankKey}");
                return;
            }

            field = (field ?? string.Empty).Trim().ToLowerInvariant();
            switch (field)
            {
                case "gather":
                    rank.GatherMultiplier = Mathf.Clamp(rank.GatherMultiplier + delta, 1f, 10f);
                    SendReply(admin, $"[{rankKey}] Добыча с нод -> x{rank.GatherMultiplier:0.##}");
                    break;
                case "ground":
                    rank.GroundPickupMultiplier = Mathf.Clamp(rank.GroundPickupMultiplier + delta, 1f, 10f);
                    SendReply(admin, $"[{rankKey}] Подбор с земли -> x{rank.GroundPickupMultiplier:0.##}");
                    break;
                case "loot":
                    rank.ContainerLootMultiplier = Mathf.Clamp(rank.ContainerLootMultiplier + delta, 1f, 10f);
                    SendReply(admin, $"[{rankKey}] Лут контейнеров -> x{rank.ContainerLootMultiplier:0.##}");
                    break;
                case "kitmul":
                    rank.RankKitAmountMultiplier = Mathf.Clamp(rank.RankKitAmountMultiplier + delta, 1f, 20f);
                    SendReply(admin, $"[{rankKey}] Количество rank kit -> x{rank.RankKitAmountMultiplier:0.##}");
                    break;
                case "kitcdh":
                    var newHours = Mathf.Clamp((rank.RankKitCooldownSeconds / 3600f) + delta, 0f, 168f);
                    rank.RankKitCooldownSeconds = Mathf.RoundToInt(newHours * 3600f);
                    SendReply(admin, $"[{rankKey}] КД rank kit -> {FormatDurationSeconds(rank.RankKitCooldownSeconds)}");
                    break;
                case "dailymul":
                    rank.DailyRewardMultiplier = Mathf.Clamp(rank.DailyRewardMultiplier + delta, 0.1f, 20f);
                    SendReply(admin, $"[{rankKey}] Множитель daily -> x{rank.DailyRewardMultiplier:0.##}");
                    break;
                case "homecd":
                    var newHomeCd = Mathf.Clamp(rank.HomeTeleportCooldownReductionSeconds + delta, 0f, 600f);
                    rank.HomeTeleportCooldownReductionSeconds = Mathf.RoundToInt(newHomeCd);
                    SendReply(admin, $"[{rankKey}] Снижение КД home TP -> {rank.HomeTeleportCooldownReductionSeconds}с");
                    break;
                case "teamcd":
                    var newTeamCd = Mathf.Clamp(rank.TeamTeleportCooldownReductionSeconds + delta, 0f, 600f);
                    rank.TeamTeleportCooldownReductionSeconds = Mathf.RoundToInt(newTeamCd);
                    SendReply(admin, $"[{rankKey}] Снижение КД team TP -> {rank.TeamTeleportCooldownReductionSeconds}с");
                    break;
                case "townlim":
                    var newTownBonus = Mathf.Clamp(rank.TownTeleportDailyLimitBonus + delta, 0f, 1000f);
                    rank.TownTeleportDailyLimitBonus = Mathf.RoundToInt(newTownBonus);
                    SendReply(admin, $"[{rankKey}] Бонус дневного лимита town TP -> +{rank.TownTeleportDailyLimitBonus}");
                    break;
                default:
                    SendReply(admin, $"Неизвестный параметр: {field}");
                    return;
            }

            SaveConfig();
        }

        private static void AddUiButton(CuiElementContainer container, string parent, string anchorMin, string anchorMax, string color, string text, string command, int fontSize = 12)
        {
            container.Add(new CuiButton
            {
                Button = { Color = color, Command = command },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                Text = { Text = text, FontSize = fontSize, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, parent);
        }

        private void SendUsage(BasePlayer player)
        {
            SendReply(player, "Команды: /priv my | /rankkit | /daily | /pshop [buy <package>] | /priv activate | /remove [off] | /recycler [off] | /sethome <name> | /home <name> | /homes | /removehome <name> | /hometp <home> | /towntp | /teamtp <teammate> | /privui | /priv ui | /priv list | /priv add <player/id> <rank> [days] | /priv remove <player/id> | /priv extend <player/id> <days> | /priv info <player/id> | /priv audit [count] | /priv shopsync | /priv settown | /priv cleartown | /priv townpoint");
        }

        [ChatCommand("vip")]
        private void VipChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            SendReply(player, BuildPlayerStatus(player.userID));
        }

        [ChatCommand("rankkit")]
        private void RankKitChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            string result;
            if (!TryGiveRankKit(player, out result))
            {
                SendReply(player, result);
                return;
            }

            SendReply(player, result);
        }

        [ChatCommand("daily")]
        private void DailyChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            string result;
            if (!TryGiveDailyReward(player, out result))
            {
                SendReply(player, result);
                return;
            }

            SendReply(player, result);
        }

        [ChatCommand("pshop")]
        private void PrivilegeShopChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (config.Shop == null || !config.Shop.Enabled)
            {
                SendReply(player, "Магазин привилегий отключен.");
                return;
            }

            if (args != null && args.Length >= 2 && string.Equals(args[0], "buy", StringComparison.OrdinalIgnoreCase))
            {
                string result;
                if (!TryBuyShopPackage(player, args[1], out result))
                {
                    SendReply(player, result);
                    return;
                }

                SendReply(player, result);
                return;
            }

            var currency = NormalizeShopCurrency(config.Shop.Currency);
            SendReply(player, $"Валюта магазина: {currency}");
            SendReply(player, "Используй: /pshop buy <package>");

            var keys = new List<string>(config.Shop.Packages.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            {
                ShopPackageConfig package;
                if (!config.Shop.Packages.TryGetValue(key, out package) || package == null) continue;
                SendReply(player, $"{key} -> {package.DisplayName} | ранг={NormalizeRankKey(package.Rank)} | дней={(package.Days == 0 ? "навсегда" : package.Days.ToString())} | цена={GetShopPriceText(package, currency)}");
            }
        }

        [ChatCommand("remove")]
        private void RemoveCommandChat(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            var turnOff = args != null && args.Length > 0 &&
                          (string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(args[0], "0", StringComparison.OrdinalIgnoreCase));
            if (turnOff)
            {
                DisableRemoveMode(player.userID, "Режим ремува выключен.");
                return;
            }

            string accessError;
            if (!CanUseRemoveFeature(player, out accessError))
            {
                SendReply(player, accessError);
                return;
            }

            if (!HasHammerInHands(player))
            {
                SendReply(player, "Возьми киянку в руки, чтобы включить режим ремува.");
                return;
            }

            if (removeModePlayers.Contains(player.userID))
            {
                DisableRemoveMode(player.userID, "Режим ремува выключен.");
                return;
            }

            EnableRemoveMode(player);
            SendReply(player, $"Режим ремува включен на {GetRemoveModeDurationSeconds()}с. Смотри на свою постройку и нажми ЛКМ для удаления.");
        }

        [ChatCommand("recycler")]
        private void RecyclerCommandChat(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            var turnOff = args != null && args.Length > 0 &&
                          (string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(args[0], "0", StringComparison.OrdinalIgnoreCase));
            if (turnOff)
            {
                DestroyPersonalRecycler(player.userID, "Карманный переработчик закрыт.");
                return;
            }

            string result;
            if (!TryOpenPersonalRecycler(player, out result))
            {
                SendReply(player, result);
                return;
            }

            SendReply(player, result);
        }

        [ChatCommand("sethome")]
        private void SetHomeChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args == null || args.Length < 1)
            {
                SendReply(player, "Использование: /sethome <home_name>");
                return;
            }

            string result;
            if (!TrySetHomePoint(player, args[0], out result))
            {
                SendReply(player, result);
                return;
            }

            SendReply(player, result);
        }

        [ChatCommand("home")]
        private void HomeChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args == null || args.Length == 0)
            {
                SendReply(player, BuildHomesSummary(player));
                SendReply(player, "Использование: /home <home_name>");
                return;
            }

            string result;
            if (!TryUseHomeTeleport(player, args[0], out result))
            {
                SendReply(player, result);
                return;
            }

            SendReply(player, result);
        }

        [ChatCommand("homes")]
        private void HomesChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            SendReply(player, BuildHomesSummary(player));
        }

        [ChatCommand("removehome")]
        private void RemoveHomeChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args == null || args.Length < 1)
            {
                SendReply(player, "Использование: /removehome <home_name>");
                return;
            }

            string result;
            if (!TryRemoveHomePoint(player, args[0], out result))
            {
                SendReply(player, result);
                return;
            }

            SendReply(player, result);
        }

        [ChatCommand("delhome")]
        private void DelHomeChatCommand(BasePlayer player, string command, string[] args)
        {
            RemoveHomeChatCommand(player, command, args);
        }

        [ChatCommand("hometp")]
        private void HomeTeleportChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args == null || args.Length < 1)
            {
                SendReply(player, "Использование: /hometp <home_name>");
                return;
            }

            string result;
            if (!TryUseHomeTeleport(player, args[0], out result))
            {
                SendReply(player, result);
                return;
            }

            SendReply(player, result);
        }

        [ChatCommand("towntp")]
        private void TownTeleportChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            string result;
            if (!TryUseTownTeleport(player, out result))
            {
                SendReply(player, result);
                return;
            }

            SendReply(player, result);
        }

        [ChatCommand("teamtp")]
        private void TeamTeleportChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (args == null || args.Length < 1)
            {
                SendReply(player, "Использование: /teamtp <teammate>");
                return;
            }

            string result;
            if (!TryUseTeamTeleport(player, args[0], out result))
            {
                SendReply(player, result);
                return;
            }

            SendReply(player, result);
        }

        [ChatCommand("privui")]
        private void PrivUiChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!HasAdminAccess(player))
            {
                SendReply(player, "Нет доступа.");
                return;
            }

            OpenAdminUi(player);
        }

        [ChatCommand("priv")]
        private void PrivChatCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (args == null || args.Length == 0)
            {
                if (!HasAdminAccess(player))
                {
                    SendReply(player, BuildPlayerStatus(player.userID));
                    return;
                }

                SendUsage(player);
                return;
            }

            var sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "my":
                    SendReply(player, BuildPlayerStatus(player.userID));
                    return;
                case "ui":
                    if (!HasAdminAccess(player))
                    {
                        SendReply(player, "Нет доступа.");
                        return;
                    }
                    OpenAdminUi(player);
                    return;
                case "kit":
                case "rankkit":
                    string result;
                    if (!TryGiveRankKit(player, out result))
                    {
                        SendReply(player, result);
                        return;
                    }
                    SendReply(player, result);
                    return;
                case "daily":
                    string dailyResult;
                    if (!TryGiveDailyReward(player, out dailyResult))
                    {
                        SendReply(player, dailyResult);
                        return;
                    }
                    SendReply(player, dailyResult);
                    return;
                case "pshop":
                case "shop":
                    if (config.Shop == null || !config.Shop.Enabled)
                    {
                        SendReply(player, "Магазин привилегий отключен.");
                        return;
                    }

                    if (args.Length >= 3 && string.Equals(args[1], "buy", StringComparison.OrdinalIgnoreCase))
                    {
                        string buyResult;
                        if (!TryBuyShopPackage(player, args[2], out buyResult))
                        {
                            SendReply(player, buyResult);
                            return;
                        }
                        SendReply(player, buyResult);
                        return;
                    }

                    var currency = NormalizeShopCurrency(config.Shop.Currency);
                    SendReply(player, $"Валюта магазина: {currency}");
                    SendReply(player, "Используй: /priv shop buy <package> или /pshop buy <package>");
                    var packageKeys = new List<string>(config.Shop.Packages.Keys);
                    packageKeys.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (var packageKey in packageKeys)
                    {
                        ShopPackageConfig package;
                        if (!config.Shop.Packages.TryGetValue(packageKey, out package) || package == null) continue;
                        SendReply(player, $"{packageKey} -> {package.DisplayName} | ранг={NormalizeRankKey(package.Rank)} | дней={(package.Days == 0 ? "навсегда" : package.Days.ToString())} | цена={GetShopPriceText(package, currency)}");
                    }
                    return;
                case "activate":
                    ActivateWebShopOrdersForPlayer(player);
                    return;
                case "recycler":
                    var recyclerOff = args.Length >= 2 &&
                                      (string.Equals(args[1], "off", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(args[1], "0", StringComparison.OrdinalIgnoreCase));
                    if (recyclerOff)
                    {
                        DestroyPersonalRecycler(player.userID, "Карманный переработчик закрыт.");
                        return;
                    }
                    string recyclerResult;
                    if (!TryOpenPersonalRecycler(player, out recyclerResult))
                    {
                        SendReply(player, recyclerResult);
                        return;
                    }
                    SendReply(player, recyclerResult);
                    return;
                case "removemode":
                    var removeOff = args.Length >= 2 &&
                                    (string.Equals(args[1], "off", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(args[1], "0", StringComparison.OrdinalIgnoreCase));
                    if (removeOff)
                    {
                        DisableRemoveMode(player.userID, "Режим ремува выключен.");
                        return;
                    }

                    string removeAccessError;
                    if (!CanUseRemoveFeature(player, out removeAccessError))
                    {
                        SendReply(player, removeAccessError);
                        return;
                    }

                    if (!HasHammerInHands(player))
                    {
                        SendReply(player, "Возьми киянку в руки, чтобы включить режим ремува.");
                        return;
                    }
                    if (removeModePlayers.Contains(player.userID))
                    {
                        DisableRemoveMode(player.userID, "Режим ремува выключен.");
                        return;
                    }
                    EnableRemoveMode(player);
                    SendReply(player, $"Режим ремува включен на {GetRemoveModeDurationSeconds()}с. Нажми ЛКМ по своей постройке для удаления.");
                    return;
                case "sethome":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Использование: /priv sethome <home_name>");
                        return;
                    }
                    string setHomeResult;
                    if (!TrySetHomePoint(player, args[1], out setHomeResult))
                    {
                        SendReply(player, setHomeResult);
                        return;
                    }
                    SendReply(player, setHomeResult);
                    return;
                case "homes":
                    SendReply(player, BuildHomesSummary(player));
                    return;
                case "home":
                    if (args.Length < 2)
                    {
                        SendReply(player, BuildHomesSummary(player));
                        SendReply(player, "Использование: /priv home <home_name>");
                        return;
                    }
                    string homeResult;
                    if (!TryUseHomeTeleport(player, args[1], out homeResult))
                    {
                        SendReply(player, homeResult);
                        return;
                    }
                    SendReply(player, homeResult);
                    return;
                case "removehome":
                case "delhome":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Использование: /priv removehome <home_name>");
                        return;
                    }
                    string removeHomeResult;
                    if (!TryRemoveHomePoint(player, args[1], out removeHomeResult))
                    {
                        SendReply(player, removeHomeResult);
                        return;
                    }
                    SendReply(player, removeHomeResult);
                    return;
                case "hometp":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Использование: /priv hometp <home_name>");
                        return;
                    }
                    string homeTpResult;
                    if (!TryUseHomeTeleport(player, args[1], out homeTpResult))
                    {
                        SendReply(player, homeTpResult);
                        return;
                    }
                    SendReply(player, homeTpResult);
                    return;
                case "towntp":
                    string townTpResult;
                    if (!TryUseTownTeleport(player, out townTpResult))
                    {
                        SendReply(player, townTpResult);
                        return;
                    }
                    SendReply(player, townTpResult);
                    return;
                case "settown":
                    if (!HasAdminAccess(player))
                    {
                        SendReply(player, "Нет доступа.");
                        return;
                    }
                    storedData.TownTeleportPoint = new TeleportPoint(player.transform.position);
                    WriteAudit(
                        "tp.town.set",
                        player.userID,
                        player.displayName,
                        player.userID,
                        player.displayName,
                        $"point={FormatTeleportPoint(storedData.TownTeleportPoint)}",
                        false);
                    SaveData();
                    SendReply(player, $"Точка города установлена: {FormatTeleportPoint(storedData.TownTeleportPoint)}");
                    return;
                case "cleartown":
                    if (!HasAdminAccess(player))
                    {
                        SendReply(player, "Нет доступа.");
                        return;
                    }
                    storedData.TownTeleportPoint = null;
                    WriteAudit(
                        "tp.town.clear",
                        player.userID,
                        player.displayName,
                        player.userID,
                        player.displayName,
                        string.Empty,
                        false);
                    SaveData();
                    SendReply(player, "Точка города очищена.");
                    return;
                case "townpoint":
                    SendReply(player, "Точка города: " + FormatTeleportPoint(storedData.TownTeleportPoint));
                    return;
                case "teamtp":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Использование: /priv teamtp <teammate>");
                        return;
                    }
                    string teamTpResult;
                    if (!TryUseTeamTeleport(player, args[1], out teamTpResult))
                    {
                        SendReply(player, teamTpResult);
                        return;
                    }
                    SendReply(player, teamTpResult);
                    return;
                case "list":
                    if (!HasAdminAccess(player))
                    {
                        SendReply(player, "Нет доступа.");
                        return;
                    }

                    var list = new List<string>();
                    foreach (var pair in config.Ranks)
                    {
                        var rank = pair.Value;
                        list.Add($"{pair.Key} ({rank.DisplayName})");
                    }

                    SendReply(player, "Ранги: " + string.Join(", ", list.ToArray()));
                    SendReply(player, $"Активных записей привилегий: {storedData.Players.Count}");
                    return;
                case "audit":
                    if (!HasAdminAccess(player))
                    {
                        SendReply(player, "Нет доступа.");
                        return;
                    }

                    var count = 20;
                    if (args.Length >= 2)
                    {
                        int.TryParse(args[1], out count);
                    }

                    var lines = BuildAuditTailLines(count);
                    SendReply(player, $"Записи аудита (последние {Math.Min(Math.Max(count, 1), 50)}):");
                    foreach (var line in lines)
                    {
                        SendReply(player, line);
                    }
                    return;
                case "shopsync":
                    if (!HasAdminAccess(player))
                    {
                        SendReply(player, "Нет доступа.");
                        return;
                    }
                    PollWebShopOrders();
                    SendReply(player, "Запрошена синхронизация веб-шопа.");
                    return;
            }

            if (!HasAdminAccess(player))
            {
                SendReply(player, "Нет доступа.");
                return;
            }

            if (sub == "add" || sub == "set")
            {
                if (args.Length < 3)
                {
                    SendUsage(player);
                    return;
                }

                ulong targetId;
                string targetName;
                if (!TryResolvePlayerIdentity(args[1], out targetId, out targetName))
                {
                    SendReply(player, "Игрок не найден. Укажи точный ник/часть ника или SteamID64.");
                    return;
                }

                int days = 0;
                if (args.Length >= 4 && !int.TryParse(args[3], out days))
                {
                    SendReply(player, "Дни должны быть числом (0 = навсегда).");
                    return;
                }

                string error;
                if (!SetPrivilege(targetId, targetName, args[2], days, player.displayName, player.userID, out error))
                {
                    SendReply(player, "Ошибка: " + error);
                    return;
                }

                SendReply(player, $"Привилегия выдана: {targetName} ({targetId}) -> {NormalizeRankKey(args[2])}, срок: {(days == 0 ? "навсегда" : days + " дн.")}");
                var target = BasePlayer.FindByID(targetId);
                if (target != null)
                {
                    SendReply(target, $"Вам выдана привилегия: {BuildPlayerStatus(targetId)}");
                }
                return;
            }

            if (sub == "remove")
            {
                if (args.Length < 2)
                {
                    SendUsage(player);
                    return;
                }

                ulong targetId;
                string targetName;
                if (!TryResolvePlayerIdentity(args[1], out targetId, out targetName))
                {
                    SendReply(player, "Игрок не найден.");
                    return;
                }

                if (!RemoveRecord(targetId.ToString(), player.userID, player.displayName, "chat remove"))
                {
                    SendReply(player, "Нет записи привилегии для удаления.");
                    return;
                }

                SendReply(player, $"Привилегия снята: {targetName} ({targetId})");
                var target = BasePlayer.FindByID(targetId);
                if (target != null)
                {
                    SendReply(target, "Ваша привилегия была снята.");
                }
                return;
            }

            if (sub == "extend")
            {
                if (args.Length < 3)
                {
                    SendUsage(player);
                    return;
                }

                ulong targetId;
                string targetName;
                if (!TryResolvePlayerIdentity(args[1], out targetId, out targetName))
                {
                    SendReply(player, "Игрок не найден.");
                    return;
                }

                int days;
                if (!int.TryParse(args[2], out days))
                {
                    SendReply(player, "Дни должны быть числом.");
                    return;
                }

                string error;
                if (!ExtendPrivilege(targetId, days, player.userID, player.displayName, out error))
                {
                    SendReply(player, "Ошибка: " + error);
                    return;
                }

                SendReply(player, $"Привилегия продлена для {targetName} ({targetId}) на {days} дн.");
                return;
            }

            if (sub == "info")
            {
                if (args.Length < 2)
                {
                    SendUsage(player);
                    return;
                }

                ulong targetId;
                string targetName;
                if (!TryResolvePlayerIdentity(args[1], out targetId, out targetName))
                {
                    SendReply(player, "Игрок не найден.");
                    return;
                }

                SendReply(player, $"{targetName} ({targetId}) -> {BuildPlayerStatus(targetId)}");
                return;
            }

            SendUsage(player);
        }

        [ConsoleCommand("priv.add")]
        private void PrivAddConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !HasAdminAccess(arg.Player())) return;
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Использование: priv.add <steamid64> <rank> [days]");
                return;
            }

            ulong targetId;
            if (!ulong.TryParse(arg.Args[0], out targetId))
            {
                arg.ReplyWith("Invalid SteamID64.");
                return;
            }

            var rank = arg.Args[1];
            int days = 0;
            if (arg.Args.Length >= 3 && !int.TryParse(arg.Args[2], out days))
            {
                arg.ReplyWith("Days must be numeric.");
                return;
            }

            string error;
            if (!SetPrivilege(targetId, ResolvePlayerName(targetId), rank, days, "Console", 0, out error))
            {
                arg.ReplyWith("Failed: " + error);
                return;
            }

            arg.ReplyWith($"OK: {targetId} -> {NormalizeRankKey(rank)}, duration: {(days == 0 ? "permanent" : days + " days")}");
        }

        [ConsoleCommand("priv.remove")]
        private void PrivRemoveConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !HasAdminAccess(arg.Player())) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith("Использование: priv.remove <steamid64>");
                return;
            }

            ulong targetId;
            if (!ulong.TryParse(arg.Args[0], out targetId))
            {
                arg.ReplyWith("Invalid SteamID64.");
                return;
            }

            if (!RemoveRecord(targetId.ToString(), 0, "Console", "console remove"))
            {
                arg.ReplyWith("No privilege record.");
                return;
            }

            arg.ReplyWith("OK: removed.");
        }

        [ConsoleCommand("priv.extend")]
        private void PrivExtendConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !HasAdminAccess(arg.Player())) return;
            if (arg.Args == null || arg.Args.Length < 2)
            {
                arg.ReplyWith("Использование: priv.extend <steamid64> <days>");
                return;
            }

            ulong targetId;
            int days;
            if (!ulong.TryParse(arg.Args[0], out targetId) || !int.TryParse(arg.Args[1], out days))
            {
                arg.ReplyWith("Invalid arguments.");
                return;
            }

            string error;
            if (!ExtendPrivilege(targetId, days, 0, "Console", out error))
            {
                arg.ReplyWith("Failed: " + error);
                return;
            }

            arg.ReplyWith("OK: extended.");
        }

        [ConsoleCommand("priv.list")]
        private void PrivListConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !HasAdminAccess(arg.Player())) return;

            var lines = new List<string>();
            foreach (var pair in config.Ranks)
            {
                var rank = pair.Value;
                lines.Add($"{pair.Key} ({rank.DisplayName}) perms={rank.Permissions.Count}, group={rank.OxideGroup}");
            }

            arg.ReplyWith("Ranks: " + string.Join(" | ", lines.ToArray()));
            arg.ReplyWith($"Active records: {storedData.Players.Count}");
        }

        [ConsoleCommand("priv.info")]
        private void PrivInfoConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !HasAdminAccess(arg.Player())) return;
            if (arg.Args == null || arg.Args.Length < 1)
            {
                arg.ReplyWith("Использование: priv.info <steamid64>");
                return;
            }

            ulong targetId;
            if (!ulong.TryParse(arg.Args[0], out targetId))
            {
                arg.ReplyWith("Invalid SteamID64.");
                return;
            }

            arg.ReplyWith($"{ResolvePlayerName(targetId)} ({targetId}) -> {BuildPlayerStatus(targetId)}");
        }

        [ConsoleCommand("priv.audit")]
        private void PrivAuditConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !HasAdminAccess(arg.Player())) return;

            var count = 20;
            if (arg.Args != null && arg.Args.Length >= 1)
            {
                int.TryParse(arg.Args[0], out count);
            }

            var lines = BuildAuditTailLines(count);
            arg.ReplyWith($"Audit entries (last {Math.Min(Math.Max(count, 1), 50)}):");
            foreach (var line in lines)
            {
                arg.ReplyWith(line);
            }
        }

        [ConsoleCommand("priv.shopsync")]
        private void PrivShopSyncConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !HasAdminAccess(arg.Player())) return;
            PollWebShopOrders();
            arg.ReplyWith("Запрошена синхронизация веб-шопа.");
        }

        [ConsoleCommand("priv.ui")]
        private void PrivUiConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdminAccess(player)) return;
            OpenAdminUi(player);
        }

        [ConsoleCommand("priv.ui.close")]
        private void PrivUiCloseConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            DestroyAdminUi(player);
            uiViewers.Remove(player.userID);
            uiSelectedTarget.Remove(player.userID);
            uiSelectedRank.Remove(player.userID);
            uiActiveTab.Remove(player.userID);
        }

        [ConsoleCommand("priv.ui.refresh")]
        private void PrivUiRefreshConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdminAccess(player)) return;
            OpenAdminUi(player);
        }

        [ConsoleCommand("priv.ui.tab")]
        private void PrivUiTabConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdminAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 1) return;

            SetUiActiveTab(player, arg.Args[0]);
            OpenAdminUi(player);
        }

        [ConsoleCommand("priv.ui.rank")]
        private void PrivUiRankConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdminAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 1) return;

            var rankKey = NormalizeRankKey(arg.Args[0]);
            if (FindRank(rankKey) == null)
            {
                SendReply(player, $"Ранг не найден: {rankKey}");
                return;
            }

            uiSelectedRank[player.userID] = rankKey;
            SetUiActiveTab(player, "settings");
            OpenAdminUi(player);
        }

        [ConsoleCommand("priv.ui.cfg")]
        private void PrivUiCfgConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdminAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 3) return;

            var rankKey = NormalizeRankKey(arg.Args[0]);
            var field = arg.Args[1];
            float delta;
            if (!float.TryParse(arg.Args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out delta) &&
                !float.TryParse(arg.Args[2], out delta))
            {
                return;
            }

            uiSelectedRank[player.userID] = rankKey;
            SetUiActiveTab(player, "settings");
            UiAdjustRankSetting(player, rankKey, field, delta);
            RefreshAdminUiForViewers();
        }

        [ConsoleCommand("priv.ui.select")]
        private void PrivUiSelectConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdminAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 1) return;

            ulong targetId;
            if (!ulong.TryParse(arg.Args[0], out targetId)) return;

            uiSelectedTarget[player.userID] = targetId;
            OpenAdminUi(player);
        }

        [ConsoleCommand("priv.ui.grant")]
        private void PrivUiGrantConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdminAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 3) return;

            ulong targetId;
            int days;
            if (!ulong.TryParse(arg.Args[0], out targetId)) return;
            if (!int.TryParse(arg.Args[2], out days)) return;

            var rank = arg.Args[1];
            uiSelectedTarget[player.userID] = targetId;
            UiGrant(player, targetId, rank, days);
            RefreshAdminUiForViewers();
        }

        [ConsoleCommand("priv.ui.extend")]
        private void PrivUiExtendConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdminAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 2) return;

            ulong targetId;
            int days;
            if (!ulong.TryParse(arg.Args[0], out targetId)) return;
            if (!int.TryParse(arg.Args[1], out days)) return;

            uiSelectedTarget[player.userID] = targetId;
            UiExtend(player, targetId, days);
            RefreshAdminUiForViewers();
        }

        [ConsoleCommand("priv.ui.remove")]
        private void PrivUiRemoveConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !HasAdminAccess(player)) return;
            if (arg.Args == null || arg.Args.Length < 1) return;

            ulong targetId;
            if (!ulong.TryParse(arg.Args[0], out targetId)) return;

            uiSelectedTarget[player.userID] = targetId;
            UiRemove(player, targetId);
            RefreshAdminUiForViewers();
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin == null || plugin.Name == Name)
            {
                return;
            }

            delayedReapplyTimer?.Destroy();
            delayedReapplyTimer = timer.Once(1f, () =>
            {
                delayedReapplyTimer = null;
                ReapplyPrivileges();
            });
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            CancelPendingHomeTeleport(player.userID);
            DisableRemoveMode(player.userID);
            DestroyPersonalRecycler(player.userID);
            personalRecyclerCloseGuardUntilUnix.Remove(player.userID);
            personalRecyclerWorkSoundNextTime.Remove(player.userID);
            uiViewers.Remove(player.userID);
            uiSelectedTarget.Remove(player.userID);
            uiSelectedRank.Remove(player.userID);
            uiActiveTab.Remove(player.userID);
        }

        private void TryAutoClosePocketRecycler(BasePlayer player)
        {
            if (player == null || !IsPocketRecyclerAutoCloseOnMenuCloseEnabled()) return;

            long guardUntil;
            var now = UtcNowUnix();
            if (personalRecyclerCloseGuardUntilUnix.TryGetValue(player.userID, out guardUntil) && guardUntil > now)
            {
                return;
            }

            Recycler recycler;
            if (!personalRecyclerByPlayer.TryGetValue(player.userID, out recycler) || recycler == null || recycler.IsDestroyed || recycler.net == null)
            {
                return;
            }

            if (player.inventory != null && player.inventory.loot != null && player.inventory.loot.entitySource == recycler)
            {
                return;
            }

            DestroyPersonalRecycler(player.userID, "Карманный переработчик закрыт.");
        }

        private void OnPlayerLootEnd(PlayerLoot loot)
        {
            var player = loot?.baseEntity as BasePlayer;
            TryAutoClosePocketRecycler(player);
        }

        private void OnLootEntityEnd(BasePlayer player, BaseEntity entity)
        {
            TryAutoClosePocketRecycler(player);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;
            if (!removeModePlayers.Contains(player.userID)) return;
            if (!input.WasJustPressed(BUTTON.FIRE_PRIMARY)) return;

            if (!HasHammerInHands(player))
            {
                DisableRemoveMode(player.userID, "Режим ремува выключен: киянка не в руках.");
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, RemoveMaxDistanceMeters))
            {
                return;
            }

            BaseEntity target = null;
            if (hit.collider != null)
            {
                target = hit.collider.GetComponentInParent<BaseEntity>();
            }
            if (target == null && hit.transform != null)
            {
                target = hit.transform.GetComponentInParent<BaseEntity>();
            }

            if (target == null)
            {
                SendReply(player, "Цель для удаления не найдена.");
                return;
            }

            string removeError;
            if (!CanRemoveTargetEntity(player, target, out removeError))
            {
                SendReply(player, removeError);
                return;
            }

            var targetName = string.IsNullOrWhiteSpace(target.ShortPrefabName) ? target.GetType().Name : target.ShortPrefabName;
            target.Kill(BaseNetworkable.DestroyMode.Gib);
            SendReply(player, $"Удалено: {targetName}");
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null) return null;
            var recycler = container as Recycler;
            if (recycler == null || recycler.net == null) return null;

            ulong ownerId;
            if (!personalRecyclerOwnerByEntityId.TryGetValue(recycler.net.ID.Value, out ownerId))
            {
                return null;
            }

            if (player.userID != ownerId)
            {
                return false;
            }

            StartPersonalRecyclerLifetime(recycler);
            return null;
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || item == null || item.amount <= 0) return;

            PrivilegeRecord record;
            RankDefinition rank;
            if (!TryGetActivePrivilege(player.userID, out record, out rank) || rank == null) return;
            if (rank.GatherMultiplier <= 1.01f) return;

            item.amount = Mathf.CeilToInt(item.amount * rank.GatherMultiplier);
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || item == null || item.amount <= 0) return;

            PrivilegeRecord record;
            RankDefinition rank;
            if (!TryGetActivePrivilege(player.userID, out record, out rank) || rank == null) return;
            if (rank.GatherMultiplier <= 1.01f) return;

            item.amount = Mathf.CeilToInt(item.amount * rank.GatherMultiplier);
        }

        private void ApplyGroundPickupBonus(BasePlayer player, Item item)
        {
            if (player == null || item == null || item.amount <= 0) return;

            PrivilegeRecord record;
            RankDefinition rank;
            if (!TryGetActivePrivilege(player.userID, out record, out rank) || rank == null) return;
            if (rank.GroundPickupMultiplier <= 1.01f) return;

            item.amount = Mathf.CeilToInt(item.amount * rank.GroundPickupMultiplier);
        }

        private void OnCollectiblePickedup(CollectibleEntity entity, BasePlayer player, Item item)
        {
            ApplyGroundPickupBonus(player, item);
        }

        private static bool IsBarrelPrefab(string shortPrefabName)
        {
            if (string.IsNullOrWhiteSpace(shortPrefabName)) return false;
            return shortPrefabName.IndexOf("barrel", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ContainerLootManager can return false via this hook for per-container blocking.
        private bool IsContainerLootBonusBlocked(BasePlayer player, LootContainer lootContainer)
        {
            if (lootContainer == null) return false;

            var hookResult = Interface.CallHook("CanPrivilegeContainerLootBonus", player, lootContainer);
            if (hookResult is bool boolResult)
            {
                return !boolResult;
            }

            return false;
        }

        private bool TryApplyBarrelLootBonus(LootContainer lootContainer, ulong attackerId, BasePlayer attackerPlayer = null)
        {
            if (lootContainer == null || lootContainer.inventory == null || lootContainer.net == null) return false;
            if (!IsBarrelPrefab(lootContainer.ShortPrefabName)) return false;
            if (attackerId == 0) return false;

            var containerId = lootContainer.net.ID.Value;
            if (barrelLootAdjusted.Contains(containerId)) return false;

            PrivilegeRecord record;
            RankDefinition rank;
            if (!TryGetActivePrivilege(attackerId, out record, out rank) || rank == null) return false;
            if (rank.ContainerLootMultiplier <= 1.01f) return false;
            if (IsContainerLootBonusBlocked(attackerPlayer, lootContainer)) return false;

            var changedStacks = 0;
            var snapshot = lootContainer.inventory.itemList.ToArray();
            foreach (var item in snapshot)
            {
                if (item == null || item.info == null || item.amount <= 0) continue;
                item.amount = Mathf.CeilToInt(item.amount * rank.ContainerLootMultiplier);
                changedStacks++;
            }

            if (changedStacks <= 0) return false;

            barrelLootAdjusted.Add(containerId);

            var receiver = attackerPlayer ?? BasePlayer.FindByID(attackerId) ?? BasePlayer.FindSleeping(attackerId);
            if (receiver != null && receiver.IsConnected)
            {
                SendReply(receiver, $"Бонус ранга: лут бочек x{rank.ContainerLootMultiplier:0.##}.");
            }

            return true;
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;

            var damagedPlayer = entity as BasePlayer;
            if (damagedPlayer != null && info.damageTypes != null && info.damageTypes.Total() > 0f)
            {
                CancelPendingHomeTeleport(damagedPlayer.userID, "Home TP отменен: вы получили урон.");
            }

            var lootContainer = entity as LootContainer;
            if (lootContainer == null || lootContainer.net == null) return;
            if (!IsBarrelPrefab(lootContainer.ShortPrefabName)) return;

            var attacker = info.InitiatorPlayer;
            if (attacker == null || !attacker.IsConnected) return;

            barrelLastAttacker[lootContainer.net.ID.Value] = attacker.userID;
        }

        private void OnContainerDropItems(ItemContainer container)
        {
            if (container == null || container.entityOwner == null) return;

            var lootContainer = container.entityOwner as LootContainer;
            if (lootContainer == null || lootContainer.net == null) return;
            if (!IsBarrelPrefab(lootContainer.ShortPrefabName)) return;

            var containerId = lootContainer.net.ID.Value;
            if (barrelLootAdjusted.Contains(containerId)) return;

            ulong attackerId;
            if (!barrelLastAttacker.TryGetValue(containerId, out attackerId)) return;

            TryApplyBarrelLootBonus(lootContainer, attackerId, BasePlayer.FindByID(attackerId));
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return;

            var lootContainer = entity as LootContainer;
            if (lootContainer == null || lootContainer.inventory == null) return;
            if (lootContainer.net == null) return;

            var prefab = (lootContainer.ShortPrefabName ?? string.Empty).ToLowerInvariant();
            if (prefab.Contains("corpse")) return;
            if (!prefab.Contains("barrel") && !prefab.Contains("crate") && !prefab.Contains("loot")) return;

            PrivilegeRecord record;
            RankDefinition rank;
            if (!TryGetActivePrivilege(player.userID, out record, out rank) || rank == null) return;
            if (rank.ContainerLootMultiplier <= 1.01f) return;
            if (IsContainerLootBonusBlocked(player, lootContainer)) return;

            var containerId = lootContainer.net.ID.Value;
            HashSet<ulong> claimedUsers;
            if (!containerLootBonusClaimed.TryGetValue(containerId, out claimedUsers))
            {
                claimedUsers = new HashSet<ulong>();
                containerLootBonusClaimed[containerId] = claimedUsers;
            }

            if (claimedUsers.Contains(player.userID)) return;

            // Some containers populate loot on open; defer one tick so inventory is filled.
            timer.Once(0.05f, () =>
            {
                if (player == null || !player.IsConnected) return;
                if (lootContainer == null || lootContainer.IsDestroyed || lootContainer.inventory == null || lootContainer.net == null) return;
                if (IsContainerLootBonusBlocked(player, lootContainer)) return;

                HashSet<ulong> currentClaimedUsers;
                if (!containerLootBonusClaimed.TryGetValue(containerId, out currentClaimedUsers))
                {
                    currentClaimedUsers = new HashSet<ulong>();
                    containerLootBonusClaimed[containerId] = currentClaimedUsers;
                }

                if (currentClaimedUsers.Contains(player.userID)) return;

                var snapshot = lootContainer.inventory.itemList.ToArray();
                if (snapshot.Length == 0) return;

                var extraStacks = 0;
                foreach (var sourceItem in snapshot)
                {
                    if (sourceItem == null || sourceItem.info == null || sourceItem.amount <= 0) continue;

                    var extraAmount = Mathf.CeilToInt(sourceItem.amount * (rank.ContainerLootMultiplier - 1f));
                    if (extraAmount <= 0) continue;

                    var created = ItemManager.CreateByItemID(sourceItem.info.itemid, extraAmount, sourceItem.skin);
                    if (created == null) continue;

                    if (!created.MoveToContainer(lootContainer.inventory) &&
                        !created.MoveToContainer(player.inventory.containerMain) &&
                        !created.MoveToContainer(player.inventory.containerBelt) &&
                        !created.MoveToContainer(player.inventory.containerWear))
                    {
                        created.Drop(player.transform.position + Vector3.up, Vector3.zero);
                    }

                    extraStacks++;
                }

                if (extraStacks <= 0) return;
                currentClaimedUsers.Add(player.userID);
                SendReply(player, $"Бонус ранга: лут контейнеров x{rank.ContainerLootMultiplier:0.##} (доп. стаков: {extraStacks}).");
            });
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null || entity.net == null) return;
            var entityId = entity.net.ID.Value;
            containerLootBonusClaimed.Remove(entityId);
            barrelLastAttacker.Remove(entityId);
            barrelLootAdjusted.Remove(entityId);

            ulong ownerId;
            if (personalRecyclerOwnerByEntityId.TryGetValue(entityId, out ownerId))
            {
                personalRecyclerOwnerByEntityId.Remove(entityId);
                personalRecyclerByPlayer.Remove(ownerId);
                personalRecyclerCloseGuardUntilUnix.Remove(ownerId);
                personalRecyclerWorkSoundNextTime.Remove(ownerId);
                StopPersonalRecyclerCloseWatchTimer(ownerId);
            }

            Timer lifetimeTimer;
            if (personalRecyclerLifetimeTimers.TryGetValue(entityId, out lifetimeTimer))
            {
                lifetimeTimer?.Destroy();
                personalRecyclerLifetimeTimers.Remove(entityId);
            }

            StopPersonalRecyclerSpeedTimer(entityId);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            var lootContainer = entity as LootContainer;
            if (lootContainer != null && lootContainer.net != null && IsBarrelPrefab(lootContainer.ShortPrefabName))
            {
                var attackerId = 0UL;
                BasePlayer attackerPlayer = null;

                if (info != null)
                {
                    attackerPlayer = info.InitiatorPlayer;
                    if (attackerPlayer != null)
                    {
                        attackerId = attackerPlayer.userID;
                    }
                }

                if (attackerId == 0)
                {
                    barrelLastAttacker.TryGetValue(lootContainer.net.ID.Value, out attackerId);
                }

                TryApplyBarrelLootBonus(lootContainer, attackerId, attackerPlayer);
            }

            var victimPlayer = entity as BasePlayer;
            if (victimPlayer != null)
            {
                CancelPendingHomeTeleport(victimPlayer.userID);
            }

            if (info == null) return;

            var attacker = info.InitiatorPlayer;
            if (attacker == null || !attacker.IsConnected) return;
            var victimBaseNpc = entity as BaseNpc;
            var isNpcVictim = (victimPlayer != null && victimPlayer.IsNpc) || victimBaseNpc != null;

            if (!isNpcVictim) return;

            PrivilegeRecord record;
            RankDefinition rank;
            if (!TryGetActivePrivilege(attacker.userID, out record, out rank) || rank == null) return;
            if (rank.NpcKillScrapReward <= 0) return;

            var rewardItem = ItemManager.CreateByName("scrap", rank.NpcKillScrapReward);
            if (rewardItem == null) return;

            if (!rewardItem.MoveToContainer(attacker.inventory.containerMain) &&
                !rewardItem.MoveToContainer(attacker.inventory.containerBelt))
            {
                rewardItem.Drop(attacker.transform.position + Vector3.up, Vector3.zero);
            }

            SendReply(attacker, $"Rank bonus: +{rank.NpcKillScrapReward} scrap for NPC kill.");
        }

        private object OnPlayerChat(BasePlayer player, string message, object channel)
        {
            if (player == null || string.IsNullOrWhiteSpace(message))
            {
                return null;
            }

            // Keep team/local channels untouched.
            if (channel != null)
            {
                var channelName = channel.ToString();
                if (!string.IsNullOrWhiteSpace(channelName) && !string.Equals(channelName, "Global", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            PrivilegeRecord record;
            RankDefinition rank;
            if (!TryGetActivePrivilege(player.userID, out record, out rank) || rank == null)
            {
                return null;
            }

            var chatTag = BuildRankChatTag(rank);
            var chatColor = NormalizeChatColor(rank.ChatColor);
            var safeTag = EscapeRichText(chatTag);
            var safeName = EscapeRichText(player.displayName);
            var safeMessage = EscapeRichText(message);

            PrintToChat($"<color={chatColor}>{safeTag}</color> {safeName}: {safeMessage}");
            return false;
        }

        private static string BuildRankChatTag(RankDefinition rank)
        {
            if (rank == null) return "[RANK]";

            var tag = string.IsNullOrWhiteSpace(rank.ChatTag)
                ? $"[{(string.IsNullOrWhiteSpace(rank.DisplayName) ? "RANK" : rank.DisplayName.Trim())}]"
                : rank.ChatTag.Trim();
            return tag;
        }

        private static string NormalizeChatColor(string rawColor)
        {
            var value = string.IsNullOrWhiteSpace(rawColor) ? "#ffffff" : rawColor.Trim();
            if (value.Length != 7 || value[0] != '#')
            {
                return "#ffffff";
            }

            for (var i = 1; i < value.Length; i++)
            {
                if (!Uri.IsHexDigit(value[i]))
                {
                    return "#ffffff";
                }
            }

            return value;
        }

        private static string EscapeRichText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("<", "‹").Replace(">", "›");
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            var userId = player.UserIDString;
            PrivilegeRecord record;
            RankDefinition rank;
            if (!TryGetActivePrivilege(player.userID, out record, out rank) || record == null || rank == null)
            {
                return;
            }

            record.LastKnownName = player.displayName;
            SaveData();

            string error;
            if (!ApplyRecord(userId, record, out error))
            {
                PrintWarning($"Failed to apply privilege for {userId}: {error}");
                return;
            }

            if (config.NotifyOnConnect)
            {
                SendReply(player, "Активная привилегия: " + BuildPlayerStatus(player.userID));
                if (rank.RankKitItems != null && rank.RankKitItems.Count > 0)
                {
                    long nextClaimUnix;
                    if (storedData.RankKitNextClaimUnix.TryGetValue(userId, out nextClaimUnix) && nextClaimUnix > UtcNowUnix())
                    {
                        SendReply(player, $"КД rank kit: {FormatRemaining(nextClaimUnix)}");
                    }
                    else
                    {
                        SendReply(player, "Rank kit готов. Используйте /rankkit");
                    }
                }

                if (config.DailyRewards != null && config.DailyRewards.Enabled)
                {
                    long dailyNext;
                    if (storedData.DailyNextClaimUnix.TryGetValue(userId, out dailyNext) && dailyNext > UtcNowUnix())
                    {
                        SendReply(player, $"КД daily: {FormatRemaining(dailyNext)}");
                    }
                    else
                    {
                        SendReply(player, "Ежедневная награда готова. Используйте /daily");
                    }
                }
            }
        }

        private string API_GetPlayerRank(ulong userId)
        {
            PrivilegeRecord record;
            if (!storedData.Players.TryGetValue(userId.ToString(), out record) || record == null)
            {
                return string.Empty;
            }

            return record.Rank ?? string.Empty;
        }
    }
}

