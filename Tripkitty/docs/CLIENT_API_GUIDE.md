# Tripkitty API — руководство для клиента

Документ для разработчиков фронтенда (PWA / мобильный клиент). Описывает, как
правильно работать с REST API, аутентификацией, realtime-обновлениями (SignalR)
и Web Push.

- **Base URL (dev):** `http://localhost:5010`
- **Формат тела запросов/ответов:** `application/json`, кодировка UTF-8.
- **Именование полей в JSON:** `camelCase` (например, `accessToken`, `ownerId`).
- **OpenAPI-схема (только Development):** `GET /openapi/v1.json`.

> Денежные суммы в запросах и ответах передаются как **decimal с 2 знаками**
> (например `1200.50`). Внутри сервер хранит их в копейках/центах, но клиента
> это не касается — он всегда работает с обычными суммами.

---

## 1. Аутентификация

### 1.1 Модель токенов

- **Access token** — JWT, живёт **15 минут**. Передаётся в каждом защищённом
  запросе в заголовке `Authorization: Bearer <accessToken>`.
- **Refresh token** — живёт **30 дней**. Хранится у клиента, используется только
  для получения новой пары токенов. На сервере хранится в виде SHA-256-хэша.

**Рекомендация по хранению на клиенте:** access token — в памяти; refresh token —
в защищённом хранилище (для PWA — IndexedDB / `httpOnly`-cookie на уровне
прокси, если он есть). Не кладите токены в `localStorage` без необходимости.

### 1.2 Регистрация

`POST /auth/register`

```json
{
  "lastName": "Иванова",
  "firstName": "Аня",
  "middleName": "Петровна",
  "handle": "anya",
  "email": "anya@example.com",
  "password": "secret123"
}
```

`lastName` и `firstName` обязательны, `middleName` опционально (можно `null`
или не передавать вовсе).

Ответ `200`:

```json
{
  "user": {
    "id": "u_...",
    "name": "Аня Иванова",
    "lastName": "Иванова",
    "firstName": "Аня",
    "middleName": "Петровна",
    "handle": "anya",
    "email": "anya@example.com"
  },
  "tokens": { "accessToken": "eyJ...", "refreshToken": "..." }
}
```

`name` — вычисляемое сервером отображаемое имя («Имя Фамилия», без отчества) —
удобно для прямого вывода в UI. Оно присутствует во всех объектах пользователя
(`user`, `members[]`, `friends[]`) наряду с `lastName`/`firstName`/`middleName`.

Возможные ошибки: `HANDLE_TAKEN` (409), `EMAIL_TAKEN` (409), `VALIDATION_ERROR`
(400, `field`: `lastName` / `firstName` / `handle` / `email` / `password`).

### 1.3 Вход

`POST /auth/login`

```json
{ "email": "anya@example.com", "password": "secret123" }
```

Ответ — та же структура `{ user, tokens }`. Ошибки (обе 422):
`USER_NOT_FOUND` (`field: "email"`) — аккаунта с такой почтой нет;
`WRONG_PASSWORD` (`field: "password"`) — пароль не подошёл.

### 1.4 Обновление токенов

`POST /auth/refresh`

```json
{ "refreshToken": "..." }
```

Ответ `200`: новая пара `{ accessToken, refreshToken }` (плюс `user`).
Если refresh-токен невалиден/истёк — `INVALID_TOKEN` (422). В этом случае клиент
обязан разлогинить пользователя и отправить его на экран входа.

### 1.5 Выход

`POST /auth/logout` с телом `{ "refreshToken": "..." }` — отзывает refresh-токен
на сервере. Access-токен останется валидным до истечения 15 минут (он stateless),
поэтому клиент должен дополнительно удалить токены локально.

### 1.6 Текущий пользователь

`GET /auth/me` (требует `Authorization`). Ответ:

```json
{ "user": { "id": "u_...", "name": "Аня Иванова", "lastName": "Иванова", "firstName": "Аня", "middleName": "Петровна", "handle": "anya", "email": "..." } }
```

### 1.6a Редактирование своего профиля (ФИО)

`PATCH /auth/me` (требует `Authorization`). Частичное обновление ФИО — передавайте
только изменяемые поля. Тело:

```json
{ "lastName": "Иванова", "firstName": "Аня", "middleName": "Петровна" }
```

- Любое из полей можно опустить (`null`) — оно не изменится.
- `lastName`/`firstName` при передаче не могут быть пустыми (`VALIDATION_ERROR`).
- `middleName: ""` — сбросить отчество.

