# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Tripkitty is an ASP.NET Core 10 Web API backend for a travel expense-splitting app. Clean Architecture with 4 projects. IDE is JetBrains Rider.

**Client API guide**: `docs/CLIENT_API_GUIDE.md` — полное руководство для фронтенда (auth/refresh, формат ошибок, If-Match, SignalR, Web Push). При изменении контрактов API обновляй этот документ.

## Solution Structure

```
Tripkitty.sln
├── Tripkitty.Domain/              # Entities, DomainException, repository interfaces
│   ├── Entities/                  # User, Trip, TripMember, Guest, Expense, TripEvent, Friendship, RefreshToken
│   └── Exceptions/DomainException.cs
├── Tripkitty.Application/         # Use-cases, services, DTOs
│   ├── DTOs/                      # Request/response records per domain
│   ├── Services/                  # AuthService, TripService, ParticipantService, ExpenseService, FriendService, EventService
│   └── Logic/SettlementsCalculator.cs
├── Tripkitty.Infrastructure/      # EF Core + PostgreSQL, JWT, BCrypt, ICS
│   ├── Data/                      # AppDbContext, UserRepository, TripRepository, FriendshipRepository
│   ├── Migrations/
│   ├── Services/                  # JwtService, PasswordHasher, IcsService
│   └── Extensions/ServiceCollectionExtensions.cs
└── Tripkitty.Api/                 # Minimal API endpoints, middleware, DI wiring
    ├── Endpoints/                 # AuthEndpoints, TripEndpoints, FriendEndpoints
    ├── Middleware/ExceptionMiddleware.cs
    └── Program.cs
```

**Project references**: Domain ← Application ← Infrastructure ← Api

**Central NuGet versions**: `Directory.Packages.props`. When adding a `<PackageReference>`, omit `Version` and add a `<PackageVersion>` entry there instead.

## Commands

All commands run from `Tripkitty/` (solution root).

```bash
# Build
dotnet build

# Run API (HTTP, port 5010)
dotnet run --project Tripkitty.Api

# Add EF migration (run from solution root)
dotnet ef migrations add <Name> --project Tripkitty.Infrastructure --startup-project Tripkitty.Api

# Apply migrations
dotnet ef database update --project Tripkitty.Infrastructure --startup-project Tripkitty.Api

# Docker build
docker build -f Tripkitty.Api/Dockerfile -t tripkitty-api .
```

OpenAPI docs at `/openapi/v1.json` in Development mode.

## Key Design Decisions

**Money**: stored as `AmountMinor` (long, kopeks/cents). Client sends decimal; server converts. Settlements return decimal rounded to 2 places.

**Friendship**: normalized so `UserAId < UserBId` alphabetically — one row per pair. `RequestedById` tracks who initiated. Auto-accept: if a counter-request exists when sending a friend request, it immediately becomes accepted.

**Participant removal cascade** (atomic, single EF transaction):
1. Remove from `members` or `guests`
2. Delete all expenses where `Payer == participantId`
3. Remove participantId from all `share` arrays
4. Delete expenses where `share` is now empty

**Optimistic concurrency on Trip**: `version` increments on every mutation. `PATCH /trips/{id}` requires `If-Match: <version>` header; returns `409` on mismatch.

**ID prefixes**: `u_*` (users), `g_*` (guests), plain UUID for trips/expenses/events.

**Response envelope**: эндпоинты возвращают payload завёрнутым в именованный объект — `{ trip }`, `{ trips }`, `{ user }`, `{ member }`, `{ guest }`, `{ expense }`, `{ event }`. Исключения: `/settlements` (голый объект `{ balances, transactions }`) и сообщения `{ message }`. Событие в JSON-ключе — `@event` (C# escape), на проводе `event`.

**Error format**:
```json
{ "error": { "code": "HANDLE_TAKEN", "message": "Логин @anya уже занят", "field": "handle" } }
```
`ExceptionMiddleware` maps `DomainException` codes to HTTP status (400/403/404/409/422).

**Expense.Share** stored as JSONB column in PostgreSQL.

**JWT**: access token 15 min, refresh token 30 days (stored hashed with SHA-256 in `RefreshTokens` table).

**Trip entity**: поле называется `Name`, не `Title`.

**Repository interfaces** определены прямо в файле сервиса, который их использует (например, `IFriendshipRepository` — в `ParticipantService.cs`).

**Realtime (SignalR)**: реализован. Хаб на `/hubs/trip`. Клиент вызывает `JoinTrip(tripId)` / `LeaveTrip(tripId)`. Сервер шлёт: `trip:updated`, `trip:deleted`, `expense:added`, `expense:removed`, `member:added`, `participant:removed`, `event:added`, `event:removed`. JWT передаётся как query-param `?access_token=`.

**ITripNotifier**: интерфейс в Application, реализация `SignalRTripNotifier` в `Tripkitty.Api/Hubs/`. Регистрируется в `Program.cs` (не в `ServiceCollectionExtensions`), т.к. зависит от `TripHub` из Api-проекта.

**Web Push (VAPID)**: реализован. Ключи уже в `appsettings.json`. При перегенерации — `VapidHelper.GenerateVapidKeys()` из пакета `WebPush`. `WebPushService` и `PushSubscriptionRepository` в Infrastructure. Подписка через `POST /notifications/subscribe`.

**CORS**: настроен в `ServiceCollectionExtensions`. Allowed origins берутся из `Cors:AllowedOrigins` в конфиге. `UseCors()` должен стоять до `UseAuthentication()` в `Program.cs`.

**Health Check**: `GET /health` — встроенный ASP.NET Core health check. `DbHealthCheck` (Infrastructure/Data/) проверяет `db.Database.CanConnectAsync()`. Зарегистрирован в `ServiceCollectionExtensions`, эндпоинт маппится в `Program.cs`.

## Adding a New Feature

1. Add entity to `Tripkitty.Domain/Entities/`
2. Add `DbSet` + EF configuration in `AppDbContext`
3. Add DTOs to `Tripkitty.Application/DTOs/`
4. Add service interface + implementation in `Tripkitty.Application/Services/`
5. Add repository in `Tripkitty.Infrastructure/Data/` if needed; register in `ServiceCollectionExtensions`
6. Add endpoint group in `Tripkitty.Api/Endpoints/` and call `.Map*` in `Program.cs`
7. Create EF migration

## Testing

**Test project**: `Tripkitty.Tests` (xUnit + NSubstitute). Run: `dotnet test Tripkitty.Tests`

**CPM gotcha**: шаблон `dotnet new xunit` генерирует `Version` прямо в `<PackageReference>` — это ломает Central Package Management. Нужно убрать `Version` из `.csproj` и добавить `<PackageVersion>` в `Directory.Packages.props`.

**Mocking**: NSubstitute. Все репозиторные и сервисные интерфейсы (`IUserRepository`, `IFriendshipRepository`, `ITripRepository`, `IPushNotificationService`, `ITripNotifier`) готовы к подстановке.

## Configuration

`appsettings.json` requires:
- `ConnectionStrings:Default` — PostgreSQL connection string
- `Jwt:Key` (≥ 32 chars), `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenExpiryMinutes`, `Jwt:RefreshTokenExpiryDays`
- `WebPush:Subject`, `WebPush:PublicKey`, `WebPush:PrivateKey` — VAPID ключи для Web Push
- `Cors:AllowedOrigins` — массив разрешённых origins для PWA-клиента
