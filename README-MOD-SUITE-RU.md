# Rust Mod Suite (by Shmatko)

Полноценный набор для серверов Rust (Oxide/uMod), который закрывает полный цикл:

1. Локальная установка тестового сервера
2. Настройка плагинов через удобные UI-конфигураторы
3. Локальная проверка результата
4. Заливка настроенного набора на production сервер

## Что внутри

- `privilege-system`  
  Плагин привилегий `PrivilegeSystem` (ранги, бонусы, teleports, remove/recycler).
- `container-loot-manager`  
  Плагин лута `ContainerLootManager` (настройка лута по типам контейнеров).
- `privilege-configurator`  
  Web UI-конфигуратор `PrivilegeSystem.json`.
- `loot-configurator`  
  Web UI-конфигуратор `ContainerLootManager.json`.
- `mod-suite`  
  Единый хаб с быстрыми переходами, статусом API и автодеплоем плагинов.
- `server-scripts`  
  Скрипты локального Rust сервера: установка/обновление/старт/стоп.

## Ключевые скрипты

- `install-mod-suite.ps1`  
  Автоустановка: локальный тестовый Rust сервер + Oxide + деплой модов + запуск UI-хаба.
- `open-mod-suite.ps1`  
  Запуск только unified хаба.
- `verify-local-mod-suite.ps1`  
  Автопроверка локального стенда (API, файлы, загрузка плагинов).
- `deploy-mod-suite.ps1`  
  Локальный деплой плагинов и (опционально) конфигов в `C:\rust\server`.
- `deploy-mod-suite-remote.ps1`  
  Загрузка на реальный сервер по SSH/SCP.
- `build-mod-suite-release.ps1`  
  Сборка release-архива.

## Требования

- Windows + PowerShell 5+/7+
- Python (`python` или `py`) для локального API конфигураторов
- OpenSSH client (`ssh.exe`, `scp.exe`) для remote deploy
- Интернет для SteamCMD/Rust/Oxide (если делаешь автоустановку локального сервера)

## Быстрый старт

### 1) Полная локальная установка

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\install-mod-suite.ps1
```

### 2) Открыть хаб вручную (если нужно)

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\open-mod-suite.ps1
```

URL:

- `http://127.0.0.1:18765/mod-suite/index.html`

### 3) Локальная проверка

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\verify-local-mod-suite.ps1
```

### 4) Деплой на production

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 `
  -RemoteHost "<host>" `
  -RemoteUser "<user>" `
  -RemoteRoot "<serverfiles-path>"
```

После деплоя на проде:

```text
oxide.reload ContainerLootManager
oxide.reload PrivilegeSystem
```

## Безопасный production-процесс

1. Настраиваешь всё локально через UI.
2. Проверяешь локально в игре.
3. Делаешь dry-run удаленного деплоя:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 `
  -RemoteHost "<host>" -RemoteUser "<user>" -RemoteRoot "<path>" -DryRun
```

4. Деплой с backup:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 `
  -RemoteHost "<host>" -RemoteUser "<user>" -RemoteRoot "<path>" -CreateBackups
```

## Документация

- Основной пошаговый гайд: `MOD-SUITE-GUIDE.md`
- Краткая серверная сводка: `README-RUST-SERVER.md`
- Форумный анонс (готовый текст): `FORUM-ANNOUNCE-RU.md`

## Авторство

- Core package and integration flow: **Shmatko**
- Plugin authors in Oxide metadata:
  - `PrivilegeSystem` -> `Shmatko`
  - `ContainerLootManager` -> `Shmatko`

