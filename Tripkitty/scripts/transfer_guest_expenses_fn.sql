-- Функция для переноса расходов гостя на зарегистрированного пользователя.
-- Повторяет логику transfer_guest_expenses.sql, но вызывается одной строкой из DataGrip:
--
--   SELECT transfer_guest_expenses('g_xxx...', 'u_yyy...');
--
-- Создать функцию (один раз):
--   выполнить этот файл целиком в DataGrip или через psql -f
--
-- Удалить функцию, когда больше не нужна:
--   DROP FUNCTION transfer_guest_expenses(text, text);

CREATE OR REPLACE FUNCTION transfer_guest_expenses(
    p_guest_id text,
    p_user_id  text
) RETURNS text
LANGUAGE plpgsql AS
$$
DECLARE
    v_trip_id     text;
    v_user_handle text;
    v_refs_left   int;
BEGIN
    -- Валидация: гость должен существовать
    SELECT "TripId" INTO v_trip_id
    FROM "Guests" WHERE "Id" = p_guest_id;

    IF v_trip_id IS NULL THEN
        RAISE EXCEPTION 'Гость % не найден', p_guest_id;
    END IF;

    -- Валидация: пользователь должен существовать
    SELECT "Handle" INTO v_user_handle
    FROM "Users" WHERE "Id" = p_user_id;

    IF v_user_handle IS NULL THEN
        RAISE EXCEPTION 'Пользователь % не найден', p_user_id;
    END IF;

    -- 1. Гость как плательщик
    UPDATE "Expenses"
    SET "Payer" = p_user_id
    WHERE "TripId" = v_trip_id AND "Payer" = p_guest_id;

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
            SELECT CASE WHEN elem->>'ParticipantId' = p_guest_id THEN p_user_id
                        ELSE elem->>'ParticipantId' END AS pid,
                   SUM((elem->>'Weight')::int)          AS weight,
                   SUM((elem->>'AmountMinor')::bigint)  AS amount
            FROM jsonb_array_elements(e2."Share") AS elem
            GROUP BY 1
        ) s
        WHERE e2."TripId" = v_trip_id
          AND e2."Share" @> jsonb_build_array(jsonb_build_object('ParticipantId', p_guest_id))
        GROUP BY e2."Id"
    ) m
    WHERE e."Id" = m.expense_id;

    -- 3. Пользователь должен быть участником поездки
    INSERT INTO "TripMembers" ("TripId", "UserId", "CalendarToken")
    VALUES (v_trip_id, p_user_id, replace(gen_random_uuid()::text, '-', ''))
    ON CONFLICT DO NOTHING;

    -- 4. Версия поездки
    UPDATE "Trips" SET "Version" = "Version" + 1 WHERE "Id" = v_trip_id;

    -- Проверка: ссылок на гостя не должно остаться
    SELECT count(*) INTO v_refs_left
    FROM "Expenses"
    WHERE "TripId" = v_trip_id
      AND ("Payer" = p_guest_id
           OR "Share" @> jsonb_build_array(jsonb_build_object('ParticipantId', p_guest_id)));

    IF v_refs_left > 0 THEN
        RAISE EXCEPTION 'После переноса осталось % ссылок на гостя — откат', v_refs_left;
    END IF;

    RETURN format(
        'Готово. Гость %s → @%s (поездка %s). Ссылок на гостя: 0. '
        'Можно удалить через API.',
        p_guest_id, v_user_handle, v_trip_id
    );
END;
$$;