Ответ — тот же конверт, что у `GET /auth/me`: `{ "user": UserDto }`. Реквизиты СБП
профиля редактируются отдельно через `/me/payment-methods` (см. §10).

### 1.7 Рекомендуемый паттерн HTTP-клиента

Оберните fetch/axios в интерсептор:

1. Подставляйте `Authorization: Bearer <accessToken>` во все запросы, кроме
   `/auth/register`, `/auth/login`, `/auth/refresh`.
2. На ответ `401` — **один раз** дёрните `/auth/refresh`, обновите пару токенов
   и повторите исходный запрос.
3. Если refresh тоже вернул ошибку — разлогиньте пользователя.
4. Защитите refresh от гонок: при нескольких параллельных `401` выполняйте
   только один refresh, остальные запросы ставьте в очередь до его завершения.

> Access-токен живёт всего 15 минут, поэтому корректный авто-refresh —
> обязательная часть клиента, а не опция.

---

## 2. Формат ошибок

Все доменные ошибки приходят в едином виде:

```json
{
  "error": {
    "code": "HANDLE_TAKEN",
    "message": "Логин @anya уже занят",
    "field": "handle",
    "details": null
  }
}
```

- `code` — машиночитаемый код (используйте его в логике, не текст `message`).
- `message` — человекочитаемый текст (можно показывать пользователю).
- `field` — имя поля формы, к которому относится ошибка (может быть `null`).
- `details` — произвольный объект с доп. данными под конкретный код (может быть `null`); формат зависит от `code`, см. описания эндпоинтов.

### Карта кодов → HTTP-статусов

| HTTP | Коды |
|------|------|
| `400 Bad Request` | `VALIDATION_ERROR` |
| `403 Forbidden` | `FORBIDDEN` |
| `404 Not Found` | `NOT_FOUND`, `PAYMENT_METHOD_NOT_FOUND`, `GUEST_NOT_FOUND`, `TRANSACTION_NOT_FOUND` |
| `409 Conflict` | `HANDLE_TAKEN`, `EMAIL_TAKEN`, `ALREADY_FRIENDS`, `REQUEST_EXISTS`, `ALREADY_MEMBER`, `PARTICIPANT_HAS_EXPENSES`, `TRIP_SETTLING`, `ALREADY_FINALIZED`, `NOT_FINALIZED`, `TRANSFER_READONLY` |
| `422 Unprocessable Entity` | `SELF_REQUEST`, `INVALID_PAYER`, `INVALID_SHARE`, `USER_NOT_FOUND`, `WRONG_PASSWORD`, `INVALID_TOKEN`, `VERSION_CONFLICT`, `INVALID_PHONE`, `INVALID_BANK` |
| `500 Internal Server Error` | `INTERNAL_ERROR` |
| `401 Unauthorized` | отсутствует/просрочен access-токен (тело без `error`-обёртки — это ответ middleware аутентификации) |

> `401` приходит до доменного слоя, поэтому у него нет JSON-обёртки `error`.
> Различайте его и доменные коды.

---

## 3. Поездки (Trips)

Все эндпоинты ниже требуют `Authorization`.

### 3.1 Список поездок

`GET /trips` → `{ "trips": [TripSummary, ...] }`

```json
{
  "id": "uuid",
  "name": "Грузия 2026",
  "cur": "RUB",
  "ownerId": "u_...",
  "start": "2026-07-01",
  "end": "2026-07-10",
  "version": 4,
  "status": "active"
}
```

`start` / `end` — даты в формате `YYYY-MM-DD` (могут быть `null`).
`version` нужен для оптимистичной блокировки (см. 3.4).
`status` — стадия подсчёта: `"active"` | `"settling"` | `"settled"` (см. §5.5).

### 3.2 Создание

`POST /trips`

```json
{ "name": "Грузия 2026", "cur": "RUB" }
```

`cur` опционально, по умолчанию `"RUB"`. Ответ `201 Created`, заголовок
`Location: /trips/{id}`, тело `{ "trip": TripSummary }`. Создатель автоматически
становится владельцем и первым участником.

### 3.3 Детали поездки

`GET /trips/{id}` → `{ "trip": TripDetail }`:

