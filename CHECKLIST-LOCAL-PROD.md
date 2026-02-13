# Чек-лист локального теста (как перед продом)

## 1. Старт сервисов

- [ ] Backend запущен: `http://localhost:8001/api/v1/health` отвечает `{"status":"ok"}`.
- [ ] Web запущен: `http://localhost:8080` открывается (статус `200`).
- [ ] В `backend/.env` стоят актуальные ключи:
- [ ] `SERVER_SHARED_KEY`
- [ ] `ADMIN_SHARED_KEY`
- [ ] `AUTH_SECRET_KEY`

## 2. Валюта и пакеты

- [ ] `GET /api/v1/public/packages` возвращает валюту:
- [ ] `currency_name = Rusty Bay Doubloons`
- [ ] `currency_code = RBD`
- [ ] `currency_rate_to_rub = 1.0`
- [ ] На сайте отображаются пакеты и цена в `RBD`.

## 3. Авторизация Steam и ник

- [ ] Кнопка `Войти через Steam` отправляет на Steam OpenID.
- [ ] После возврата `GET /api/v1/public/auth/me` показывает `is_authenticated = true`.
- [ ] В `buyer_name` приходит Steam-ник (или fallback `steam:<id>`, если Steam API недоступен).
- [ ] Без Steam-сессии `POST /api/v1/public/orders/demo-create` возвращает `401`.

## 4. Ручное начисление валюты админом

- [ ] `POST /api/v1/admin/wallets/grant` с `X-Admin-Key` начисляет валюту.
- [ ] `GET /api/v1/admin/wallets/{steam_id}` показывает обновленный баланс.
- [ ] В UI админ-блоке начисление работает и показывает `balance_before -> balance_after`.

## 5. Покупка за валюту

- [ ] Без баланса `POST /api/v1/public/orders/demo-create` возвращает `402`.
- [ ] После начисления валюты покупка проходит.
- [ ] В ответе покупки приходит `price` и `balance_after`.
- [ ] В UI баланс уменьшается после покупки.

## 6. Заказ и связка с плагином

- [ ] `POST /api/v1/server/orders/claim` с `X-Server-Key` возвращает заказ.
- [ ] `POST /api/v1/server/orders/complete` переводит заказ в `completed`.
- [ ] `GET /api/v1/admin/orders` показывает заказ со статусом `completed`.
- [ ] В `PrivilegeSystem.json` секция `Web shop bridge` настроена корректно.
- [ ] Выполнен `oxide.reload PrivilegeSystem`.
- [ ] Команда `priv.shopsync` (или `/priv shopsync`) отрабатывает без ошибок.

## 7. Тест в игре (end-to-end)

- [ ] Создан заказ на твой SteamID.
- [ ] Команда в игре `/priv activate` активирует покупки только для SteamID вызвавшего игрока.
- [ ] После poll/sync в игре выдан нужный ранг.
- [ ] В `priv info <steamid64>` видно новый ранг и срок.

## 8. Негативные проверки

- [ ] С неверным `X-Server-Key` claim/complete возвращает `401`.
- [ ] С неверным `X-Admin-Key` admin endpoints возвращают `401`.
- [ ] При несуществующем `package_key` покупка возвращает `404`.

## 9. Минимум перед деплоем

- [ ] Сменены дефолтные ключи (`change-me-*`) на безопасные.
- [ ] API работает за reverse proxy + HTTPS.
- [ ] Для HTTPS включено `AUTH_COOKIE_SECURE=true`.
- [ ] Настроены бэкапы БД заказов и кошельков.
- [ ] Проверен путь rollback (можно откатить плагин/конфиг).
