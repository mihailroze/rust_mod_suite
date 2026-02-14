# Конфигуратор лута

Офлайн-утилита для настройки `ContainerLootManager`.

## Что уже внутри

- Полный встроенный каталог предметов и контейнеров из сервера.
- Русский интерфейс.
- Русские названия предметов в формате `RU / EN` (в списке и в таблице предметов правила).
- Генерация готового `ContainerLootManager.json`.
- В конфиг автоматически добавляются правила для всех контейнеров.
- Если в каталоге есть `Observed rules`, контейнеры сразу заполняются лутом, который уже спавнился на сервере.
- Иконки контейнеров в списке правил (чтобы было видно тип крейта/контейнера).
- Быстрые подсказки при добавлении предмета (по `shortname`, `RU/EN` названию и категории) + добавление по `Enter`.
- Кнопка паттерна "улучшенный лут" для массового буста всех правил по контейнерам.

## Запуск

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\open-configurator.ps1
```

или открыть:

- `C:\rust\mods\loot-configurator\index.html`

## Как обновить встроенный каталог после обновления Rust

1. На сервере:
   - `/lootcfg exportcatalog` (или `loot.catalog.export` в консоли)
2. На машине:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\sync-catalog-from-server.ps1
```

3. Обновить русские названия предметов (опционально, но желательно после апдейта Rust):

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\generate-ru-item-names.ps1
```

## Иконки контейнеров (крейтов)

- Иконки хранятся в:
  - `C:\rust\mods\loot-configurator\assets\container-icons`
- Обновить/скачать набор иконок:

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\sync-container-icons.ps1
```

## Рабочий процесс

1. Открой утилиту.
2. При необходимости выбери профиль в блоке `Паттерн улучшенного лута` и нажми `Применить ко всем контейнерам`.
3. Точно подправь нужные контейнеры вручную (если требуется).
4. Нажми `Скачать JSON`.
5. Положи файл в:
   - `C:\rust\server\oxide\config\ContainerLootManager.json`
6. Перезагрузи плагин:
   - `oxide.reload ContainerLootManager`

## Auto-save button

- Start configurator with:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\loot-configurator\open-configurator.ps1`
- In UI click `Сохранить на сервер`.
- Config will be written automatically to:
  - `C:\rust\server\oxide\config\ContainerLootManager.json`
- Then reload plugin:
  - `oxide.reload ContainerLootManager`

## Auto-deploy plugin button

- In UI click `Развернуть плагин`.
- Source file:
  - `C:\rust\mods\container-loot-manager\ContainerLootManager.cs`
- Deploy target:
  - `C:\rust\server\oxide\plugins\ContainerLootManager.cs`
- Reload on server:
  - `oxide.reload ContainerLootManager`

## Unified suite launcher

- One entry point for both configurators + deploy tools:
  - `powershell -ExecutionPolicy Bypass -File C:\rust\mods\open-mod-suite.ps1`
- Hub URL:
  - `http://127.0.0.1:18765/mod-suite/index.html`

---

Maintained by Shmatko.
