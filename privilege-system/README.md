# PrivilegeSystem plugin (by Shmatko)

Timed privilege/rank system for Rust Oxide servers.

## Features

- Rank templates in config (`vip`, `premium`, `elite` by default)
- Grant privilege permanently or for N days
- Automatic expiry cleanup
- Auto-grant/reapply rank permissions and oxide group
- Built-in rank bonuses:
  - node gather multiplier by rank
  - ground pickup multiplier by rank
  - container loot multiplier by rank
  - scrap reward for NPC kills
  - rank kit with cooldown and amount multiplier (`/rankkit`)
- Daily reward system by rank multiplier (`/daily`)
- Privilege shop (`/pshop`) with `Economics` or `ServerRewards`
- Web shop bridge (external site/API -> auto grant on server poll)
- Built-in remove mode (`/remove`):
  - available by rank option `Allow remove command`
  - can be enabled only while holding a hammer
  - mode works for configured duration (`Teleport features -> Remove mode duration seconds`, default `30s`), remove by left-click
  - while active, top-left indicator shows remove status and countdown timer
  - `/remove off` (or `/priv removemode off`) disables immediately
  - non-admins can remove only their own entities
- Built-in pocket recycler (`/recycler`):
  - available by rank option `Allow pocket recycler`
  - `/recycler` opens a personal built-in recycler with native recycler UI
  - recycler model is not placed in front of the player (UI-only behavior)
  - command cooldown configured by `Teleport features -> Pocket recycler command cooldown seconds` (default `10s`)
  - recycles by real recycler recipes (same as monument recycler), but faster via `Teleport features -> Pocket recycler speed multiplier`
  - `/recycler off` (or `/priv recycler off`) closes immediately
- Built-in teleport module with rank modifiers:
  - `/sethome <name>`, `/home <name>`, `/homes`, `/removehome <name>`
  - home set allowed only on your own `foundation/floor` with your sleeping bag/bed on that same block
  - default homes for player without active rank: `1` (`Home points without privilege`)
  - home activation delay: base `15s`, reduced by rank home teleport reduction
  - `/hometp <home>` (base cooldown 30s, reduced by rank)
  - `/towntp` (base 10 uses/day, increased by rank, town point set by admin)
  - `/teamtp <teammate>` (base cooldown 15s, reduced by rank)
  - visual effect + sound on teleport
- Audit log for privilege actions and purchases
- Visual admin panel:
  - manage ranks (`/privui`)
  - settings tab to tune perks per rank
- Admin chat commands and console commands
- Player status command (`/vip`, `/priv my`)

## Files

- `PrivilegeSystem.cs` - plugin source
- `scripts/deploy.ps1` - copy plugin to server plugin folder

## Deploy

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\privilege-system\scripts\deploy.ps1
```

## Offline configurator

- Folder: `C:\rust\mods\privilege-configurator`
- Start:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\privilege-configurator\open-configurator.ps1
```

- Output file: `PrivilegeSystem.json`

## Chat commands

- `/vip` - show your current privilege
- `/priv my` - show your current privilege
- `/rankkit` - claim your rank kit (if available)
- `/daily` - claim daily reward
- `/pshop` - list shop packages
- `/pshop buy <package>` - buy package
- `/priv activate` - claim and activate your paid web-shop orders by your SteamID64
- `/remove [off]` - toggle remove mode (if allowed by rank)
- `/recycler [off]` - open/close personal recycler (if allowed by rank)
- `/sethome <name>` - save/update your home point
- `/home <name>` - teleport to your home point
- `/homes` - list your home points
- `/removehome <name>` - delete home point
- `/hometp <home>` - teleport to your home point (alias wrapper)
- `/towntp` - teleport to configured town point
- `/teamtp <teammate>` - teleport to your online teammate
- `/priv kit` - same as `/rankkit`
- `/privui` - open visual admin panel (admins)
- `/priv ui` - same as `/privui`
- `/priv list` - list ranks (admin only)
- `/priv add <player/id> <rank> [days]` - give rank (admin only)
- `/priv remove <player/id>` - remove rank (admin only)
- `/priv extend <player/id> <days>` - extend timed rank (admin only)
- `/priv info <player/id>` - show player privilege (admin only)
- `/priv audit [count]` - last audit records (admin only)
- `/priv shopsync` - force one web-shop sync poll (admin only)
- `/priv settown` - set town teleport point at your current position (admin only)
- `/priv cleartown` - clear town teleport point (admin only)
- `/priv townpoint` - show current town teleport point

