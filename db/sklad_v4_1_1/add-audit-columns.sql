/* ============================================================================
   СКЛАД NEW — Sklad_v4.1.1: поля аудита
   ----------------------------------------------------------------------------
   Добавляет во все бизнес-таблицы:
       СоздалId       nvarchar(450) NULL
       ИзменилId      nvarchar(450) NULL
       ДатаСоздания   datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
       ДатаИзменения  datetime2 NULL

   Скрипт идемпотентен:
     • существующие колонки не добавляются повторно;
     • существующие datetime-колонки приводятся к datetime2;
     • существующие default-ограничения заменяются на SYSUTCDATETIME().

   Запуск:
     sqlcmd -S localhost -U sa -P '<пароль>' -C -f 65001 -b \
            -i add-audit-columns.sql
   ========================================================================== */

USE [Sklad_v4.1.1];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @tables TABLE (name sysname PRIMARY KEY);

INSERT INTO @tables (name)
VALUES
    (N'ВидНоменклатуры'),
    (N'ГруппаНоменклатуры'),
    (N'Деньги'),
    (N'ЕдИзмерения'),
    (N'ЗначениеХарактеристики'),
    (N'КатегорияНоменклатуры'),
    (N'Клиент'),
    (N'НакладнаяРасход'),
    (N'Номенклатура'),
    (N'Поставщик'),
    (N'Приход'),
    (N'Расход'),
    (N'Расхождение'),
    (N'Ревизия'),
    (N'Сделка'),
    (N'Склад'),
    (N'СписокЗначений'),
    (N'ТипЗначения'),
    (N'Характеристика');

DECLARE @table sysname;
DECLARE @sql nvarchar(max);
DECLARE @objectId int;
DECLARE @defaultName sysname;
DECLARE @newDefaultName sysname;
DECLARE @nullable nvarchar(10);
DECLARE @hadDefault bit;

DECLARE tables_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM @tables ORDER BY name;

OPEN tables_cursor;
FETCH NEXT FROM tables_cursor INTO @table;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @objectId = OBJECT_ID(N'dbo.' + QUOTENAME(@table));

    IF @objectId IS NULL
    BEGIN
        RAISERROR (N'Таблица dbo.%s не найдена', 16, 1, @table);
        CLOSE tables_cursor;
        DEALLOCATE tables_cursor;
        RETURN;
    END

    /* -----------------------------------------------------------------------
       СоздалId
       --------------------------------------------------------------------- */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = @objectId AND name = N'СоздалId'
    )
    BEGIN
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                 + N' ADD СоздалId nvarchar(450) NULL;';
        EXEC sys.sp_executesql @sql;
    END

    /* -----------------------------------------------------------------------
       ИзменилId
       --------------------------------------------------------------------- */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = @objectId AND name = N'ИзменилId'
    )
    BEGIN
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                 + N' ADD ИзменилId nvarchar(450) NULL;';
        EXEC sys.sp_executesql @sql;
    END

    /* -----------------------------------------------------------------------
       ДатаСоздания
       --------------------------------------------------------------------- */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = @objectId AND name = N'ДатаСоздания'
    )
    BEGIN
        SET @newDefaultName = N'DF_Аудит_' + @table + N'_ДатаСоздания';
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                 + N' ADD ДатаСоздания datetime2 NOT NULL'
                 + N' CONSTRAINT ' + QUOTENAME(@newDefaultName)
                 + N' DEFAULT (SYSUTCDATETIME());';
        EXEC sys.sp_executesql @sql;
    END
    ELSE
    BEGIN
        /* Сначала убрать default: ALTER COLUMN не работает при зависимом constraint */
        SET @defaultName = NULL;

        SELECT @defaultName = dc.name
        FROM sys.default_constraints AS dc
        JOIN sys.columns AS c
          ON c.object_id = dc.parent_object_id
         AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = @objectId
          AND c.name = N'ДатаСоздания';

        IF @defaultName IS NOT NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                     + N' DROP CONSTRAINT ' + QUOTENAME(@defaultName) + N';';
            EXEC sys.sp_executesql @sql;
        END

        /* Привести тип к datetime2, сохранив nullability */
        SET @nullable = CASE WHEN EXISTS (
            SELECT 1
            FROM sys.columns
            WHERE object_id = @objectId
              AND name = N'ДатаСоздания'
              AND is_nullable = 1
        ) THEN N'NULL' ELSE N'NOT NULL' END;

        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                 + N' ALTER COLUMN ДатаСоздания datetime2 ' + @nullable + N';';
        EXEC sys.sp_executesql @sql;

        /* Вернуть default, теперь в UTC */
        SET @newDefaultName = N'DF_Аудит_' + @table + N'_ДатаСоздания';
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                 + N' ADD CONSTRAINT ' + QUOTENAME(@newDefaultName)
                 + N' DEFAULT (SYSUTCDATETIME()) FOR ДатаСоздания;';
        EXEC sys.sp_executesql @sql;
    END

    /* -----------------------------------------------------------------------
       ДатаИзменения
       --------------------------------------------------------------------- */
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = @objectId AND name = N'ДатаИзменения'
    )
    BEGIN
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                 + N' ADD ДатаИзменения datetime2 NULL;';
        EXEC sys.sp_executesql @sql;
    END
    ELSE
    BEGIN
        /* Сначала убрать default, если он есть */
        SET @defaultName = NULL;
        SET @hadDefault = 0;

        SELECT @defaultName = dc.name
        FROM sys.default_constraints AS dc
        JOIN sys.columns AS c
          ON c.object_id = dc.parent_object_id
         AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = @objectId
          AND c.name = N'ДатаИзменения';

        SET @hadDefault = CASE WHEN @defaultName IS NULL THEN 0 ELSE 1 END;

        IF @defaultName IS NOT NULL
        BEGIN
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                     + N' DROP CONSTRAINT ' + QUOTENAME(@defaultName) + N';';
            EXEC sys.sp_executesql @sql;
        END

        SET @nullable = CASE WHEN EXISTS (
            SELECT 1
            FROM sys.columns
            WHERE object_id = @objectId
              AND name = N'ДатаИзменения'
              AND is_nullable = 1
        ) THEN N'NULL' ELSE N'NOT NULL' END;

        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                 + N' ALTER COLUMN ДатаИзменения datetime2 ' + @nullable + N';';
        EXEC sys.sp_executesql @sql;

        IF @hadDefault = 1
        BEGIN
            SET @newDefaultName = N'DF_Аудит_' + @table + N'_ДатаИзменения';
            SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
                     + N' ADD CONSTRAINT ' + QUOTENAME(@newDefaultName)
                     + N' DEFAULT (SYSUTCDATETIME()) FOR ДатаИзменения;';
            EXEC sys.sp_executesql @sql;
        END
    END

    FETCH NEXT FROM tables_cursor INTO @table;
END

CLOSE tables_cursor;
DEALLOCATE tables_cursor;
GO

PRINT N'Sklad_v4.1.1: поля аудита добавлены. Далее: verify-audit.sql';
GO