```json
{
  "id": "uuid",
  "name": "Грузия 2026",
  "cur": "RUB",
  "ownerId": "u_...",
  "start": "2026-07-01",
  "end": "2026-07-10",
  "version": 4,
  "status": "active",
  "members": [ { "id": "u_...", "name": "Аня Иванова", "lastName": "Иванова", "firstName": "Аня", "middleName": "Петровна", "handle": "anya", "email": "..." } ],
  "guests":  [ { "id": "g_...", "name": "Петя Сидоров", "lastName": "Сидоров", "firstName": "Петя", "middleName": null, "paymentDetails": { "phone": "+79991234567", "banks": ["TBANK"], "label": null } } ],
  "expenses":[ { "id": "...", "title": "Такси", "amount": 1200.50, "payer": "u_...", "share": [{"participantId":"u_..."},{"participantId":"g_..."}], "splitType": 0, "createdBy": "u_...", "isTransfer": false } ],
  "events":  [ { "id": "...", "title": "Заезд", "date": "2026-07-01", "time": "14:00", "endTime": null, "createdBy": "u_..." } ]
}
```

### 3.4 Изменение (оптимистичная блокировка)

`PATCH /trips/{id}` — **требует заголовок `If-Match` с текущей версией поездки**:

```
If-Match: 4
```

```json
{ "name": "Грузия (лето)", "start": "2026-07-02", "end": "2026-07-12" }
```

Все поля опциональны (`name`, `start`, `end`). Каждая успешная мутация
увеличивает `version`. Если переданная версия не совпадает с серверной —
ответ `422` с кодом `VERSION_CONFLICT`. Клиент должен в этом случае
перезапросить `GET /trips/{id}`, показать актуальные данные и повторить
изменение с новой версией.

> Заголовок `If-Match` обязателен. Без него — `422 VERSION_CONFLICT`.
> Значение можно слать как `4` или `"4"` (кавычки сервер срезает).

### 3.5 Очистка и удаление

- `POST /trips/{id}/clear` — очищает содержимое поездки (расходы, гостей, зафиксированный
  подсчёт; статус сбрасывается в `active`), сохраняя её.
- `DELETE /trips/{id}` — удаляет поездку целиком.

Оба возвращают `{ "message": "..." }`.

---

## 4. Участники

В поездке два типа участников:

- **Member** (`id` с префиксом `u_`) — зарегистрированный пользователь.
- **Guest** (`id` с префиксом `g_`) — гость без аккаунта, существует только в рамках поездки.

И members, и guests могут быть плательщиком (`payer`) и попадать в `share` расхода.

### 4.1 Добавить участника-пользователя

`POST /trips/{id}/members`

```json
{ "userId": "u_..." }
```

Ответ `{ "member": MemberDto }`. Ошибка `ALREADY_MEMBER` (409), если уже добавлен.

### 4.2 Добавить гостя

`POST /trips/{id}/guests`

```json
{
  "lastName": "Сидоров",
  "firstName": "Петя",
  "middleName": null,
  "paymentDetails": { "phone": "+79991234567", "banks": ["TBANK"], "label": null }
}
```

`lastName` и `firstName` обязательны, `middleName` опционально. `paymentDetails`
опционально — реквизиты гостя для переводов (см. §11). `phone` нормализуется к
`+7XXXXXXXXXX` (только RU-номера, иначе `INVALID_PHONE`), `banks` — непустой список
кодов из `GET /banks` (иначе `INVALID_BANK`).

Ответ `{ "guest": { "id": "g_...", "name": "Петя Сидоров", ..., "paymentDetails": { ... } } }`.

### 4.2a Редактировать гостя (ФИО + реквизиты)

`PATCH /trips/{id}/guests/{guestId}` — доступно любому участнику поездки.
Частичное обновление:

```json
{
  "lastName": "Сидоров",
  "firstName": "Пётр",
  "middleName": "",
  "paymentDetails": { "phone": "+79991234567", "banks": ["SBER"], "label": null },
  "clearPayment": false
}
```

- ФИО: любое поле можно опустить (`null`) — не изменится; `lastName`/`firstName`
  при передаче не могут быть пустыми; `middleName: ""` — сбросить отчество.
- Реквизиты: `paymentDetails` (не `null`) — задать/заменить; `clearPayment: true` —
  сбросить реквизиты; если не передано ни то, ни другое — реквизиты не меняются.

Ответ `{ "guest": GuestDto }`. Мутация шлёт `trip:updated` по SignalR и повышает
`version` поездки. Ошибка `GUEST_NOT_FOUND` (404), если гостя нет в поездке.

### 4.3 Удалить участника

`DELETE /trips/{id}/participants/{participantId}`

Удаление **блокируется**, если участник фигурирует хоть в одном расходе — как
плательщик (`payer`) или в чьём-либо `share`. В этом случае ответ — `409 Conflict`,
код `PARTICIPANT_HAS_EXPENSES`, а `error.details` содержит список блокирующих
расходов:

