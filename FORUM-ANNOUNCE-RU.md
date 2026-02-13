# [RELEASE] Rust Mod Suite by Shmatko

Привет, коллеги.

Выкладываю **Rust Mod Suite** — единый набор для настройки и эксплуатации двух ключевых модулей сервера:

- `PrivilegeSystem` (привилегии, ранги, бонусы, телепорты, remove/recycler)
- `ContainerLootManager` (гибкая система лута по типам контейнеров)

Главная идея:  
**всё настраивается и проверяется локально, после чего в 1 шаг отправляется на production сервер.**

---

## Что умеет Rust Mod Suite

### 1) Полная локальная автоустановка

Одна команда поднимает локальный стенд:

- Rust test server
- Oxide/uMod
- деплой двух плагинов
- запуск unified UI-хаба

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\install-mod-suite.ps1
```

### 2) Единый UI-хаб

Через `mod-suite`:

- быстрый вход в оба конфигуратора
- авторазвертывание плагинов
- health-check локального API

URL:

- `http://127.0.0.1:18765/mod-suite/index.html`

### 3) Удобные конфигураторы

- `Privilege Configurator`
  - ранги, бонусы, daily, teleport features
  - автосохранение `PrivilegeSystem.json` на сервер
  - кнопка автодеплоя `PrivilegeSystem.cs`
- `Loot Configurator`
  - правила лута по ключам контейнеров
  - автосохранение `ContainerLootManager.json`
  - кнопка автодеплоя `ContainerLootManager.cs`

### 4) Локальная автопроверка

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\verify-local-mod-suite.ps1
```

Проверяет:

- API `/health`
- наличие нужных `.cs` и `.json`
- факт загрузки плагинов по логам Oxide

### 5) Production deploy по SSH/SCP

```powershell
powershell -ExecutionPolicy Bypass -File C:\rust\mods\deploy-mod-suite-remote.ps1 `
  -RemoteHost "<host>" `
  -RemoteUser "<user>" `
  -RemoteRoot "<serverfiles-path>"
```

Поддерживается:

- `-DryRun`
- `-CreateBackups`
- `-IdentityFile` (SSH key)

---

## Кому полезно

- Владельцам Rust серверов, кто хочет стабильный workflow:
  - локальная настройка
  - локальное тестирование
  - контролируемый релиз на прод
- Админам, у кого часто меняются:
  - наборы лута
  - баланс привилегий
  - серверные конфиги

---

## Быстрый сценарий работы

1. Установить/поднять локальный стенд (`install-mod-suite.ps1`).
2. Настроить всё через UI.
3. Протестировать в локальной игре.
4. Проверить `verify-local-mod-suite.ps1`.
5. Сделать `deploy-mod-suite-remote.ps1` на прод.
6. Выполнить на проде:

```text
oxide.reload ContainerLootManager
oxide.reload PrivilegeSystem
```

---

## Что в архиве

- оба плагина
- оба конфигуратора
- unified hub
- скрипты автоустановки/проверки/деплоя
- подробная документация

---

## Важное

- Авторство плагинов в Oxide: **Shmatko**
- Интерфейс `mod-suite` на русском
- Полный гайд в комплекте: `MOD-SUITE-GUIDE.md`

---

Если нужен, могу выложить отдельно:

1. минимальный “lite” архив (без локального сервера),
2. шаблон CI/CD пайплайна для авто-доставки конфигов на прод,
3. версию с авто-reload через RCON после remote deploy.

