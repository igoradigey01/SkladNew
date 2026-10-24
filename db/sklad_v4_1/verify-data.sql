/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА Sklad-v4.1: проверка данных
   ----------------------------------------------------------------------------
   Что проверяет:
     1. состав базы — сколько строк в каждой таблице;
     2. контрольные суммы — приход, расход, сальдо, деньги — против Sklad-v4
        и против эталона Sh_29-06-25 (ядро в v4.1 не менялось, суммы должны
        совпасть до копейки);
     3. сироты по внешним ключам — номенклатура, склады, контрагенты, ревизии,
        сделки, накладные;
     4. нарушения CHECK-ограничений;
     5. целостность системы характеристик: значения не соответствуют типу,
        умолчание типа List отсутствует в списке значений, дубли значений;
     6. битые ссылки на кадровую базу Sklad_v1_HR (СотрудникGuid);
     7. целостность взаиморасчётов: ровная нумерация сделок, каждая строка
        привязана к шапке, каждый клиентский платёж — к сделке, клиент
        накладной и платежа совпадает с клиентом сделки;
     8. баланс сделок и сверка с эталоном (включая расхождение с хранимым
        Долгом эталона — это ожидаемо, см. пояснение в разделе 8).

   Все проверки возвращают ПУСТОЙ результат — значит всё в порядке.
   Скрипт ничего не меняет.
   ========================================================================== */

USE [Sklad-v4.1];
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
UNION ALL SELECT N'Сделка',         COUNT(*) FROM dbo.Сделка
UNION ALL SELECT N'НакладнаяРасход',COUNT(*) FROM dbo.НакладнаяРасход
UNION ALL SELECT N'Приход',         COUNT(*) FROM dbo.Приход
UNION ALL SELECT N'Расход',         COUNT(*) FROM dbo.Расход
UNION ALL SELECT N'Деньги',         COUNT(*) FROM dbo.Деньги
UNION ALL SELECT N'Ревизия',        COUNT(*) FROM dbo.Ревизия
UNION ALL SELECT N'Расхождение',    COUNT(*) FROM dbo.Расхождение
ORDER BY [Таблица];
GO

/* ===========================================================================
   2. КОНТРОЛЬНЫЕ СУММЫ: v4.1 против v4 и эталона
   =========================================================================== */
PRINT N'=== 2. Контрольные суммы (v4.1 | v4 | эталон) ===';
SELECT N'Приход: количество' AS [Показатель],
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM dbo.Приход) AS [Sklad-v4.1],
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM [Sklad-v4].dbo.Приход) AS [Sklad-v4],
       (SELECT CAST(SUM(CAST([колОснЕдИзм] AS decimal(18,3))) AS decimal(18,3))
        FROM [Sh_29-06-25].dbo.[СкладПриход]) AS [Эталон]
UNION ALL
SELECT N'Расход: количество',
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM dbo.Расход),
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM [Sklad-v4].dbo.Расход),
       (SELECT CAST(SUM(CAST([колОснЕдИзм] AS decimal(18,3))) AS decimal(18,3))
        FROM [Sh_29-06-25].dbo.[СкладРасход])
UNION ALL
SELECT N'Сальдо',
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM dbo.Приход)
     - (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM dbo.Расход),
       (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM [Sklad-v4].dbo.Приход)
     - (SELECT CAST(SUM(Quantity) AS decimal(18,3)) FROM [Sklad-v4].dbo.Расход),
       (SELECT CAST(SUM(CAST([колОснЕдИзм] AS decimal(18,3))) AS decimal(18,3))
        FROM [Sh_29-06-25].dbo.[СкладПриход])
     - (SELECT CAST(SUM(CAST([колОснЕдИзм] AS decimal(18,3))) AS decimal(18,3))
        FROM [Sh_29-06-25].dbo.[СкладРасход])
