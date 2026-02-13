# Rust Mod Suite: Privilege + Loot (Community Edition, by Shmatko)

Этот пакет предназначен для полного цикла:
- локальная автоустановка тестового Rust сервера;
- настройка привилегий и лута через UI;
- локальная проверка;
- финальная загрузка настроенных плагинов/конфигов на реальный сервер.

## 1) Что входит в комплект

В `C:\rust\mods`:
- `privilege-system\PrivilegeSystem.cs`
- `container-loot-manager\ContainerLootManager.cs`
- `privilege-configurator\` (GUI + local API server)
- `loot-configurator\` (GUI)
- `mod-suite\` (единый hub)
- `server-scripts` (скрипты локального сервера: install/update/start/stop)
- `install-mod-suite.ps1` (полная автоустановка локального стенда)
- `open-mod-suite.ps1` (запуск unified hub)
- `verify-local-mod-suite.ps1` (автопроверка локального стенда)
- `deploy-mod-suite.ps1` (локальный деплой в `C:\rust\server`)
- `deploy-mod-suite-remote.ps1` (деплой на реальный сервер по SSH/SCP)
- `build-mod-suite-release.ps1` (сборка release zip)

## 2) Требования

- Windows + PowerShell 5+/7+
- Интернет для SteamCMD/Rust/Oxide (на этапе локальной установки)
- Python (`python` или `py`) для локального API конфигураторов
- OpenSSH client (`ssh.exe`, `scp.exe`) для remote deploy
- Права на запись в:
  - `C:\rust\server\oxide\plugins`
  - `C:\rust\server\oxide\config`

## 3) Быстрый старт (1 команда)

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\install-mod-suite.ps1
```

Что делает скрипт:
1. Ставит/обновляет локальный Rust тест-сервер (`C:\rust\server`) через `C:\rust\scripts\setup-rust-test-server.ps1`.
2. Ставит Oxide.
3. Копирует плагины в локальный сервер.
4. Поднимает локальный сервер (если не запущен).
5. Запускает unified UI hub.
6. Выполняет базовую автопроверку локального стенда.

## 4) Ручной запуск unified hub

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\open-mod-suite.ps1
```

Откроется:
- `http://127.0.0.1:18765/mod-suite/index.html`

В hub доступны:
- переход в `Privilege Configurator`;
- переход в `Loot Configurator`;
- деплой обоих плагинов одной кнопкой;
- проверка health локального API.

## 5) Настройка через UI

### Privilege Configurator
- Открыть `Open Privilege Configurator`
- Настроить ранги/бонусы/модули
- Нажать `Сохранить на сервер`
- При необходимости нажать `Развернуть плагин`

### Loot Configurator
- Открыть `Open Loot Configurator`
- Настроить правила контейнеров
- Нажать `Сохранить на сервер`
- При необходимости нажать `Развернуть плагин`

## 6) Локальный деплой и reload

CLI-деплой в локальный сервер:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite.ps1
```

После деплоя в консоли сервера/RCON:

```text
oxide.reload ContainerLootManager
oxide.reload PrivilegeSystem
```

## 7) Автоматическая локальная проверка

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\verify-local-mod-suite.ps1
```

Строгий режим (требует запущенный `RustDedicated`):

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\verify-local-mod-suite.ps1 -RequireServerProcess
```

Проверяется:
- `GET /health` локального API;
- наличие локально задеплоенных `.cs` и `.json`;
- факт загрузки плагинов по последнему `oxide_*.txt`.

## 8) Деплой на реальный сервер (production)

### Базовый пример

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 `
  -RemoteHost "203.0.113.10" `
  -RemoteUser "rustadmin" `
  -RemoteRoot "/home/rustserver/serverfiles"
```

### С SSH-ключом и backup перед overwrite

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 `
  -RemoteHost "203.0.113.10" `
  -RemoteUser "rustadmin" `
  -RemoteRoot "/home/rustserver/serverfiles" `
  -IdentityFile "C:\Users\you\.ssh\id_ed25519" `
  -CreateBackups
```

### Dry-run (без реальной загрузки)

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 `
  -RemoteHost "203.0.113.10" `
  -RemoteUser "rustadmin" `
  -RemoteRoot "/home/rustserver/serverfiles" `
  -DryRun
```

Скрипт загружает:
- плагины в `<RemoteRoot>/oxide/plugins`
- конфиги в `<RemoteRoot>/oxide/config`

После загрузки на проде выполнить:

```text
oxide.reload ContainerLootManager
oxide.reload PrivilegeSystem
```

## 9) Полный сценарий для пользователя

1. Запустить `install-mod-suite.ps1`.
2. Настроить все в UI (`mod-suite`).
3. Проверить локально через `verify-local-mod-suite.ps1` + ручные игровые тесты.
4. Выполнить `deploy-mod-suite-remote.ps1` на реальный сервер.
5. Сделать `oxide.reload` на реальном сервере.

## 10) Сборка архива для публикации в сообществе

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\build-mod-suite-release.ps1
```

Результат:
- папка: `C:\rust\mods\releases\rust-mod-suite-<timestamp>`
- архив: `C:\rust\mods\releases\rust-mod-suite-<timestamp>.zip`

## 11) Troubleshooting

### Не работает кнопка "Сохранить на сервер"
- Запусти UI только через `open-mod-suite.ps1` или `open-configurator.ps1`
- Проверь `http://127.0.0.1:18765/health`

### Ошибки компиляции плагинов
- Проверь:
  - `C:\rust\server\oxide\logs\oxide_YYYY-MM-DD.txt`
  - `C:\rust\server\oxide\logs\oxide.compiler_YYYY-MM-DD.log`

### Remote deploy не подключается
- Проверь SSH доступ:
  - `ssh <user>@<host>`
- Проверь путь `-RemoteRoot` (должен содержать `oxide/plugins` и `oxide/config`)
- Проверь firewall/порт `22` (или свой `-RemotePort`)

### Плагин загружен, но поведение не изменилось
- Убедись, что конфиги тоже загружены на прод
- Выполни `oxide.reload` для обоих плагинов
- Проверь, что редактировался именно нужный server identity

---

Copyright and maintenance: by Shmatko.
