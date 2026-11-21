/* ============================================================================
   СДЕЛКИ ПОСТАВЩИКА

   Сделка поставщика — отдельная сущность, логически не связанная со сделками
   клиента. Клиентская dbo.Сделка не меняется.

   Группировка: один поставщик + один месяц даты прихода = одна сделка.
   Приход и выплата ссылаются на сделку через отдельные колонки
   КодСделкиПоставщика, чтобы не пересекаться с клиентским КодСделки.

   Скрипт идемпотентен: повторный запуск не создаёт дублей.
   Демо-выплаты добавляются один раз на сделку (проверка по UETR).
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

/* ---------------------------------------------------------------------------
   1. Нумерация сделок поставщика: P-000001, P-000002, ...
   ------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.seq_НомерСделкиПоставщика', N'SO') IS NULL
    CREATE SEQUENCE dbo.seq_НомерСделкиПоставщика AS int START WITH 1 INCREMENT BY 1;
GO

/* ---------------------------------------------------------------------------
   2. Сделка поставщика
   ------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.СделкаПоставщика', N'U') IS NULL
CREATE TABLE dbo.СделкаПоставщика
(
    КодСделки    uniqueidentifier NOT NULL,
    НомерСделки  nvarchar(20) NOT NULL
                 CONSTRAINT DF_СделкаПоставщика_НомерСделки
                 DEFAULT (N'P-' + RIGHT('000000' + CAST(NEXT VALUE FOR dbo.seq_НомерСделкиПоставщика AS varchar(10)), 6)),
    ПоставщикId  int NOT NULL,
    Дата         datetime NOT NULL CONSTRAINT DF_СделкаПоставщика_Дата DEFAULT (GETDATE()),
    Комментарий  nvarchar(500) NULL,
    ДатаСоздания datetime NOT NULL CONSTRAINT DF_СделкаПоставщика_ДатаСоздания DEFAULT (GETDATE()),
    Активна      bit NOT NULL CONSTRAINT DF_СделкаПоставщика_Активна DEFAULT (1),

    CONSTRAINT PK_СделкаПоставщика PRIMARY KEY CLUSTERED (КодСделки),
    CONSTRAINT UQ_СделкаПоставщика_НомерСделки UNIQUE (НомерСделки),
    CONSTRAINT FK_СделкаПоставщика_Поставщик FOREIGN KEY (ПоставщикId)
        REFERENCES dbo.Поставщик (Id) ON UPDATE CASCADE ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   3. Приход -> сделка поставщика
   ------------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.Приход', N'КодСделкиПоставщика') IS NULL
    ALTER TABLE dbo.Приход ADD КодСделкиПоставщика uniqueidentifier NULL
        CONSTRAINT FK_Приход_СделкаПоставщика
        REFERENCES dbo.СделкаПоставщика (КодСделки);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Приход_КодСделкиПоставщика'
      AND object_id = OBJECT_ID(N'dbo.Приход')
)
    CREATE INDEX IX_Приход_КодСделкиПоставщика
        ON dbo.Приход (КодСделкиПоставщика) INCLUDE (Date, Sum, UETR);
GO

/* ---------------------------------------------------------------------------
   4. Выплата поставщику -> сделка поставщика

   Клиентский КодСделки остаётся как был. Две колонки одновременно
   заполнять нельзя — это гарантирует CK_Деньги_ОднаСделка.
   ------------------------------------------------------------------------- */
IF COL_LENGTH(N'dbo.Деньги', N'КодСделкиПоставщика') IS NULL
    ALTER TABLE dbo.Деньги ADD КодСделкиПоставщика uniqueidentifier NULL
        CONSTRAINT FK_Деньги_СделкаПоставщика
        REFERENCES dbo.СделкаПоставщика (КодСделки);
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Деньги_ОднаСделка')
    ALTER TABLE dbo.Деньги ADD CONSTRAINT CK_Деньги_ОднаСделка CHECK (
        КодСделки IS NULL OR КодСделкиПоставщика IS NULL);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Деньги_КодСделкиПоставщика'
      AND object_id = OBJECT_ID(N'dbo.Деньги')
)
    CREATE INDEX IX_Деньги_КодСделкиПоставщика
        ON dbo.Деньги (КодСделкиПоставщика) INCLUDE (Date, Sum, OperationType);