UNION ALL
SELECT N'Деньги: сумма',
       (SELECT CAST(SUM(Sum) AS decimal(18,2)) FROM dbo.Деньги),
       (SELECT CAST(SUM(Sum) AS decimal(18,2)) FROM [Sklad-v4].dbo.Деньги),
       (SELECT CAST(SUM(CAST([cуммаПринятыхДенег] AS decimal(18,2))) AS decimal(18,2))
        FROM [Sh_29-06-25].dbo.[КлиентДеньги])
UNION ALL
SELECT N'Номенклатура: позиций',
       (SELECT COUNT(*) FROM dbo.Номенклатура),
       (SELECT COUNT(*) FROM [Sklad-v4].dbo.Номенклатура),
       (SELECT COUNT(*) FROM [Sh_29-06-25].dbo.[Номенклатура])
UNION ALL
SELECT N'Клиенты',
       (SELECT COUNT(*) FROM dbo.Клиент),
       (SELECT COUNT(*) FROM [Sklad-v4].dbo.Клиент),
       (SELECT COUNT(*) FROM [Sh_29-06-25].dbo.[Клиент]);
GO

/* ===========================================================================
   2.1. ВЗАИМОРАСЧЁТЫ: v4.1 против эталона
   =========================================================================== */
PRINT N'=== 2.1. Взаиморасчёты (v4.1 | эталон) ===';
SELECT N'Сделки' AS [Показатель],
       (SELECT COUNT(*) FROM dbo.Сделка) AS [Sklad-v4.1],
       (SELECT COUNT(*) FROM [Sh_29-06-25].dbo.[КлиентМеткаРасчет]) AS [Эталон]
UNION ALL
SELECT N'Накладные',
       (SELECT COUNT(*) FROM dbo.НакладнаяРасход),
       (SELECT COUNT(*) FROM [Sh_29-06-25].dbo.[НакладнаяРасход])
UNION ALL
SELECT N'Отгружено, сумма',
       (SELECT CAST(SUM([Sum]) AS decimal(18,2)) FROM dbo.Расход),
       (SELECT CAST(SUM([суммаПонаклодной]) AS decimal(18,2))
        FROM [Sh_29-06-25].dbo.[НакладнаяРасход])
UNION ALL
SELECT N'Оплачено, сумма',
       (SELECT CAST(SUM([Sum]) AS decimal(18,2))
        FROM dbo.Деньги WHERE [КодСделки] IS NOT NULL),
       (SELECT CAST(SUM([cуммаПринятыхДенег]) AS decimal(18,2))
        FROM [Sh_29-06-25].dbo.[КлиентДеньги])
UNION ALL
SELECT N'Долг (отгружено − оплачено)',
       (SELECT CAST(SUM([Долг]) AS decimal(18,2)) FROM dbo.vw_БалансСделки),
       (SELECT CAST(SUM([суммаПонаклодной]) AS decimal(18,2))
        FROM [Sh_29-06-25].dbo.[НакладнаяРасход])
     - (SELECT CAST(SUM([cуммаПринятыхДенег]) AS decimal(18,2))
        FROM [Sh_29-06-25].dbo.[КлиентДеньги]);
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
   3.1. СИРОТЫ И РАССИНХРОН ВЗАИМОРАСЧЁТОВ (должно быть пусто)
   =========================================================================== */
PRINT N'=== 3.1. Сироты взаиморасчётов (пусто = в порядке) ===';
SELECT N'Накладная без сделки' AS [Проблема], n.[Id] AS [ДокументId],
       CAST(n.[КодСделки] AS nvarchar(36)) AS [Ключ]
