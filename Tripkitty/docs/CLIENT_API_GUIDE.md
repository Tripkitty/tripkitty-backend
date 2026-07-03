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
  "name": "Аня",
  "handle": "anya",
  "email": "anya@example.com",
  "password": "secret123"
}
```

Ответ `200`:

```json
{
  "user": { "id": "u_...", "name": "Аня", "handle": "anya", "email": "anya@example.com" },
  "tokens": { "accessToken": "eyJ...", "refreshToken": "..." }
}
```

Возможные ошибки: `HANDLE_TAKEN` (409), `EMAIL_TAKEN` (409), `VALIDATION_ERROR` (400).

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
{ "user": { "id": "u_...", "name": "Аня", "handle": "anya", "email": "..." } }
```

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
    "field": "handle"
  }
}
```

- `code` — машиночитаемый код (используйте его в логике, не текст `message`).
- `message` — человекочитаемый текст (можно показывать пользователю).
- `field` — имя поля формы, к которому относится ошибка (может быть `null`).

### Карта кодов → HTTP-статусов

| HTTP | Коды |
|------|------|
| `400 Bad Request` | `VALIDATION_ERROR` |
| `403 Forbidden` | `FORBIDDEN` |
| `404 Not Found` | `NOT_FOUND` |
| `409 Conflict` | `HANDLE_TAKEN`, `EMAIL_TAKEN`, `ALREADY_FRIENDS`, `REQUEST_EXISTS`, `ALREADY_MEMBER` |
| `422 Unprocessable Entity` | `SELF_REQUEST`, `INVALID_PAYER`, `INVALID_SHARE`, `USER_NOT_FOUND`, `WRONG_PASSWORD`, `INVALID_TOKEN`, `VERSION_CONFLICT` |
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
  "version": 4
}
```

`start` / `end` — даты в формате `YYYY-MM-DD` (могут быть `null`).
`version` нужен для оптимистичной блокировки (см. 3.4).

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
  "members": [ { "id": "u_...", "name": "Аня", "handle": "anya", "email": "..." } ],
  "guests":  [ { "id": "g_...", "name": "Петя" } ],
  "expenses":[ { "id": "...", "title": "Такси", "amount": 1200.50, "payer": "u_...", "share": [{"participantId":"u_..."},{"participantId":"g_..."}], "splitType": 0, "createdBy": "u_..." } ],
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

- `POST /trips/{id}/clear` — очищает содержимое поездки (расходы/события), сохраняя её.
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
{ "name": "Петя" }
```

Ответ `{ "guest": { "id": "g_...", "name": "Петя" } }`.

### 4.3 Удалить участника (каскад!)

`DELETE /trips/{id}/participants/{participantId}`

Удаление участника **атомарно** влечёт за собой:

1. Удаление участника из `members` / `guests`.
2. Удаление всех расходов, где этот участник — плательщик (`payer`).
3. Удаление участника из всех массивов `share`.
4. Удаление расходов, у которых `share` стал пустым.

Поэтому после удаления участника клиент **обязан перезапросить детали поездки**
(`GET /trips/{id}`) — локально пересчитать состояние нельзя, изменения затрагивают
расходы. То же касается списка взаиморасчётов.

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
  "createdBy": "u_..."
}
```

### 5.2 Удалить расход

`DELETE /trips/{id}/expenses/{expenseId}` → `{ "message": "Expense removed" }`.

### 5.3 Взаиморасчёты

`GET /trips/{id}/settlements`

```json
{
  "balances": {
    "u_anya": 800.50,
    "u_petya": -400.25,
    "g_kolya": -400.25
  },
  "transactions": [
    { "from": "u_petya", "to": "u_anya", "amount": 400.25 },
    { "from": "g_kolya", "to": "u_anya", "amount": 400.25 }
  ]
}
```

- `balances` — итоговый баланс каждого участника: **положительный** = ему должны,
  **отрицательный** = должен он.
- `transactions` — минимальный набор переводов «кто кому сколько», чтобы
  обнулить балансы (жадный алгоритм). `from` платит `to` сумму `amount`.

Клиент должен **не считать долги сам**, а брать готовый ответ этого эндпоинта —
алгоритм минимизации переводов и округление выполняются на сервере.

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

### 6.2 Удалить событие

`DELETE /trips/{id}/events/{eventId}` → `{ "message": "Event removed" }`.

### 6.3 Экспорт календаря (ICS)

`GET /trips/{id}/calendar.ics` — возвращает файл `text/calendar` (заголовок
`Content-Disposition: attachment`). Доступно только участникам поездки.
Можно отдать ссылку «Добавить в календарь». Запрос требует `Authorization`,
поэтому для прямого скачивания из браузера понадобится прокачать токен
(fetch + blob), а не просто `<a href>`.

---

## 7. Друзья

### 7.1 Поиск пользователя по хэндлу

`GET /users/search?handle=anya` (символ `@` можно опускать, регистр не важен).

- Найден: `{ "user": { "id", "name", "handle", "email" } }`.
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
| `expense:removed` | `{ expenseId }` | удалён расход |
| `member:added` | `{ id, name }` | добавлен участник |
| `participant:removed` | `{ participantId }` | удалён участник (помните про каскад — лучше перезапросить детали) |
| `event:added` | `TripEventDto` | добавлено событие |
| `event:removed` | `{ eventId }` | удалено событие |

```js
connection.on("expense:added", (expense) => { /* обновить UI */ });
connection.on("participant:removed", ({ participantId }) => {
  // каскадно могли пропасть расходы — перезапросите GET /trips/{id}
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

## 11. Чек-лист интеграции

1. [ ] HTTP-клиент с авто-подстановкой `Bearer` и авто-`refresh` на `401`.
2. [ ] Единая обработка ошибок по `error.code` (а не по тексту).
3. [ ] `If-Match` при каждом `PATCH /trips/{id}` + обработка `VERSION_CONFLICT`.
4. [ ] После `participant:removed` / удаления участника — перезапрос деталей поездки.
5. [ ] Суммы — decimal с 2 знаками; долги берём из `/settlements`, сами не считаем.
6. [ ] SignalR: `accessTokenFactory` со свежим токеном, `JoinTrip`/`LeaveTrip`,
       повторный `JoinTrip` после реконнекта.
7. [ ] Web Push: SW зарегистрирован, ключ получен, подписка отправлена/снимается.
8. [ ] Фронтенд-домен добавлен в `Cors:AllowedOrigins` на бэкенде.
