# Конфигуратор PrivilegeSystem

Офлайн-утилита для настройки `PrivilegeSystem`.

## Что умеет

- Редактирование общих настроек:
  - `Notify player on connect`
  - `Expiry check interval (seconds)`
- Русские пояснения к настройкам прямо в UI.
- Настройка интеграций через permissions внешних плагинов:
  - телепортация
  - дома (база + шаблон лимита точек)
  - карманный переработчик
  - команда `/remove`
- Настройка блоков:
  - `Daily rewards` (enabled, cooldown, allow without rank, base items)
  - `Teleport features` (relay-команды и базовые кд/лимиты)
  - `Packages`
  - `Audit` (enabled/max/echo)
  - `External bridge` (api/server key/poll/batch/timeout)
- Полное редактирование рангов:
  - ключ, `Display name`, `Chat tag`, `Chat color`, `Oxide group`
  - фичи ранга:
    - `Allow teleport`
    - `Home points`
    - `Allow pocket recycler`
    - `Allow remove command`
    - `Daily reward multiplier`
    - `Home teleport cooldown reduction (seconds)`
    - `Team teleport cooldown reduction (seconds)`
    - `Town teleport daily limit bonus`
  - `Gather multiplier`
  - `Ground pickup multiplier`
  - `Container loot multiplier`
  - `NPC kill scrap reward`
  - `Rank kit cooldown seconds`
  - `Rank kit amount multiplier`
  - `Permissions`
  - `Rank kit items`
- Генерация готового `PrivilegeSystem.json`.
- Подсказки предметов в формате `RU / EN` при настройке rank kit.
- Форматы ввода:
  - Daily items: `shortname amount` по строкам
  - Package lines: `key|name|rank|days|eco|rp` по строкам

## Запуск

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\privilege-configurator\open-configurator.ps1
```

или открыть:

- `C:\rust\mods\privilege-configurator\index.html`

## Важно про каталог предметов

По умолчанию утилита использует каталог из:

- `C:\rust\mods\loot-configurator\catalog-embedded.js`
- `C:\rust\mods\loot-configurator\item-names-ru.js`

Если этих файлов нет, можно загрузить каталог вручную кнопкой `Загрузить каталог предметов`
(файл экспорта `ContainerLootCatalog.json` от `ContainerLootManager`).

## Рабочий процесс

1. Открой утилиту.
2. Настрой ранги и их бонусы.
3. При необходимости настрой `Daily rewards`, `Teleport features`, `Packages`, `Audit`.
4. Нажми `Скачать JSON`.
5. Положи файл в:
   - `C:\rust\server\oxide\config\PrivilegeSystem.json`
6. Перезагрузи плагин:
   - `oxide.reload PrivilegeSystem`

## Auto-save button

- Start configurator with:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\privilege-configurator\open-configurator.ps1`
- In UI click `Сохранить на сервер`.
- Config will be written automatically to:
  - `C:\rust\server\oxide\config\PrivilegeSystem.json`
- Then reload plugin:
  - `oxide.reload PrivilegeSystem`

## Auto-deploy plugin button

- Start configurator via local server:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\privilege-configurator\open-configurator.ps1`
- Click `Развернуть плагин`.
- Source file:
  - `C:\rust\mods\privilege-system\PrivilegeSystem.cs`
- Deploy target:
  - `C:\rust\server\oxide\plugins\PrivilegeSystem.cs`
- Reload on server:
  - `oxide.reload PrivilegeSystem`

## Unified suite launcher

- One entry point for both configurators + deploy tools:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\open-mod-suite.ps1`
- Hub URL:
  - `http://127.0.0.1:18765/mod-suite/index.html`

---

Maintained by Shmatko.


