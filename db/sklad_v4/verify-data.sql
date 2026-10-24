/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА Sklad-v4: проверка данных
   ----------------------------------------------------------------------------
   Что проверяет:
     1. состав базы — сколько строк в каждой таблице;
     2. контрольные суммы — приход, расход, сальдо, деньги — против Sklad-v3
        и против эталона Sh_29-06-25 (ядро в v4 не менялось, суммы должны
        совпасть до копейки);
     3. сироты по внешним ключам — номенклатура, склады, контрагенты, ревизии;
     4. нарушения CHECK-ограничений;
     5. целостность системы характеристик: значения не соответствуют типу,
        умолчание типа List отсутствует в списке значений, дубли значений;
     6. битые ссылки на кадровую базу Sklad_v1_HR (СотрудникGuid).

   Все проверки возвращают ПУСТОЙ результат — значит всё в порядке.
   Скрипт ничего не меняет.
   ========================================================================== */

USE [Sklad-v4];
GO

SET NOCOUNT ON;
GO

/* ===========================================================================
   1. СОСТАВ БАЗЫ
   =========================================================================== */
PRINT N'=== 1. Состав базы ===';
SELECT N'Склад'              AS [Таблица], COUNT(*) AS [Строк] FROM dbo.Склад
UNION ALL SELECT N'ЕдИзмерения',    COUNT(*) FROM dbo.ЕдИзмерения
UNION ALL SELECT N'ВидНоменклатуры',COUNT(*) FROM dbo.ВидНоменклатуры
UNION ALL SELECT N'Категория',      COUNT(*) FROM dbo.КатегорияНоменклатуры
UNION ALL SELECT N'Группа',         COUNT(*) FROM dbo.ГруппаНоменклатуры
UNION ALL SELECT N'ТипЗначения',    COUNT(*) FROM dbo.ТипЗначения
UNION ALL SELECT N'Характеристика', COUNT(*) FROM dbo.Характеристика
UNION ALL SELECT N'СписокЗначений', COUNT(*) FROM dbo.СписокЗначений
UNION ALL SELECT N'Номенклатура',   COUNT(*) FROM dbo.Номенклатура
UNION ALL SELECT N'ЗначениеХар-ки', COUNT(*) FROM dbo.ЗначениеХарактеристики
UNION ALL SELECT N'Клиент',         COUNT(*) FROM dbo.Клиент
UNION ALL SELECT N'Поставщик',      COUNT(*) FROM dbo.Поставщик
UNION ALL SELECT N'Приход',         COUNT(*) FROM dbo.Приход
UNION ALL SELECT N'Расход',         COUNT(*) FROM dbo.Расход
UNION ALL SELECT N'Деньги',         COUNT(*) FROM dbo.Деньги
UNION ALL SELECT N'Ревизия',        COUNT(*) FROM dbo.Ревизия
UNION ALL SELECT N'Расхождение',    COUNT(*) FROM dbo.Расхождение
ORDER BY [Таблица];
GO

/* ===========================================================================
   2. КОНТРОЛЬНЫЕ СУММЫ: v4 против v3 и эталона
   =========================================================================== */
PRINT N'=== 2. Контрольные суммы (v4 | v3 | эталон) ===';
SELECT N'Приход: количество' AS [Показатель],
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM dbo.Приход) AS [Sklad-v4],
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM [Sklad-v3].dbo.Приход) AS [Sklad-v3],
       (SELECT CAST(SUM(CAST([колОснЕдИзм] AS decimal(18,3))) AS decimal(18,3))
        FROM [Sh_29-06-25].dbo.[СкладПриход]) AS [Эталон]
UNION ALL
SELECT N'Расход: количество',
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM dbo.Расход),
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM [Sklad-v3].dbo.Расход),
       (SELECT CAST(SUM(CAST([колОснЕдИзм] AS decimal(18,3))) AS decimal(18,3))
        FROM [Sh_29-06-25].dbo.[СкладРасход])