```json
{
  "error": {
    "code": "PARTICIPANT_HAS_EXPENSES",
    "message": "Нельзя удалить участника, пока на нём есть расходы — сначала удалите или переназначьте их",
    "field": null,
    "details": { "expenseIds": ["exp_1", "exp_2"] }
  }
}
```

Автоматического каскадного удаления расходов нет. Клиент должен сначала сам
удалить или переназначить расходы из `details.expenseIds`
(`DELETE /trips/{id}/expenses/{expenseId}` либо PATCH с новым `payer`/`share`),
и только потом повторить удаление участника. `expenseIds` можно использовать
и для превентивной проверки на клиенте (подсветить блокирующие расходы),
но он всегда актуален на момент ответа сервера — не полагайтесь на локальный
кэш `TripDetail`, если между действиями прошло время.

---

## 5. Расходы и взаиморасчёты

### 5.1 Добавить расход

`POST /trips/{id}/expenses`

Поле `splitType` определяет способ разбивки:

| Значение | Режим | Описание |
|---|---|---|
| `0` | `Equal` (по умолчанию) | Поровну между всеми участниками `share` |
| `1` | `ByShares` | Пропорционально по весам (`weight`) |
| `2` | `ByAmounts` | Точные суммы на каждого (`amount`) |

**Равномерное деление** (`splitType: 0` или не передавать):
```json
{
  "title": "Такси из аэропорта",
  "amount": 1200.50,
  "payer": "u_...",
  "splitType": 0,
  "share": [
    { "participantId": "u_..." },
    { "participantId": "g_..." }
  ]
}
```

**По частям** (`splitType: 1`) — пропорциональное деление:
```json
{
  "title": "Аренда номера",
  "amount": 9000,
  "payer": "u_alice",
  "splitType": 1,
  "share": [
    { "participantId": "u_alice", "weight": 2 },
    { "participantId": "u_bob",   "weight": 1 }
  ]
}
```
Алиса платит 6000 (2/3), Боб — 3000 (1/3).

**По суммам** (`splitType: 2`) — точные суммы:
```json
{
  "title": "Ужин",
  "amount": 4700,
  "payer": "u_alice",
  "splitType": 2,
  "share": [
    { "participantId": "u_alice",   "amount": 2000 },
    { "participantId": "u_bob",     "amount": 1500 },
    { "participantId": "u_charlie", "amount": 1200 }
  ]
}
```
Сумма `amount` в `share` должна точно совпадать с полем `amount` (допуск ±0.01).

Правила валидации:

- `title` — непустой (`VALIDATION_ERROR`, field `title`).
- `amount` — строго `> 0` (`VALIDATION_ERROR`, field `amount`).
- `payer` — обязателен и должен быть участником поездки (`INVALID_PAYER`).
- `share` — минимум один участник, **все** должны быть участниками поездки
  (`INVALID_SHARE`). Дубликаты в `share` сервер схлопывает автоматически.
- `ByShares`: все `weight` обязательны и `> 0` (`VALIDATION_ERROR`, field `share`).
- `ByAmounts`: все `amount` обязательны и `> 0`, сумма должна равняться `amount` (`VALIDATION_ERROR`, field `share`).

> **Реквизиты для перевода** к расходу **не** привязываются. Куда переводить,
> определяется на этапе взаиморасчётов по реквизитам **получателя** (`toPayment` в
> `/settlements`, см. §5.3 и §11) — расходы одного человека сворачиваются в один
> перевод, поэтому реквизиты логически принадлежат участнику, а не расходу.

Ответ `{ "expense": ExpenseDto }`.

`ExpenseDto`:
```json
{
  "id": "...",
  "title": "Такси",
  "amount": 1200.50,
  "payer": "u_...",
  "splitType": 0,
  "share": [
    { "participantId": "u_...", "weight": null, "amount": null },
    { "participantId": "g_...", "weight": null, "amount": null }
  ],
  "createdBy": "u_...",
  "isTransfer": false
}
```

`isTransfer: true` — служебный расход-перевод, созданный при переоткрытии подсчёта
(§5.5); редактировать/удалять его нельзя (`409 TRANSFER_READONLY`).

### 5.2 Отредактировать расход

`PATCH /trips/{id}/expenses/{expenseId}`

Тело — как у `POST /trips/{id}/expenses` (§5.1): полная замена расхода, все поля
обязательны, действуют те же правила валидации. Частичного PATCH (null = не менять)
здесь нет — форма пересылается целиком.

Ответ `{ "expense": ExpenseDto, "warning": string | null }`.

