# Rust test server (small map) for mods

## What was prepared

- `C:\rust\steamcmd` - SteamCMD
- `C:\rust\server` - RustDedicated server files
- `C:\rust\mods\oxide-rust.zip` - downloaded Oxide archive
- `C:\rust\scripts\setup-rust-test-server.ps1` - full setup script
- `C:\rust\scripts\update-rust-server.ps1` - server update script
- `C:\rust\scripts\install-oxide.ps1` - Oxide install/update script
- `C:\rust\scripts\start-test-server.ps1` - start script (small map)
- `C:\rust\scripts\stop-test-server.ps1` - stop running server

## First start

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\start-test-server.ps1
```

## Start with custom settings example

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\start-test-server.ps1 `
  -HostName "My Local Mod Test" `
  -RconPassword "MyStrongRconPass_2026" `
  -WorldSize 1000 `
  -Seed 12345 `
  -MaxPlayers 5 `
  -Insecure
```

## Update server after Rust patch

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\update-rust-server.ps1
```

## Reinstall/update Oxide

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\install-oxide.ps1
```

## Stop server

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\stop-test-server.ps1
```

## One-command full setup (if needed)

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\setup-rust-test-server.ps1
```

## Notes

- Small test map is set with `-WorldSize 1000`.
- Server data is saved to `C:\rust\server\server\modtest`.
- Default ports: game `28015`, RCON `28016`.
- If you test only locally and have EAC problems, run with `-Insecure`.

## Container loot plugin

- Source: `C:\rust\mods\container-loot-manager\ContainerLootManager.cs`
- Config: `C:\rust\server\oxide\config\ContainerLootManager.json`
- In-game admin command: `/lootcfg help`
- Visual editor: `/lootui`
- Export loot catalog: `/lootcfg exportcatalog` -> `C:\rust\server\oxide\data\ContainerLootCatalog.json`

Deploy:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\container-loot-manager\scripts\deploy.ps1
```

## Separate loot configurator utility

- Folder: `C:\rust\mods\loot-configurator`
- Start: `powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\open-configurator.ps1`
- Utility output: `ContainerLootManager.json` (скачать из UI)
- В утилите уже встроены все контейнеры и весь лут (экспорт с сервера).
- Контейнеры при старте утилиты уже заполнены текущим спавнящимся лутом (Observed rules из каталога).
- Названия предметов отображаются как `RU / EN`.
- Обновить встроенный каталог:
  - `/lootcfg exportcatalog` на сервере
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\sync-catalog-from-server.ps1`
- Обновить словарь русских названий после апдейта каталога:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\generate-ru-item-names.ps1`

## Privilege plugin

- Source: `C:\rust\mods\privilege-system\PrivilegeSystem.cs`
- Config: `C:\rust\server\oxide\config\PrivilegeSystem.json`
- In-game admin UI: `/privui`
- Player status: `/vip` or `/priv my`
- Rank kit claim: `/rankkit`
- Daily reward: `/daily`
- Built-in utilities by rank: `/remove [off]` (enable only with hammer in hands), `/recycler [off]` (personal recycler UI-only, no model in front, faster than default recycler, command cooldown configurable)
- Built-in teleports: `/sethome <name>`, `/home <name>`, `/homes`, `/removehome <name>`, `/hometp <home>`, `/towntp`, `/teamtp <teammate>`, `/priv settown` (home can be set only on your own foundation/floor with your sleeping bag/bed on that block)
- Audit view (admin): `/priv audit [count]`

Deploy:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\privilege-system\scripts\deploy.ps1
```

## Separate privilege configurator utility

- Folder: `C:\rust\mods\privilege-configurator`
- Start: `powershell -ExecutionPolicy Bypass -File C:\rust\mods\privilege-configurator\open-configurator.ps1`
- Utility output: `PrivilegeSystem.json` (скачать из UI)
- Настраиваются ранги, permissions, множители, rank kit, daily/tp/audit.
- Для подсказок предметов используется каталог из `loot-configurator`.

## Unified Mod Suite (recommended)

- One launcher for both configurators + one-click plugin deploy:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\open-mod-suite.ps1`
- Hub URL:
  - `http://127.0.0.1:18765/mod-suite/index.html`
- One-command deploy (both plugins):
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite.ps1`
- Full installation/operations guide:
  - `C:\rust\mods\MOD-SUITE-GUIDE.md`

## Auto install + production deploy (by Shmatko)

- Full local auto-install (server + oxide + plugins + suite):
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\install-mod-suite.ps1`
- Local verification:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\verify-local-mod-suite.ps1`
- Production upload via SSH/SCP:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 -RemoteHost <host> -RemoteUser <user> -RemoteRoot <path>`