GO

/* ---------------------------------------------------------------------------
   5. Триггеры

   TR_Деньги_Проверка: клиентская ветка не меняется, добавлена проверка
   выплаты по сделке поставщика.
   ------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.TR_Деньги_Проверка', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Деньги_Проверка;
GO

CREATE TRIGGER dbo.TR_Деньги_Проверка
ON dbo.Деньги
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        INNER JOIN dbo.Сделка AS s ON s.КодСделки = i.КодСделки
        WHERE i.CounterpartyType <> 1
           OR i.CounterpartyId IS NULL
           OR i.CounterpartyId <> s.КлиентId
           OR i.OperationType NOT IN (1, 2, 4)
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51012, N'Платёж по сделке должен быть клиентским и ссылаться на клиента сделки', 1;
    END

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        INNER JOIN dbo.СделкаПоставщика AS s
            ON s.КодСделки = i.КодСделкиПоставщика
        WHERE i.CounterpartyType <> 2
           OR i.CounterpartyId IS NULL
           OR i.CounterpartyId <> s.ПоставщикId
           OR i.OperationType NOT IN (1, 2, 4)
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51014, N'Выплата по сделке поставщика должна ссылаться на этого поставщика', 1;
    END
END
GO

/* Приход со сделкой должен принадлежать тому же поставщику. */
IF OBJECT_ID(N'dbo.TR_Приход_Проверка', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_Приход_Проверка;
GO

CREATE TRIGGER dbo.TR_Приход_Проверка
ON dbo.Приход
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS i
        INNER JOIN dbo.СделкаПоставщика AS s
            ON s.КодСделки = i.КодСделкиПоставщика
        WHERE s.ПоставщикId <> i.ПоставщикId
           OR i.Type <> 1
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51013, N'Приход не совпадает со сделкой поставщика (поставщик или тип)', 1;
    END
END
GO

/* ---------------------------------------------------------------------------
   6. Перенос: поставщик + месяц = сделка
   ------------------------------------------------------------------------- */
;WITH groups AS
(
    SELECT  i.ПоставщикId,
            DATEFROMPARTS(YEAR(i.Date), MONTH(i.Date), 1) AS Месяц
    FROM    dbo.Приход AS i
    WHERE   i.Type = 1
    GROUP BY i.ПоставщикId, DATEFROMPARTS(YEAR(i.Date), MONTH(i.Date), 1)
)
INSERT INTO dbo.СделкаПоставщика
    (КодСделки, НомерСделки, ПоставщикId, Дата, Комментарий)
SELECT  CONVERT(uniqueidentifier,
                'F0000000-0000-0000-0000-'
                + RIGHT('000000' + CAST(g.ПоставщикId AS varchar(6)), 6)
                + CONVERT(char(6), g.Месяц, 112)),
        N'P-' + RIGHT('000000' + CAST(NEXT VALUE FOR dbo.seq_НомерСделкиПоставщика AS varchar(10)), 6),
        g.ПоставщикId,
        g.Месяц,
        N'Поставка ' + CONVERT(char(7), g.Месяц, 120)
FROM    groups AS g
WHERE   NOT EXISTS
        (
            SELECT 1
            FROM dbo.СделкаПоставщика AS s
            WHERE s.КодСделки = CONVERT(uniqueidentifier,
                                        'F0000000-0000-0000-0000-'
                                        + RIGHT('000000' + CAST(g.ПоставщикId AS varchar(6)), 6)
                                        + CONVERT(char(6), g.Месяц, 112))
        );
GO

UPDATE i
SET    КодСделкиПоставщика = s.КодСделки
FROM   dbo.Приход AS i
INNER JOIN dbo.СделкаПоставщика AS s
    ON  s.ПоставщикId = i.ПоставщикId
    AND s.Дата = DATEFROMPARTS(YEAR(i.Date), MONTH(i.Date), 1)
WHERE  i.Type = 1
   AND i.КодСделкиПоставщика IS NULL;
GO

/* ---------------------------------------------------------------------------
   7. Демо-выплаты поставщику

   Сумма выплат считается от суммы приходов сделки. Сценарий зависит от
   порядкового номера сделки у поставщика (idx % 3):
     0 — один платёж 100 % (долг 0)
     1 — два платежа 40 % + 30 % (долг 30 %)
     2 — один платёж 55 % (долг 45 %)
   UETR детерминированный, повторный запуск не дублирует строки.
   ------------------------------------------------------------------------- */
;WITH deals AS
(
    SELECT  s.КодСделки,
            s.ПоставщикId,
            s.Дата AS Месяц,
            CAST(SUM(i.Sum) AS decimal(18, 2)) AS Сумма,
            MAX(i.Date) AS ПоследняяДата,
            ROW_NUMBER() OVER (PARTITION BY s.ПоставщикId ORDER BY s.Дата) AS idx
    FROM    dbo.СделкаПоставщика AS s
    INNER JOIN dbo.Приход AS i ON i.КодСделкиПоставщика = s.КодСделки
    GROUP BY s.КодСделки, s.ПоставщикId, s.Дата
)
INSERT INTO dbo.Деньги
    (UETR, Date, OperationType, CounterpartyId, CounterpartyType,
     КодСделкиПоставщика, Sum, PaymentMethod, Comment)
SELECT  CONVERT(uniqueidentifier,
                'C0000000-0000-0000-0001-'
                + RIGHT('00000' + CAST(d.ПоставщикId AS varchar(5)), 5)
                + CONVERT(char(6), d.Месяц, 112) + '1'),
        DATEADD(day, 5, d.ПоследняяДата),
        2,                      -- оплата выдана
        d.ПоставщикId,
        2,                      -- контрагент = поставщик
        d.КодСделки,
        CASE d.idx % 3
            WHEN 0 THEN d.Сумма
            WHEN 1 THEN CAST(ROUND(d.Сумма * 0.40, 2) AS decimal(18, 2))
            ELSE        CAST(ROUND(d.Сумма * 0.55, 2) AS decimal(18, 2))
        END,
        1,                      -- наличные
        N'Демо-выплата поставщику (миграция)'
FROM    deals AS d
WHERE   NOT EXISTS
        (
            SELECT 1
            FROM dbo.Деньги AS m
            WHERE m.UETR = CONVERT(uniqueidentifier,
                                   'C0000000-0000-0000-0001-'
                                   + RIGHT('00000' + CAST(d.ПоставщикId AS varchar(5)), 5)
                                   + CONVERT(char(6), d.Месяц, 112) + '1')
        );
GO

;WITH deals AS
(
    SELECT  s.КодСделки,
            s.ПоставщикId,
            s.Дата AS Месяц,
            CAST(SUM(i.Sum) AS decimal(18, 2)) AS Сумма,
            MAX(i.Date) AS ПоследняяДата,
            ROW_NUMBER() OVER (PARTITION BY s.ПоставщикId ORDER BY s.Дата) AS idx
    FROM    dbo.СделкаПоставщика AS s
    INNER JOIN dbo.Приход AS i ON i.КодСделкиПоставщика = s.КодСделки
    GROUP BY s.КодСделки, s.ПоставщикId, s.Дата
)
INSERT INTO dbo.Деньги
    (UETR, Date, OperationType, CounterpartyId, CounterpartyType,
     КодСделкиПоставщика, Sum, PaymentMethod, Comment)
SELECT  CONVERT(uniqueidentifier,
                'C0000000-0000-0000-0001-'
                + RIGHT('00000' + CAST(d.ПоставщикId AS varchar(5)), 5)
                + CONVERT(char(6), d.Месяц, 112) + '2'),
        DATEADD(day, 12, d.ПоследняяДата),
        2,
        d.ПоставщикId,
        2,
        d.КодСделки,
        CAST(ROUND(d.Сумма * 0.30, 2) AS decimal(18, 2)),
        1,
        N'Демо-выплата поставщику (миграция)'
FROM    deals AS d
WHERE   d.idx % 3 = 1
    AND NOT EXISTS
        (
            SELECT 1
            FROM dbo.Деньги AS m
            WHERE m.UETR = CONVERT(uniqueidentifier,
                                   'C0000000-0000-0000-0001-'
                                   + RIGHT('00000' + CAST(d.ПоставщикId AS varchar(5)), 5)
                                   + CONVERT(char(6), d.Месяц, 112) + '2')
        );
GO

/* ---------------------------------------------------------------------------
   8. Баланс сделки поставщика

   Долг — сколько мы должны поставщику: получено минус отдано.
   Возврат от поставщика (OperationType = 1) уменьшает долг.
   ------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.vw_БалансСделкиПоставщика', N'V') IS NOT NULL
    DROP VIEW dbo.vw_БалансСделкиПоставщика;
GO

CREATE VIEW dbo.vw_БалансСделкиПоставщика
AS
WITH получено AS
(
    SELECT  i.[КодСделкиПоставщика],
            COUNT(DISTINCT i.[UETR]) AS Накладных,
            CAST(ISNULL(SUM(i.[Sum]), 0) AS decimal(18, 2)) AS Получено
    FROM    dbo.Приход AS i
    WHERE   i.[КодСделкиПоставщика] IS NOT NULL
    GROUP BY i.[КодСделкиПоставщика]
),
отдано AS
(
    SELECT  m.[КодСделкиПоставщика],
            COUNT(*) AS Платежей,
            CAST(ISNULL(SUM(
                CASE WHEN m.[OperationType] = 2 THEN m.[Sum]
                     ELSE -m.[Sum]
                END), 0) AS decimal(18, 2)) AS Отдано
    FROM    dbo.Деньги AS m
    WHERE   m.[КодСделкиПоставщика] IS NOT NULL
    GROUP BY m.[КодСделкиПоставщика]
)
SELECT  s.[КодСделки],
        s.[НомерСделки],
        s.[ПоставщикId],
        p.[Название]            AS Поставщик,
        s.[Дата],
        s.[Активна],
        ISNULL(o.[Накладных], 0) AS Накладных,
        ISNULL(o.[Получено], 0)  AS Получено,
        ISNULL(pl.[Платежей], 0) AS Платежей,
        ISNULL(pl.[Отдано], 0)   AS Отдано,
        CAST(ISNULL(o.[Получено], 0) - ISNULL(pl.[Отдано], 0) AS decimal(18, 2)) AS Долг,
        CAST(CASE WHEN ISNULL(pl.[Отдано], 0) > ISNULL(o.[Получено], 0)
                  THEN ISNULL(pl.[Отдано], 0) - ISNULL(o.[Получено], 0)
                  ELSE 0
             END AS decimal(18, 2)) AS Переплата
FROM    dbo.СделкаПоставщика AS s
INNER JOIN dbo.Поставщик AS p ON p.[Id] = s.[ПоставщикId]
LEFT JOIN получено AS o ON o.[КодСделкиПоставщика] = s.[КодСделки]
LEFT JOIN отдано  AS pl ON pl.[КодСделкиПоставщика] = s.[КодСделки];
GO

COMMIT TRANSACTION;
GO

PRINT N'Сделки поставщика созданы';
GO