`warning: "TRIP_HAS_PAID_TRANSFERS"` — в поездке уже есть оплаченные расходы-переводы
(был reopen после частичной оплаты, §5.5). Правка суммы/состава расхода может изменить
остаток чужого долга — уже переведённые деньги не теряются, но пересчитываются заново.
Это не ошибка (200 OK) — просто предупреждение для UI, покажите его пользователю перед
сохранением или в тосте после. Привязки к конкретному расходу нет: флаг говорит про
поездку в целом, а не про то, что именно этот расход уже был оплачен.

### 5.3 Удалить расход

`DELETE /trips/{id}/expenses/{expenseId}` → `{ "message": "Expense removed" }`.

### 5.4 Взаиморасчёты

`GET /trips/{id}/settlements`

```json
{
  "status": "active",
  "balances": {
    "u_anya": 800.50,
    "u_petya": -400.25,
    "g_kolya": -400.25
  },
  "transactions": [
    { "from": "u_petya", "to": "u_anya", "amount": 400.25, "toPayment": { "phone": "+79991234567", "banks": ["SBERBANK", "TBANK"], "label": "Основной" }, "id": null, "isPaid": null, "paidAt": null },
    { "from": "g_kolya", "to": "u_anya", "amount": 400.25, "toPayment": { "phone": "+79991234567", "banks": ["SBERBANK", "TBANK"], "label": "Основной" }, "id": null, "isPaid": null, "paidAt": null }
  ]
}
```

- `status` — стадия подсчёта поездки, та же что в trip-DTO (см. §5.5).
- `balances` — итоговый баланс каждого участника: **положительный** = ему должны,
  **отрицательный** = должен он.
- `transactions` — минимальный набор переводов «кто кому сколько», чтобы
  обнулить балансы (жадный алгоритм). `from` платит `to` сумму `amount`.
  Пока `status: "active"` это **предварительный** расчёт: он пересчитывается при
  каждом изменении расходов и состав переводов может полностью меняться —
  `id`/`isPaid`/`paidAt` всегда `null`. После финализации (§5.5) транзакции
  зафиксированы: у каждой есть постоянный `id`, флаг `isPaid` и время `paidAt`.
- `toPayment` — реквизиты **получателя** (`to`), куда переводить (`{ phone, banks[], label }`).
  Для юзера — его override для этой поездки, а если не задан — **дефолтный** способ оплаты
  из профиля (§11). Для гостя — его `paymentDetails`. `null`, если у получателя нет реквизитов.
  Резолвится живьём даже после финализации — если получатель добавит СБП позже,
  реквизиты появятся.

Клиент должен **не считать долги сам**, а брать готовый ответ этого эндпоинта —
алгоритм минимизации переводов и округление выполняются на сервере.

### 5.5 Финализация подсчёта и отметки об оплате

Жизненный цикл поездки: `active` → `settling` → `settled`.

- **`active`** — все накидывают расходы, `/settlements` показывает живой предварительный расчёт.
- **`settling`** — владелец завершил подсчёт: список переводов зафиксирован, участники
  переводят деньги и отмечают оплату. Мутации денег заблокированы (`409 TRIP_SETTLING`):
  добавление/редактирование/удаление расходов, добавление/удаление участников и гостей.
  События календаря, `PATCH /trips/{id}` и редактирование профиля гостя — разрешены.
- **`settled`** — все переводы отмечены оплаченными (проставляется автоматически;
  снятие отметки возвращает `settling`).

Все три эндпоинта возвращают полный `{ "settlements": SettlementsResponse }` (формат §5.4).

**`POST /trips/{id}/settlement`** — завершить подсчёт (только владелец, иначе `403`).
Фиксирует транзакции и переводит поездку в `settling` (или сразу в `settled`, если
переводить нечего). Повторный вызов — `409 ALREADY_FINALIZED`.

**`PATCH /trips/{id}/settlement/transactions/{txId}`** — отметить оплату:

```json
{ "paid": true }
```

Отметить (или снять отметку `"paid": false`) может любой из двух концов перевода;
если конец — гость, то любой участник поездки. Чужой перевод — `403`.
До финализации — `409 NOT_FINALIZED`, неизвестный `txId` — `404 TRANSACTION_NOT_FOUND`.

**`POST /trips/{id}/settlement/reopen`** — переоткрыть подсчёт (только владелец).
Возвращает поездку в `active`: неоплаченные транзакции удаляются, а **оплаченные
конвертируются в расходы-переводы** (`"isTransfer": true`, `title: "Перевод"`), чтобы уже
переведённые деньги остались учтёнными в новом пересчёте. Сценарий: забыли расход после
финализации → reopen → добавить расход → финализировать заново.

