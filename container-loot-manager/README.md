# ContainerLootManager plugin (by Shmatko)

Per-container loot control for Rust Oxide/uMod.

## What it does

- Lets you configure loot for each container type (by container key).
- For every item in the rule, you set:
  - shortname
  - amount range
  - spawn chance
  - weight
- Works for normal containers and barrels (`LootContainer` based entities).
- Can override vanilla loot or keep vanilla (per rule).

## Files

- `ContainerLootManager.cs` - plugin source
- `scripts/deploy.ps1` - deploy script to server plugins folder

## Deploy

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\container-loot-manager\scripts\deploy.ps1
```

## Main command

- `/lootcfg help`
- `/lootui` - open visual CUI editor
- `/lootcfg exportcatalog` - export full catalog + observed loot rules to `oxide/data/ContainerLootCatalog.json`

In CUI you can:
- switch current rule
- bind rule to looked container key
- toggle enabled/override/duplicates/force-one
- edit min/max rolls and max stacks
- select loot item and edit min/max amount, chance, weight
- add item from currently held item
- remove selected item

Also available from server console:

- `loot.catalog.export`

### Useful commands

- `/lootcfg where` - look at a container and show keys for config
- `/lootcfg reroll` - refill looked container immediately
- `/lootcfg nearby 30` - show container keys around you
- `/lootcfg list` - show configured rules
- `/lootcfg show <key>` - show one rule details
- `/lootcfg additem <key> <shortname> <min> <max> <chance> [weight]`
- `/lootcfg delitem <key> <index>`
- `/lootcfg clear <key>`
- `/lootcfg draws <key> <minRolls> <maxRolls>`
- `/lootcfg enabled <key> on|off`
- `/lootcfg override <key> on|off`
- `/lootcfg duplicates <key> on|off`
- `/lootcfg forceone <key> on|off`
- `/lootcfg maxstacks <key> <number>`

`chance` accepts either `0..1` or `0..100` (percent).

## Config path

- `C:\rust\server\oxide\config\ContainerLootManager.json`

Add/edit rules there too if you prefer JSON editing.