UNION ALL
SELECT N'Сальдо',
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM dbo.Приход)
     - (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM dbo.Расход),
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM [Sklad-v3].dbo.Приход)
     - (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM [Sklad-v3].dbo.Расход),
       (SELECT CAST(SUM(CAST([колОснЕдИзм] AS decimal(18,3))) AS decimal(18,3))
        FROM [Sh_29-06-25].dbo.[СкладПриход])
     - (SELECT CAST(SUM(CAST([колОснЕдИзм] AS decimal(18,3))) AS decimal(18,3))
        FROM [Sh_29-06-25].dbo.[СкладРасход])
UNION ALL
SELECT N'Деньги: сумма',
       (SELECT CAST(SUM(Sum) AS decimal(18,2)) FROM dbo.Деньги),
       (SELECT CAST(SUM(Sum) AS decimal(18,2)) FROM [Sklad-v3].dbo.Деньги),
       (SELECT CAST(SUM(CAST([cуммаПринятыхДенег] AS decimal(18,2))) AS decimal(18,2))
        FROM [Sh_29-06-25].dbo.[КлиентДеньги])
UNION ALL
SELECT N'Номенклатура: позиций',
       (SELECT COUNT(*) FROM dbo.Номенклатура),
       (SELECT COUNT(*) FROM [Sklad-v3].dbo.Номенклатура),
       (SELECT COUNT(*) FROM [Sh_29-06-25].dbo.[Номенклатура])
UNION ALL
SELECT N'Клиенты',
       (SELECT COUNT(*) FROM dbo.Клиент),
       (SELECT COUNT(*) FROM [Sklad-v3].dbo.Клиент),
       (SELECT COUNT(*) FROM [Sh_29-06-25].dbo.[Клиент]);
GO

/* ===========================================================================
   3. СИРОТЫ ПО ВНЕШНИМ КЛЮЧАМ (должно быть пусто)
   =========================================================================== */
PRINT N'=== 3. Сироты по внешним ключам (пусто = в порядке) ===';
SELECT N'Приход' AS [Таблица], p.Id
FROM dbo.Приход AS p
LEFT JOIN dbo.Номенклатура AS н ON н.Id = p.НоменклатураId
LEFT JOIN dbo.Поставщик AS с ON с.Id = p.ПоставщикId
LEFT JOIN dbo.Склад AS к ON к.Id = p.СкладId
WHERE н.Id IS NULL OR с.Id IS NULL OR к.Id IS NULL
UNION ALL
SELECT N'Расход', r.Id
FROM dbo.Расход AS r
LEFT JOIN dbo.Номенклатура AS н ON н.Id = r.НоменклатураId
LEFT JOIN dbo.Клиент AS к ON к.Id = r.КлиентId
LEFT JOIN dbo.Склад AS с ON с.Id = r.СкладId
WHERE н.Id IS NULL OR к.Id IS NULL OR с.Id IS NULL
UNION ALL
SELECT N'ЗначениеХарактеристики', z.Id
FROM dbo.ЗначениеХарактеристики AS z
LEFT JOIN dbo.Номенклатура AS н ON н.Id = z.НоменклатураId
LEFT JOIN dbo.Характеристика AS х ON х.Id = z.ХарактеристикаId
WHERE н.Id IS NULL OR х.Id IS NULL
UNION ALL
SELECT N'Характеристика', х.Id
FROM dbo.Характеристика AS х
LEFT JOIN dbo.ВидНоменклатуры AS в ON в.Id = х.ВидНоменклатурыId
LEFT JOIN dbo.ТипЗначения AS т ON т.Id = х.ТипЗначенияId
WHERE в.Id IS NULL OR т.Id IS NULL
UNION ALL
SELECT N'Расхождение', d.Id
FROM dbo.Расхождение AS d
LEFT JOIN dbo.Ревизия AS р ON р.Code = d.КодРевизии
WHERE р.Code IS NULL;
GO

/* ===========================================================================
   4. НАРУШЕНИЯ CHECK-ОГРАНИЧЕНИЙ (должно быть пусто)
   =========================================================================== */