FROM dbo.НакладнаяРасход AS n
LEFT JOIN dbo.Сделка AS s ON s.[КодСделки] = n.[КодСделки]
WHERE s.[КодСделки] IS NULL
UNION ALL
SELECT N'Клиент накладной <> клиент сделки', n.[Id], CAST(n.[КодСделки] AS nvarchar(36))
FROM dbo.НакладнаяРасход AS n
INNER JOIN dbo.Сделка AS s ON s.[КодСделки] = n.[КодСделки]
WHERE s.[КлиентId] <> n.[КлиентId]
UNION ALL
SELECT N'Строка ссылается на несуществующую шапку', r.[Id], CAST(r.[UETR] AS nvarchar(36))
FROM dbo.Расход AS r
LEFT JOIN dbo.НакладнаяРасход AS n ON n.[Id] = r.[НакладнаяId]
WHERE r.[НакладнаяId] IS NOT NULL AND n.[Id] IS NULL
UNION ALL
SELECT N'Клиент строки <> клиент шапки', r.[Id], n.[Номер]
FROM dbo.Расход AS r
INNER JOIN dbo.НакладнаяРасход AS n ON n.[Id] = r.[НакладнаяId]
WHERE n.[КлиентId] <> r.[КлиентId]
UNION ALL
SELECT N'UETR строки <> UETR шапки', r.[Id], CAST(r.[UETR] AS nvarchar(36))
FROM dbo.Расход AS r
INNER JOIN dbo.НакладнаяРасход AS n ON n.[Id] = r.[НакладнаяId]
WHERE n.[UETR] <> r.[UETR]
UNION ALL
SELECT N'Продажа без шапки накладной', r.[Id], CAST(r.[UETR] AS nvarchar(36))
FROM dbo.Расход AS r
WHERE r.[Type] = 1 AND r.[НакладнаяId] IS NULL
UNION ALL
SELECT N'Накладная без строк', n.[Id], n.[Номер]
FROM dbo.НакладнаяРасход AS n
WHERE NOT EXISTS (SELECT 1 FROM dbo.Расход AS r WHERE r.[НакладнаяId] = n.[Id])
UNION ALL
SELECT N'Платёж ссылается на несуществующую сделку', m.[Id], CAST(m.[КодСделки] AS nvarchar(36))
FROM dbo.Деньги AS m
LEFT JOIN dbo.Сделка AS s ON s.[КодСделки] = m.[КодСделки]
WHERE m.[КодСделки] IS NOT NULL AND s.[КодСделки] IS NULL
UNION ALL
SELECT N'Клиент платежа <> клиент сделки', m.[Id], CAST(m.[КодСделки] AS nvarchar(36))
FROM dbo.Деньги AS m
INNER JOIN dbo.Сделка AS s ON s.[КодСделки] = m.[КодСделки]
WHERE m.[CounterpartyId] IS NULL OR m.[CounterpartyId] <> s.[КлиентId]
UNION ALL
SELECT N'Платёж по сделке не клиентский', m.[Id], CAST(m.[КодСделки] AS nvarchar(36))
FROM dbo.Деньги AS m
WHERE m.[КодСделки] IS NOT NULL AND m.[CounterpartyType] <> 1
UNION ALL
SELECT N'Сделка без клиента', 0, CAST(s.[КодСделки] AS nvarchar(36))
FROM dbo.Сделка AS s
LEFT JOIN dbo.Клиент AS k ON k.[Id] = s.[КлиентId]
WHERE k.[Id] IS NULL;
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

/* ===========================================================================
   8. НУМЕРАЦИЯ СДЕЛОК (Нарушений = 0)
   =========================================================================== */
PRINT N'=== 8. Нумерация сделок (Нарушений = 0) ===';
WITH n AS
(
    SELECT CAST(SUBSTRING([НомерСделки], 3, 6) AS int) AS Н,
           ROW_NUMBER() OVER (ORDER BY [НомерСделки]) AS rn
    FROM dbo.Сделка
)
SELECT MIN(Н) AS [Мин],
       MAX(Н) AS [Макс],
       COUNT(*) AS [Всего],
       SUM(CASE WHEN Н <> rn THEN 1 ELSE 0 END) AS [Нарушений]
FROM n;
GO