Расходы-переводы нельзя редактировать и удалять (`409 TRANSFER_READONLY`) — в UI
показывайте их отдельным стилем без кнопок правки.

---

## 6. События (расписание поездки)

### 6.1 Добавить событие

`POST /trips/{id}/events`

```json
{ "title": "Заезд в отель", "date": "2026-07-01", "time": "14:00", "endTime": "15:00" }
```

- `date` — `YYYY-MM-DD`, обязательно.
- `time`, `endTime` — `HH:mm`, опциональны (`null` = весь день / без времени).

Ответ `{ "event": TripEventDto }`.

### 6.2 Отредактировать событие

`PATCH /trips/{id}/events/{eventId}`

Тело — как у `POST /trips/{id}/events` (§6.1): полная замена события, все поля
пересылаются целиком (как в create), включая `time`/`endTime` (`null` = сбросить).

Ответ `{ "event": TripEventDto }`.

### 6.3 Удалить событие

`DELETE /trips/{id}/events/{eventId}` → `{ "message": "Event removed" }`.

### 6.4 Экспорт календаря (ICS)

`GET /trips/{id}/calendar.ics` — возвращает файл `text/calendar` (заголовок
`Content-Disposition: attachment`). Доступно только участникам поездки.
Можно отдать ссылку «Добавить в календарь». Запрос требует `Authorization`,
поэтому для прямого скачивания из браузера понадобится прокачать токен
(fetch + blob), а не просто `<a href>`.

---

## 7. Друзья

### 7.1 Поиск пользователя по хэндлу

`GET /users/search?handle=anya` (символ `@` можно опускать, регистр не важен).

- Найден: `{ "user": { "id", "name", "lastName", "firstName", "middleName", "handle", "email" } }`.
- Не найден: `404` `{ "error": { "code": "NOT_FOUND", ... } }`.

### 7.2 Списки

`GET /me/friends`:

```json
{
  "friends":  [ FriendDto, ... ],
  "incoming": [ FriendDto, ... ],
  "outgoing": [ FriendDto, ... ]
}
```

- `friends` — принятые;
- `incoming` — входящие запросы (вам прислали);
- `outgoing` — исходящие запросы (вы отправили).

`FriendDto` — та же форма, что и `user`:
`{ "id", "name", "lastName", "firstName", "middleName", "handle", "email" }`.

### 7.3 Отправить запрос

`POST /me/friends/requests` — нужно указать **либо** `handle`, **либо** `userId`:

```json
{ "handle": "petya" }
```

Особое поведение **auto-accept**: если адресат уже отправил вам встречный запрос,
ваш запрос моментально становится принятой дружбой (без перехода в `outgoing`).

Ошибки: `SELF_REQUEST` (422), `ALREADY_FRIENDS` (409), `REQUEST_EXISTS` (409),
`NOT_FOUND` (404), `VALIDATION_ERROR` (400, если не передан ни `handle`, ни `userId`).

### 7.4 Принять / отклонить / удалить

- `POST /me/friends/requests/{userId}/accept`
- `POST /me/friends/requests/{userId}/decline`
- `DELETE /me/friends/{userId}`

Здесь `{userId}` — id **другого** пользователя. Все возвращают `{ "message": "..." }`.
Принять можно только чужой запрос (попытка принять свой — `FORBIDDEN`).

---

## 8. Realtime через SignalR

Хаб: `/hubs/trip`. Используйте официальный клиент
`@microsoft/signalr`.

### 8.1 Подключение

JWT передаётся **query-параметром** `access_token` (не заголовком), т.к.
WebSocket-транспорт не поддерживает кастомные заголовки:

```js
import { HubConnectionBuilder, HttpTransportType } from "@microsoft/signalr";

const connection = new HubConnectionBuilder()
  .withUrl("http://localhost:5010/hubs/trip", {
    accessTokenFactory: () => currentAccessToken, // вернёт актуальный access-токен
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
```

`accessTokenFactory` библиотека сама положит в `?access_token=...`. Поскольку
access-токен живёт 15 минут, фабрика должна возвращать **свежий** токен (тот же,
что обновляет ваш HTTP-интерсептор).

### 8.2 Подписка на поездку

Хаб группирует подключения по `tripId`. Чтобы получать события конкретной поездки:

```js
await connection.invoke("JoinTrip", tripId);
// при уходе с экрана:
await connection.invoke("LeaveTrip", tripId);
```

После `withAutomaticReconnect` повторно вызывайте `JoinTrip` в обработчике
`connection.onreconnected(...)` — членство в группах после реконнекта теряется.

### 8.3 Серверные события

Подпишитесь до `start()`:

