/* ============================================================================
   СКЛАД NEW — Sklad_v4.1.1: проверка полей аудита
   ----------------------------------------------------------------------------
   Проверяет:
     • во всех 19 бизнес-таблицах есть 4 колонки аудита;
     • типы дат — datetime2;
     • нет пустых ДатаСоздания;
     • количество строк совпадает с Sklad-v4.1.

   Если проверка не проходит — скрипт завершается ошибкой (THROW).
   ========================================================================== */

USE [Sklad_v4.1.1];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @tables TABLE (name sysname PRIMARY KEY);
INSERT INTO @tables (name)
VALUES
    (N'ВидНоменклатуры'), (N'ГруппаНоменклатуры'), (N'Деньги'),
    (N'ЕдИзмерения'), (N'ЗначениеХарактеристики'), (N'КатегорияНоменклатуры'),
    (N'Клиент'), (N'НакладнаяРасход'), (N'Номенклатура'), (N'Поставщик'),
    (N'Приход'), (N'Расход'), (N'Расхождение'), (N'Ревизия'),
    (N'Сделка'), (N'Склад'), (N'СписокЗначений'), (N'ТипЗначения'),
    (N'Характеристика');

/* ---------------------------------------------------------------------------
   1. Недостающие колонки
   ------------------------------------------------------------------------- */
CREATE TABLE #missing
(
    table_name sysname NOT NULL,
    column_name sysname NOT NULL
);

INSERT INTO #missing (table_name, column_name)
SELECT t.name, c.name
FROM @tables AS t
CROSS JOIN (VALUES (N'СоздалId'), (N'ИзменилId'),
                   (N'ДатаСоздания'), (N'ДатаИзменения')) AS c (name)
WHERE NOT EXISTS (
    SELECT 1
    FROM sys.columns AS sc
    WHERE sc.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(t.name))
      AND sc.name = c.name
);

IF EXISTS (SELECT 1 FROM #missing)
BEGIN
    SELECT N'НЕ НАЙДЕНЫ КОЛОНКИ' AS [Проверка], * FROM #missing;
    THROW 51000, N'В Sklad_v4.1.1 не хватает колонок аудита', 1;
END

/* ---------------------------------------------------------------------------
   2. Типы дат и nullability
   ------------------------------------------------------------------------- */
SELECT t.name AS [Таблица],
       c.name AS [Колонка],
       ty.name AS [Тип],
       CASE c.is_nullable WHEN 1 THEN N'NULL' ELSE N'NOT NULL' END AS [Nullability]
FROM @tables AS t
JOIN sys.columns AS c
  ON c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(t.name))
JOIN sys.types AS ty
  ON ty.user_type_id = c.user_type_id
WHERE c.name IN (N'ДатаСоздания', N'ДатаИзменения')
ORDER BY t.name, c.name;

IF EXISTS (
    SELECT 1
    FROM @tables AS t
    JOIN sys.columns AS c
      ON c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(t.name))
    JOIN sys.types AS ty
      ON ty.user_type_id = c.user_type_id
    WHERE c.name IN (N'ДатаСоздания', N'ДатаИзменения')
      AND ty.name <> N'datetime2'
)
BEGIN
    THROW 51001, N'Не все колонки аудита имеют тип datetime2', 1;
END

/* ---------------------------------------------------------------------------
   3. Пустые ДатаСоздания
   ------------------------------------------------------------------------- */
CREATE TABLE #null_dates (table_name sysname NOT NULL, null_count bigint NOT NULL);

DECLARE @table sysname;
DECLARE @sql nvarchar(max);

DECLARE tables_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM @tables ORDER BY name;

OPEN tables_cursor;
FETCH NEXT FROM tables_cursor INTO @table;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'INSERT INTO #null_dates (table_name, null_count)
                 SELECT N''' + REPLACE(@table, N'''', N'''''') + N''', COUNT_BIG(*)
                 FROM dbo.' + QUOTENAME(@table) + N'
                 WHERE ДатаСоздания IS NULL;';
    EXEC sys.sp_executesql @sql;

    FETCH NEXT FROM tables_cursor INTO @table;
END
CLOSE tables_cursor;
DEALLOCATE tables_cursor;

IF EXISTS (SELECT 1 FROM #null_dates WHERE null_count > 0)
BEGIN
    SELECT * FROM #null_dates WHERE null_count > 0;
    THROW 51002, N'Есть строки с пустым ДатаСоздания', 1;
END

/* ---------------------------------------------------------------------------
   4. Количество строк: источник и копия
   ------------------------------------------------------------------------- */
CREATE TABLE #row_counts
(
    table_name sysname NOT NULL,
    source_rows bigint NULL,
    target_rows bigint NULL
);

INSERT INTO #row_counts (table_name, source_rows, target_rows)
SELECT COALESCE(s.table_name, t.table_name),
       s.row_count,
       t.row_count
FROM (
    SELECT tb.name AS table_name, SUM(p.rows) AS row_count
    FROM [Sklad-v4.1].sys.tables AS tb
    JOIN [Sklad-v4.1].sys.partitions AS p
      ON p.object_id = tb.object_id
     AND p.index_id IN (0, 1)
    WHERE tb.is_ms_shipped = 0
    GROUP BY tb.name
) AS s
FULL OUTER JOIN (
    SELECT tb.name AS table_name, SUM(p.rows) AS row_count
    FROM [Sklad_v4.1.1].sys.tables AS tb
    JOIN [Sklad_v4.1.1].sys.partitions AS p
      ON p.object_id = tb.object_id
     AND p.index_id IN (0, 1)
    WHERE tb.is_ms_shipped = 0
    GROUP BY tb.name
) AS t
  ON t.table_name = s.table_name;

SELECT table_name AS [Таблица],
       source_rows AS [Sklad-v4.1],
       target_rows AS [Sklad_v4.1.1],
       CASE
           WHEN source_rows = target_rows THEN N'OK'
           ELSE N'РАСХОЖДЕНИЕ'
       END AS [Статус]
FROM #row_counts
ORDER BY table_name;

IF EXISTS (
    SELECT 1
    FROM #row_counts
    WHERE ISNULL(source_rows, -1) <> ISNULL(target_rows, -1)
)
BEGIN
    THROW 51003, N'Количество строк в копии не совпадает с Sklad-v4.1', 1;
END

PRINT N'Sklad_v4.1.1: проверка аудита пройдена.';
GO
