# Rust Mod Suite (Community Edition, by Shmatko)

Готовый набор для Rust-серверов (Oxide/uMod):
- привилегии (`PrivilegeSystem`),
- лут (`ContainerLootManager`),
- два UI-конфигуратора,
- единый локальный хаб,
- автоустановка/проверка/деплой (включая remote deploy на production).

## Состав репозитория

- `privilege-system/` - плагин `PrivilegeSystem.cs`
- `container-loot-manager/` - плагин `ContainerLootManager.cs`
- `privilege-configurator/` - UI + local API для конфигурации привилегий
- `loot-configurator/` - UI для конфигурации лута
- `mod-suite/` - единый веб-хаб запуска и авторазвертывания
- `server-scripts/` - скрипты локального Rust-сервера (install/update/start/stop)
- `install-mod-suite.ps1` - полный локальный bootstrap
- `verify-local-mod-suite.ps1` - проверка локального стенда
- `deploy-mod-suite.ps1` - деплой в локальный `C:\rust\server`
- `deploy-mod-suite-remote.ps1` - деплой на реальный сервер по SSH/SCP
- `build-mod-suite-release.ps1` - сборка релизного архива

## Быстрый старт

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\install-mod-suite.ps1
```

После установки открыть хаб:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\open-mod-suite.ps1
```

URL хаба:
- `http://127.0.0.1:18765/mod-suite/index.html`

## Локальная проверка

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\verify-local-mod-suite.ps1
```

## Деплой на production

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 `
  -RemoteHost "<host>" `
  -RemoteUser "<user>" `
  -RemoteRoot "<serverfiles-path>"
```

После деплоя на сервере:

```text
oxide.reload ContainerLootManager
oxide.reload PrivilegeSystem
```

## Документация

- Полный гайд: `MOD-SUITE-GUIDE.md`
- Русский README пакета: `README-MOD-SUITE-RU.md`
- Краткая серверная сводка: `README-RUST-SERVER.md`

## Авторство

Core integration and packaging: **Shmatko**.
Plugin metadata authors: **Shmatko**.