PRINT N'=== 4. Нарушения CHECK-ограничений (пусто = в порядке) ===';
SELECT N'Приход' AS [Таблица], Id
FROM dbo.Приход WHERE Quantity <= 0 OR Price < 0 OR Sum < 0
UNION ALL
SELECT N'Расход', Id
FROM dbo.Расход WHERE Quantity <= 0 OR Price < 0 OR Sum < 0
UNION ALL
SELECT N'Деньги', Id
FROM dbo.Деньги WHERE Sum < 0
UNION ALL
SELECT N'Номенклатура', Id
FROM dbo.Номенклатура
WHERE ТипId NOT IN (1, 2, 3)
   OR ISNULL(ЦенаЗакупочная, 0) < 0
   OR ISNULL(ЦенаПродажная, 0) < 0
   OR ISNULL(ЦенаОптовая, 0) < 0
   OR ISNULL(СрокГодности, 0) < 0
   OR (МинимальныйОстаток IS NOT NULL AND МаксимальныйОстаток IS NOT NULL
       AND МаксимальныйОстаток < МинимальныйОстаток)
UNION ALL
SELECT N'Характеристика', Id
FROM dbo.Характеристика
WHERE (Минимум IS NOT NULL AND Максимум IS NOT NULL AND Максимум < Минимум)
   OR (МинимальнаяДата IS NOT NULL AND МаксимальнаяДата IS NOT NULL
       AND МаксимальнаяДата < МинимальнаяДата)
   OR ISNULL(МаксимальнаяДлина, 1) <= 0;
GO

/* ===========================================================================
   5. ЦЕЛОСТНОСТЬ СИСТЕМЫ ХАРАКТЕРИСТИК (должно быть пусто)
   =========================================================================== */
PRINT N'=== 5. Значения не соответствуют типу (пусто = в порядке) ===';
SELECT z.Id, н.[Код] AS [Номенклатура], х.[Название] AS [Характеристика],
       т.[Название] AS [Тип], z.[Значение]
FROM dbo.ЗначениеХарактеристики AS z
INNER JOIN dbo.Номенклатура AS н ON н.Id = z.НоменклатураId
INNER JOIN dbo.Характеристика AS х ON х.Id = z.ХарактеристикаId
INNER JOIN dbo.ТипЗначения AS т ON т.Id = х.ТипЗначенияId
LEFT JOIN dbo.СписокЗначений AS с ON с.ХарактеристикаId = х.Id AND с.Значение = z.Значение
WHERE (т.[Название] = N'Number' AND TRY_CONVERT(decimal(18,3), z.[Значение]) IS NULL)
   OR (т.[Название] = N'Number' AND х.Минимум IS NOT NULL
       AND TRY_CONVERT(decimal(18,3), z.[Значение]) < х.Минимум)
   OR (т.[Название] = N'Number' AND х.Максимум IS NOT NULL
       AND TRY_CONVERT(decimal(18,3), z.[Значение]) > х.Максимум)
   OR (т.[Название] = N'Date' AND TRY_CONVERT(date, z.[Значение]) IS NULL)
   OR (т.[Название] = N'Date' AND х.МинимальнаяДата IS NOT NULL
       AND TRY_CONVERT(date, z.[Значение]) < х.МинимальнаяДата)
   OR (т.[Название] = N'Date' AND х.МаксимальнаяДата IS NOT NULL
       AND TRY_CONVERT(date, z.[Значение]) > х.МаксимальнаяДата)
   OR (т.[Название] = N'Text' AND х.МаксимальнаяДлина IS NOT NULL
       AND LEN(z.[Значение]) > х.МаксимальнаяДлина)
   OR (т.[Название] = N'Bool'
       AND LOWER(LTRIM(RTRIM(z.[Значение]))) NOT IN (N'0', N'1', N'true', N'false', N'да', N'нет'))
   OR (т.[Название] = N'List' AND с.Id IS NULL);
GO

