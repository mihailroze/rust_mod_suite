# Rust Mod Suite Hub (by Shmatko)

Единая точка входа для:
- `PrivilegeSystem`
- `ContainerLootManager`
- `privilege-configurator`
- `loot-configurator`

## Запуск

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\open-mod-suite.ps1
```

Далее открой:
- `http://127.0.0.1:18765/mod-suite/index.html`

## Что дает хаб

- Быстрые ссылки на оба конфигуратора
- Проверка статуса API (`/health`)
- Развертывание в один клик:
  - `PrivilegeSystem.cs`
  - `ContainerLootManager.cs`

## Полный гайд

См.:
- `C:\rust\mods\MOD-SUITE-GUIDE.md`

## Скрипты автоматизации

- Локальная автоустановка:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\install-mod-suite.ps1`
- Локальная проверка:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\verify-local-mod-suite.ps1`
- Деплой на production (SSH/SCP):
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 -RemoteHost <host> -RemoteUser <user> -RemoteRoot <path>`
