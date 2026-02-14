# Rust тестовый сервер (маленькая карта) для модов

## Что уже подготовлено

- `C:\rust\steamcmd` - SteamCMD
- `C:\rust\server` - файлы сервера RustDedicated
- `C:\rust\mods\oxide-rust.zip` - скачанный архив Oxide
- `C:\rust\scripts\setup-rust-test-server.ps1` - полный скрипт подготовки сервера
- `C:\rust\scripts\update-rust-server.ps1` - скрипт обновления сервера
- `C:\rust\scripts\install-oxide.ps1` - скрипт установки/обновления Oxide
- `C:\rust\scripts\start-test-server.ps1` - скрипт запуска (маленькая карта)
- `C:\rust\scripts\stop-test-server.ps1` - скрипт остановки сервера

## Где лежат server-scripts

- Основной путь (по умолчанию): `C:\rust\scripts\*.ps1`
- Копия в репозитории/релизе: `C:\rust\mods\server-scripts\*.ps1`

Эквивалентные примеры:

- обычный путь: `powershell -ExecutionPolicy Bypass -File C:\rust\scripts\start-test-server.ps1`
- путь из репозитория: `powershell -ExecutionPolicy Bypass -File C:\rust\mods\server-scripts\start-test-server.ps1`

## Первый запуск

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\start-test-server.ps1
```

## Пример запуска с кастомными настройками

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\start-test-server.ps1 `
  -HostName "My Local Mod Test" `
  -RconPassword "MyStrongRconPass_2026" `
  -WorldSize 1000 `
  -Seed 12345 `
  -MaxPlayers 5 `
  -Insecure
```

## Обновить сервер после патча Rust

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\update-rust-server.ps1
```

## Переустановить/обновить Oxide

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\install-oxide.ps1
```

## Остановить сервер

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\stop-test-server.ps1
```

## Полная подготовка одной командой (если нужно)

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\scripts\setup-rust-test-server.ps1
```

## Примечания

- Маленькая тестовая карта задается через `-WorldSize 1000`.
- Данные сервера сохраняются в `C:\rust\server\server\modtest`.
- Порты по умолчанию: игровой `28015`, RCON `28016`.
- Если тестируешь только локально и есть проблемы с EAC, используй `-Insecure`.

## Плагин лута контейнеров

- Исходник: `C:\rust\mods\container-loot-manager\ContainerLootManager.cs`
- Конфиг: `C:\rust\server\oxide\config\ContainerLootManager.json`
- Админ-команда в игре: `/lootcfg help`
- Визуальный редактор: `/lootui`
- Экспорт каталога лута: `/lootcfg exportcatalog` -> `C:\rust\server\oxide\data\ContainerLootCatalog.json`
- Массовый респавн лута по карте: `/lootcfg respawnall [all|custom]` (или в консоли сервера `loot.respawnall [all|custom]`)

Деплой:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\container-loot-manager\scripts\deploy.ps1
```

## Отдельный конфигуратор лута

- Папка: `C:\rust\mods\loot-configurator`
- Запуск: `powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\open-configurator.ps1`
- Результат утилиты: `ContainerLootManager.json` (скачивается из UI)
- В утилиту уже встроены все контейнеры и весь лут (экспорт с сервера).
- Контейнеры при старте уже заполнены текущим спавнящимся лутом (Observed rules из каталога).
- Названия предметов отображаются как `RU / EN`.
- В правиле контейнера есть точечная настройка бонуса привилегий: блокировка, масштаб (0..1) и лимит доп. стаков (если используется PrivilegeSystem).
- Обновление встроенного каталога:
  - Выполнить `/lootcfg exportcatalog` на сервере
  - Запустить `powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\sync-catalog-from-server.ps1`
- Обновление словаря русских названий после апдейта каталога:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\generate-ru-item-names.ps1`

## Плагин привилегий

- Исходник: `C:\rust\mods\privilege-system\PrivilegeSystem.cs`
- Конфиг: `C:\rust\server\oxide\config\PrivilegeSystem.json`
- Админ-UI в игре: `/privui`
- Статус игрока: `/vip` или `/priv my`
- Получение rank kit: `/rankkit`
- Daily-награда: `/daily`
- Встроенные утилиты по рангу: `/remove [off]` (включается только с киянкой в руке), `/recycler [off]` (личный переработчик только через UI, без модели перед игроком, быстрее обычного, кд команды настраивается)
- Встроенные телепорты: `/sethome <name>`, `/home <name>`, `/homes`, `/removehome <name>`, `/hometp <home>`, `/towntp`, `/teamtp <teammate>`, `/priv settown` (home можно ставить только на своем фундаменте/полу, где есть твой спальник или полотенце)
- Просмотр аудита (админ): `/priv audit [count]`

Деплой:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\privilege-system\scripts\deploy.ps1
```

## Отдельный конфигуратор привилегий

- Папка: `C:\rust\mods\privilege-configurator`
- Запуск: `powershell -ExecutionPolicy Bypass -File C:\rust\mods\privilege-configurator\open-configurator.ps1`
- Результат утилиты: `PrivilegeSystem.json` (скачивается из UI)
- Настраиваются ранги, permissions, множители, rank kit, daily/tp/audit.
- Для подсказок предметов используется каталог из `loot-configurator`.

## Unified Mod Suite (рекомендуется)

- Единый запускатор двух конфигураторов + деплой плагинов в один клик:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\open-mod-suite.ps1`
- URL хаба:
  - `http://127.0.0.1:18765/mod-suite/index.html`
- Деплой сразу двух плагинов:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite.ps1`
- Полный гайд по установке и эксплуатации:
  - `C:\rust\mods\MOD-SUITE-GUIDE.md`

## Автоустановка + production deploy (by Shmatko)

- Полная локальная автоустановка (сервер + oxide + плагины + suite):
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\install-mod-suite.ps1`
- Локальная проверка:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\verify-local-mod-suite.ps1`
- Загрузка на production по SSH/SCP:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 -RemoteHost <host> -RemoteUser <user> -RemoteRoot <path>`