## In-game usage

Player:

- Buy in web shop, then use `/priv activate` to claim paid order by your SteamID64.
- Check active status with `/vip` or `/priv my`.
- Claim rank kit with `/rankkit` (or `/priv kit`).
- Claim daily reward with `/daily`.
- Remove mode (if rank allows): `/remove`, then left-click own entity; `/remove off` to disable.
- Remove mode can be enabled only with hammer in hands.
- Pocket recycler (if rank allows):
  - `/recycler`
  - put items into recycler UI and press Start
  - `/recycler off` to force close
- Use teleports:
  - `/sethome base`
  - `/home base` (or `/hometp base`)
  - `/homes`
  - `/removehome base`
  - `/hometp <home>`
  - `/towntp`
  - `/teamtp <teammate>`
- View shop list in-game with `/pshop`, buy with `/pshop buy <package>`.

Admin:

- Open panel: `/privui` (or `/priv ui`).
- Manual rank grant: `/priv add <player/id> <rank> [days]`.
- Extend/remove rank:
  - `/priv extend <player/id> <days>`
  - `/priv remove <player/id>`
- Inspect player: `/priv info <player/id>`.
- View audit: `/priv audit [count]`.
- Force web sync: `/priv shopsync`.
- Set town point: `/priv settown`.

Chat tag/color:

- For players with active rank, global chat messages are formatted with rank `Chat tag` and `Chat color` from config.

## Console commands

- `priv.add <steamid64> <rank> [days]`
- `priv.remove <steamid64>`
- `priv.extend <steamid64> <days>`
- `priv.list`
- `priv.info <steamid64>`
- `priv.audit [count]`
- `priv.shopsync`
- `priv.ui` (opens panel for admin player)

## Permission

- `privilegesystem.admin`
- `privilegesystem.rank.vip`
- `privilegesystem.rank.premium`
- `privilegesystem.rank.elite`

Owner/admin users can use admin commands without extra grant.

## Notes

Default rank permissions are internal (`privilegesystem.rank.*`).
If you use plugins like Kits/Teleport/Backpacks, replace rank permission lists in:

- `C:\rust\server\oxide\config\PrivilegeSystem.json`

Main rank bonuses are also configured in this same file:

- `Gather multiplier`
- `Ground pickup multiplier`
- `Container loot multiplier`
- `NPC kill scrap reward`
- `Rank kit cooldown seconds`
- `Rank kit amount multiplier`
- `Rank kit items`
- `Daily reward multiplier`
- `Home teleport cooldown reduction (seconds)`
- `Team teleport cooldown reduction (seconds)`
- `Town teleport daily limit bonus`

For shop payments install one of:

- `Economics` (currency `economics`)
- `ServerRewards` (currency `serverrewards`)

Teleport commands are built into `PrivilegeSystem` and do not require `NTeleportation`.
Set town destination once with `/priv settown`.

## External web shop bridge

`Web shop bridge` allows auto-grant from an external website/API.

Expected endpoints in external API:

- `POST /api/v1/server/orders/claim` (`X-Server-Key` header)
- `POST /api/v1/server/orders/complete` (`X-Server-Key` header)

Plugin polls by interval and applies received orders as rank grants.
Manual trigger:

- player chat: `/priv activate` (claims only that player's SteamID64 orders)
- chat: `/priv shopsync` (admin)
- console: `priv.shopsync`

Global feature sections:

- `Daily rewards`
- `Teleport features`
- `Shop`
- `Audit`
- `Web shop bridge`

Admin panel controls:

- Select online target from the list
- Grant rank quickly (`VIP/PREMIUM/ELITE`, 7d/30d/permanent)
- Extend active rank (`+7d`, `+30d`)
- Remove rank
- Settings tab:
  - `Node Gather`
  - `Ground Gather`
  - `Container Loot`
  - `Kit Amount`
  - `Kit Cooldown (hours)`