PRINT N'=== 8.1. Дубли номеров и LegacyId (пусто = в порядке) ===';
SELECT N'НомерСделки' AS [Что дублируется], НомерСделки AS [Значение], COUNT(*) AS [Повторов]
FROM dbo.Сделка
GROUP BY НомерСделки
HAVING COUNT(*) > 1
UNION ALL
SELECT N'LegacyId', CAST(LegacyId AS nvarchar(20)), COUNT(*)
FROM dbo.Сделка
WHERE LegacyId IS NOT NULL
GROUP BY LegacyId
HAVING COUNT(*) > 1;
GO

/* ===========================================================================
   9. БАЛАНС: ВЫЧИСЛЕНО ПРОТИВ ХРАНИМОГО ДОЛГА ЭТАЛОНА
   ---------------------------------------------------------------------------
   СовпалоСДвижениями должно быть равно числу сделок: это доказывает, что
   представление считает ровно то же, что дают накладные и платежи.
   СовпалоСХранимым меньше — это и есть причина, по которой Долг перестал
   храниться: в эталоне он не сходился с движениями.
   =========================================================================== */
PRINT N'=== 9. Баланс: вычислено против хранимого Долга эталона ===';
WITH n AS
(
    SELECT [меткаId], CAST(SUM([суммаПонаклодной]) AS decimal(18,2)) AS s
    FROM [Sh_29-06-25].dbo.[НакладнаяРасход]
    GROUP BY [меткаId]
),
d AS
(
    SELECT [меткаId], CAST(SUM([cуммаПринятыхДенег]) AS decimal(18,2)) AS s
    FROM [Sh_29-06-25].dbo.[КлиентДеньги]
    GROUP BY [меткаId]
)
SELECT CAST(SUM(b.[Долг]) AS decimal(18,2)) AS [ВычисленныйДолгV41],
       CAST(SUM(m.[Долг]) AS decimal(18,2)) AS [ХранимыйДолгЭталона],
       SUM(CASE WHEN ABS(b.[Долг] - (ISNULL(n.s, 0) - ISNULL(d.s, 0))) < 0.01
                THEN 1 ELSE 0 END) AS [СовпалоСДвижениями],
       SUM(CASE WHEN ABS(b.[Долг] - m.[Долг]) < 0.01
                THEN 1 ELSE 0 END) AS [СовпалоСХранимым],
       COUNT(*) AS [Сделок]
FROM dbo.vw_БалансСделки AS b
INNER JOIN dbo.Сделка AS s ON s.[КодСделки] = b.[КодСделки]
INNER JOIN [Sh_29-06-25].dbo.[КлиентМеткаРасчет] AS m ON m.[id] = s.[LegacyId]
LEFT JOIN n ON n.[меткаId] = m.[id]
LEFT JOIN d ON d.[меткаId] = m.[id];
GO

PRINT N'    Долг в эталоне хранился и у части сделок не сходился с движениями.';
PRINT N'    В v4.1 он вычисляется, поэтому расхождение с хранимым — ожидаемо.';
PRINT N'    Ниже — первые пять сделок, где значения расходятся.';
SELECT TOP 5
       s.[НомерСделки],
       m.[id]        AS [МеткаВЭтапоне],
       m.[Долг]      AS [ХранимыйДолг],
       b.[Долг]      AS [ВычисленныйДолг],
       b.[Отгружено],
       b.[Оплачено]
FROM dbo.vw_БалансСделки AS b
INNER JOIN dbo.Сделка AS s ON s.[КодСделки] = b.[КодСделки]
INNER JOIN [Sh_29-06-25].dbo.[КлиентМеткаРасчет] AS m ON m.[id] = s.[LegacyId]
WHERE ABS(b.[Долг] - m.[Долг]) >= 0.01
ORDER BY s.[НомерСделки];
GO

PRINT N'Проверка Sklad-v4.1 завершена';
GO