| Событие | Payload | Когда |
|---------|---------|-------|
| `trip:updated` | `TripDetail` (полный объект) | поездка изменена (PATCH, добавление участника/расхода) |
| `trip:deleted` | `{ tripId }` | поездка удалена |
| `expense:added` | `ExpenseDto` | добавлен расход |
| `expense:updated` | `ExpenseDto` | расход отредактирован |
| `expense:removed` | `{ expenseId }` | удалён расход |
| `member:added` | `{ id, name }` | добавлен участник |
| `participant:removed` | `{ participantId }` | удалён участник |
| `event:added` | `TripEventDto` | добавлено событие |
| `event:updated` | `TripEventDto` | событие отредактировано |
| `event:removed` | `{ eventId }` | удалено событие |
| `settlement:updated` | `{ tripId, settlements }` (полный `SettlementsResponse`, §5.4) | финализация / reopen / отметка об оплате |

```js
connection.on("expense:added", (expense) => { /* обновить UI */ });
connection.on("participant:removed", ({ participantId }) => {
  // обновить локальный список участников
});
```

> События — это «эхо» мутаций, выполняемых **другими** клиентами. Инициатор
> запроса и так получает результат в HTTP-ответе; используйте SignalR, чтобы
> синхронизировать остальные открытые сессии поездки.

---

## 9. Web Push (VAPID)

Позволяет присылать пуши (новый расход, запрос в друзья) даже когда вкладка
закрыта. Нужна регистрация Service Worker.

### 9.1 Получить публичный VAPID-ключ

`GET /notifications/vapid-public-key` → `{ "publicKey": "BI...." }`
(требует `Authorization`).

### 9.2 Оформить подписку в браузере

```js
const reg = await navigator.serviceWorker.ready;
const { publicKey } = await api.get("/notifications/vapid-public-key");

const sub = await reg.pushManager.subscribe({
  userVisibleOnly: true,
  applicationServerKey: urlBase64ToUint8Array(publicKey), // конвертация base64url → Uint8Array
});

const json = sub.toJSON(); // { endpoint, keys: { p256dh, auth } }
```

### 9.3 Отправить подписку на сервер

`POST /notifications/subscribe`

```json
{
  "endpoint": "https://fcm.googleapis.com/...",
  "p256dh": "BPx...",
  "auth": "k9x..."
}
```

> Из `sub.toJSON()` берите `endpoint`, `keys.p256dh` → `p256dh`,
> `keys.auth` → `auth`.

### 9.4 Отписаться

`DELETE /notifications/subscribe` с телом `{ "endpoint": "..." }`.

Вызывайте при логауте и при `pushsubscriptionchange`.

---

## 10. CORS и окружение

- Сервер отдаёт CORS только для origin'ов из конфигурации `Cors:AllowedOrigins`,
  с `AllowCredentials`. Ваш домен фронтенда должен быть туда добавлен (попросите
  бэкенд) — иначе браузер заблокирует запросы.
- В dev API слушает `http://localhost:5010`. В проде используйте HTTPS-URL
  (сервер делает `UseHttpsRedirection`).
- `GET /health` — проверка живости (включая доступность БД). Без авторизации.
  Удобно для health-пробы инфраструктуры, не для бизнес-логики клиента.

---

## 11. Способы оплаты и реквизиты (СБП)

Модель в три уровня:

1. **Способы оплаты в профиле** — глобальный список пользователя (номер + банки).
2. **Реквизиты юзера в поездке** — опциональный override поверх профиля (напр. «в этой
   поездке верните на Тинькофф»). Если не задан — берётся дефолтный способ из профиля.
3. **Реквизиты гостя** — хранятся прямо на госте (у гостя нет профиля), см. §4.2.

Реквизиты нужны получателю на этапе взаиморасчётов: в `/settlements` каждый перевод несёт
`toPayment` получателя (§5.3). К расходу реквизиты не привязываются.

### 11.1 Справочник банков

`GET /banks` (без авторизации) → `{ "banks": [ { "code": "SBERBANK", "name": "Сбербанк" }, ... ] }`.
Коды: `SBERBANK`, `TBANK`, `ALFABANK`, `VTB`. Рисуйте выбор банка из этого списка — при
добавлении новых банков фронт менять не нужно.

### 11.2 Способы оплаты в профиле

- `GET /me/payment-methods` → `{ "paymentMethods": [ PaymentMethodDto ] }`
- `POST /me/payment-methods` → `{ "paymentMethod": PaymentMethodDto }`
- `PATCH /me/payment-methods/{id}` → `{ "paymentMethod": PaymentMethodDto }`
- `DELETE /me/payment-methods/{id}` → `{ "message": "..." }`