PRINT N'=== 5.1. Умолчание типа List отсутствует в списке значений (пусто = в порядке) ===';
SELECT х.Id, в.[Название] AS [Вид], х.[Название] AS [Характеристика], х.[ЗначениеПоУмолчанию]
FROM dbo.Характеристика AS х
INNER JOIN dbo.ВидНоменклатуры AS в ON в.Id = х.ВидНоменклатурыId
INNER JOIN dbo.ТипЗначения AS т ON т.Id = х.ТипЗначенияId
WHERE т.[Название] = N'List'
  AND х.[ЗначениеПоУмолчанию] IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.СписокЗначений AS с
                  WHERE с.ХарактеристикаId = х.Id AND с.Значение = х.ЗначениеПоУмолчанию);
GO

PRINT N'=== 5.2. Дубли значений в списке (пусто = в порядке) ===';
SELECT ХарактеристикаId, Значение, COUNT(*) AS [Повторов]
FROM dbo.СписокЗначений
GROUP BY ХарактеристикаId, Значение
HAVING COUNT(*) > 1;
GO

PRINT N'=== 5.3. Позиции без характеристик своего вида (информация) ===';
SELECT в.[Название] AS [Вид],
       COUNT(*) AS [Позиций],
       COUNT(з.[НоменклатураId]) AS [С заполненными значениями]
FROM dbo.Номенклатура AS н
INNER JOIN dbo.ВидНоменклатуры AS в ON в.Id = н.ВидНоменклатурыId
LEFT JOIN (SELECT DISTINCT НоменклатураId FROM dbo.ЗначениеХарактеристики) AS з
       ON з.НоменклатураId = н.Id
GROUP BY в.[Название]
ORDER BY в.[Название];
GO

/* ===========================================================================
   6. ССЫЛКИ НА КАДРОВУЮ БАЗУ (пусто = в порядке)
   =========================================================================== */
PRINT N'=== 6. Битые ссылки на Sklad_v1_HR (пусто = в порядке) ===';
SELECT т.Таблица, т.Id, т.СотрудникGuid
FROM (
    SELECT N'Приход' AS Таблица, Id, СотрудникGuid FROM dbo.Приход WHERE СотрудникGuid IS NOT NULL
    UNION ALL SELECT N'Расход',  Id, СотрудникGuid FROM dbo.Расход  WHERE СотрудникGuid IS NOT NULL
    UNION ALL SELECT N'Деньги',  Id, СотрудникGuid FROM dbo.Деньги  WHERE СотрудникGuid IS NOT NULL) AS т
WHERE NOT EXISTS (SELECT 1 FROM [Sklad_v1_HR].dbo.Сотрудник AS с WHERE с.[Guid] = т.СотрудникGuid);
GO

/* ===========================================================================
   7. UETR: пустые значения и полные дубли строк (должно быть пусто)
   =========================================================================== */
PRINT N'=== 7. Пустой UETR (пусто = в порядке) ===';
SELECT N'Приход' AS [Таблица], Id, UETR FROM dbo.Приход WHERE UETR IS NULL
UNION ALL SELECT N'Расход',  Id, UETR FROM dbo.Расход  WHERE UETR IS NULL
UNION ALL SELECT N'Деньги',  Id, UETR FROM dbo.Деньги  WHERE UETR IS NULL;
GO

PRINT N'=== 7.1. Полные дубли строк движений ===';
PRINT N'    В эталоне есть две пары одинаковых строк в одной накладной';
PRINT N'    (НакладнаяРасход 11: номенклатура 129, 3 шт по 250;';
PRINT N'     НакладнаяРасход 113: номенклатура 128, 4.06 шт по 200).';
PRINT N'    Это свойство исходных данных, а не дефект переноса:';
PRINT N'    в Sklad-v3 те же строки, суммы сходятся с эталоном до копейки.';
SELECT N'Приход' AS [Таблица], UETR, НоменклатураId, СкладId, Quantity, Price, COUNT(*) AS [Повторов]
FROM dbo.Приход
GROUP BY UETR, НоменклатураId, СкладId, Quantity, Price
HAVING COUNT(*) > 1
UNION ALL
SELECT N'Расход', UETR, НоменклатураId, СкладId, Quantity, Price, COUNT(*)
FROM dbo.Расход
GROUP BY UETR, НоменклатураId, СкладId, Quantity, Price
HAVING COUNT(*) > 1;
GO

PRINT N'Проверка Sklad-v4 завершена';
GO
