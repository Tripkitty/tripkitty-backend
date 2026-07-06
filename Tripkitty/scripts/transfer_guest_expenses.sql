-- Переносит все расходы гостя (g_*) на зарегистрированного пользователя (u_*).
-- Повторяет логику каскада ParticipantService, но вместо удаления — перенос:
--   1. Payer: гость -> пользователь
--   2. Share (JSONB): гость -> пользователь; если пользователь уже был в share,
--      записи объединяются (Weight и AmountMinor суммируются)
--   3. Пользователь добавляется в участники поездки, если его там нет
--   4. Trip.Version инкрементируется (optimistic concurrency)
-- Гостя НЕ удаляет — после переноса его можно безопасно удалить через API,
-- каскад ничего не заденет (ссылок на гостя в расходах не останется).
--
-- Запуск (локально):
--   docker exec -i tripkitty-postgres-1 psql -U postgres -d tripkitty \
--     -v guest_id=g_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx \
--     -v user_id=u_yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy \
--     -f - < scripts/transfer_guest_expenses.sql

\set ON_ERROR_STOP on

-- \gset упадёт, если гость или пользователь не найдены — это и есть защита
SELECT "TripId" AS trip_id FROM "Guests" WHERE "Id" = :'guest_id' \gset
SELECT "Handle" AS user_handle FROM "Users" WHERE "Id" = :'user_id' \gset

\echo Перенос расходов гостя :guest_id -> @:user_handle (поездка :trip_id)

BEGIN;

-- 1. Гость как плательщик
UPDATE "Expenses"
SET "Payer" = :'user_id'
WHERE "TripId" = :'trip_id' AND "Payer" = :'guest_id';

-- 2. Гость в share: заменить id и схлопнуть дубликаты
--    (SUM по null-значениям даёт null — Equal-сплиты не портятся)
UPDATE "Expenses" e
SET "Share" = m.new_share
FROM (
    SELECT e2."Id" AS expense_id,
           jsonb_agg(jsonb_build_object(
               'ParticipantId', s.pid,
               'Weight',        s.weight,
               'AmountMinor',   s.amount)) AS new_share
    FROM "Expenses" e2
    CROSS JOIN LATERAL (
        SELECT CASE WHEN elem->>'ParticipantId' = :'guest_id' THEN :'user_id'
                    ELSE elem->>'ParticipantId' END AS pid,
               SUM((elem->>'Weight')::int)          AS weight,
               SUM((elem->>'AmountMinor')::bigint)  AS amount
        FROM jsonb_array_elements(e2."Share") AS elem
        GROUP BY 1
    ) s
    WHERE e2."TripId" = :'trip_id'
      AND e2."Share" @> jsonb_build_array(jsonb_build_object('ParticipantId', :'guest_id'))
    GROUP BY e2."Id"
) m
WHERE e."Id" = m.expense_id;

-- 3. Пользователь должен быть участником поездки, иначе расходы повиснут
INSERT INTO "TripMembers" ("TripId", "UserId", "CalendarToken")
VALUES (:'trip_id', :'user_id', replace(gen_random_uuid()::text, '-', ''))
ON CONFLICT DO NOTHING;

-- 4. Версия поездки
UPDATE "Trips" SET "Version" = "Version" + 1 WHERE "Id" = :'trip_id';

COMMIT;

\echo Готово. Ссылок на гостя в расходах (должно быть 0):
SELECT count(*) AS refs_left FROM "Expenses"
WHERE "TripId" = :'trip_id'
  AND ("Payer" = :'guest_id'
       OR "Share" @> jsonb_build_array(jsonb_build_object('ParticipantId', :'guest_id')));