`PaymentMethodDto`:
```json
{ "id": "pm_...", "phone": "+79991234567", "banks": ["SBERBANK", "TBANK"], "label": "Основной", "isDefault": true }
```

Тело `POST`:
```json
{ "phone": "89991234567", "banks": ["SBERBANK", "TBANK"], "label": "Основной", "isDefault": false }
```
`phone` нормализуется к `+7XXXXXXXXXX` (только RU, иначе `INVALID_PHONE`), `banks` — непустой
список кодов из `GET /banks` (иначе `INVALID_BANK`). `PATCH` — все поля опциональны (передавайте
только изменяемые). Первый добавленный способ автоматически становится дефолтным. При установке
`isDefault: true` флаг снимается с остальных; при удалении дефолтного — дефолтным становится любой
из оставшихся. Чужой/несуществующий `id` → `PAYMENT_METHOD_NOT_FOUND` (404).

### 11.3 Мои реквизиты в поездке (override)

- `GET /trips/{id}/me/payment` → эффективные реквизиты текущего юзера:
```json
{ "payment": { "phone": "+79991234567", "banks": ["TBANK"], "label": null }, "source": "trip" }
```
`source`: `"trip"` (задан override), `"profile"` (взято из дефолтного способа профиля),
`"none"` (реквизитов нет). `payment` = `null` при `"none"`.

- `PATCH /trips/{id}/me/payment` — задать/сбросить override для этой поездки:
```json
{ "payment": { "phone": "89991234567", "banks": ["TBANK"] } }
```
`payment: null` — **сбросить** override (реквизиты снова возьмутся из профиля). Ответ — та же
структура, что у `GET`. Так реализуется «выбрать из своих способов» (клиент подставляет `phone`/`banks`
выбранного способа) и «ввести локально».

---

## 12. Что нового (What's New)

Плашка «что нового» после обновления. Контент задаётся на бэкенде статически; фронт решает,
**показывать ли** плашку, сравнивая версию с сохранённой локально. Эндпоинт публичный.

`GET /whats-new?since={version}` (без авторизации):

```json
{
  "whatsNew": {
    "latestVersion": 3,
    "releases": [
      {
        "version": 3,
        "title": "Оплата по СБП",
        "date": "2026-07-07",
        "items": [
          "Реквизиты для перевода теперь можно указать в профиле",
          "В расчётах виден банк и телефон получателя"
        ]
      }
    ]
  }
}
```

- `latestVersion` — старшая доступная версия. Клиент сравнивает её со своей сохранённой.
- `releases` — отсортированы от новых к старым.
- `since` — версия, которую клиент уже видел; возвращаются только релизы **новее** неё.
  Без `since` отдаётся вся история изменений (для экрана «все обновления»).
- `date` — ISO-дата релиза (`YYYY-MM-DD`), опциональна для показа.

**Паттерн клиента:**

1. Храните последнюю показанную версию в `localStorage` (`whatsNewSeenVersion`).
2. **Первый запуск** (ключа ещё нет) — запишите текущую `latestVersion` и плашку **не** показывайте
   (новичку нечего сравнивать).
3. Иначе запросите `GET /whats-new?since={seen}`. Если `releases` непусто — покажите плашку
   (ненавязчивый bottom sheet / тост, не полноэкранный модал), затем запишите `latestVersion` в
   `localStorage`.
4. `items` — plain-текст, форматирование на клиенте (не рендерите как HTML).

---

## 13. Чек-лист интеграции

1. [ ] HTTP-клиент с авто-подстановкой `Bearer` и авто-`refresh` на `401`.
2. [ ] Единая обработка ошибок по `error.code` (а не по тексту).
3. [ ] `If-Match` при каждом `PATCH /trips/{id}` + обработка `VERSION_CONFLICT`.
4. [ ] Обработка `PARTICIPANT_HAS_EXPENSES` (409) при удалении участника — предложить сначала удалить/переназначить его расходы.
5. [ ] Суммы — decimal с 2 знаками; долги берём из `/settlements`, сами не считаем.
6. [ ] Реквизиты для перевода берём из `toPayment` в `/settlements`, не привязываем к расходу.
7. [ ] SignalR: `accessTokenFactory` со свежим токеном, `JoinTrip`/`LeaveTrip`,
       повторный `JoinTrip` после реконнекта.
8. [ ] Web Push: SW зарегистрирован, ключ получен, подписка отправлена/снимается.
9. [ ] Фронтенд-домен добавлен в `Cors:AllowedOrigins` на бэкенде.
