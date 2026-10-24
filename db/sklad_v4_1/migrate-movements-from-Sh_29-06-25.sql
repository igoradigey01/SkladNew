/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА: перенос движений и денег из эталона
   ----------------------------------------------------------------------------
   Что происходит:
     • накладная прихода  -> один UETR на все её строки -> Приход
     • накладная расхода  -> один UETR на все её строки -> Расход
     • принятые деньги    -> Деньги (свой UETR, контрагент = клиент)
     • аудит-метка        -> Ревизия, результат аудита -> Расхождение
       (AccountingValue = учёт, ActualValue = кол - недостача,
        Delta = -недостача, то есть «сколько не хватило»)

   Скрипт одинаков для Sklad-v3, Sklad-v4 и Sklad-v4.1: ядро движения не
   менялось, менялась только система характеристик и слой взаиморасчётов.

   UETR детерминированный: A… — приход, B… — расход, C… — деньги, D… — аудит.
   Повторный запуск не дублирует данные: строки фильтруются по UETR.
   ========================================================================== */

USE [Sklad-v4.1];
GO

SET ANSI_NULLS ON;
GO
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

/* ===========================================================================
   1. ПРИХОД: накладная -> UETR -> строки Income
   =========================================================================== */
WITH nakl AS
(
    SELECT  n.[id], n.[дата], n.[поставщик],
            CONVERT(uniqueidentifier,
                   CONCAT('A0000000-0000-0000-0000-',
                          RIGHT('000000000000' + CAST(n.[id] AS varchar(12)), 12))) AS UETR
    FROM    [Sh_29-06-25].dbo.[НакладнаяПриход] AS n
)
INSERT INTO dbo.Приход (UETR, Date, Type, ПоставщикId, НоменклатураId, СкладId,
                       Quantity, Price, Sum)
SELECT  k.UETR,
        k.[дата],
        1,                                  -- закупка у поставщика
        k.[поставщик],
        s.[номенклатура],
        s.[складId],
        CAST(s.[колОснЕдИзм] AS decimal(18,3)),
        CAST(s.[ценаОснЕдИзм] AS decimal(18,2)),
        CAST(ROUND(s.[колОснЕдИзм] * s.[ценаОснЕдИзм], 2) AS decimal(18,2))
FROM    [Sh_29-06-25].dbo.[СкладПриход] AS s
INNER JOIN nakl AS k ON k.[id] = s.[накладнаяId]
WHERE NOT EXISTS (SELECT 1 FROM dbo.Приход AS i WHERE i.UETR = k.UETR);
GO

/* ===========================================================================
   2. РАСХОД: накладная -> UETR -> строки Outcome
   =========================================================================== */
WITH nakl AS
(
    SELECT  n.[id], n.[дата], n.[клиентId],
            CONVERT(uniqueidentifier,
                   CONCAT('B0000000-0000-0000-0000-',
                          RIGHT('000000000000' + CAST(n.[id] AS varchar(12)), 12))) AS UETR
    FROM    [Sh_29-06-25].dbo.[НакладнаяРасход] AS n
)
INSERT INTO dbo.Расход (UETR, Date, Type, КлиентId, НоменклатураId, СкладId,
                        Quantity, Price, Sum)
SELECT  k.UETR,
        k.[дата],
        1,                                  -- продажа клиенту
        k.[клиентId],
        s.[номенклатура],
        s.[складId],
        CAST(s.[колОснЕдИзм] AS decimal(18,3)),
        CAST(s.[ценаОснЕдИзм] AS decimal(18,2)),
        CAST(ROUND(s.[колОснЕдИзм] * s.[ценаОснЕдИзм], 2) AS decimal(18,2))
FROM    [Sh_29-06-25].dbo.[СкладРасход] AS s
INNER JOIN nakl AS k ON k.[id] = s.[накладнаяId]
WHERE NOT EXISTS (SELECT 1 FROM dbo.Расход AS o WHERE o.UETR = k.UETR);
GO

/* ===========================================================================
   3. ДЕНЬГИ: КлиентДеньги -> Money (свой UETR на каждое движение)
   =========================================================================== */
INSERT INTO dbo.Деньги (UETR, Date, OperationType, CounterpartyId, CounterpartyType,
                      Sum, PaymentMethod, Comment)
SELECT  CONVERT(uniqueidentifier,
                   CONCAT('C0000000-0000-0000-0000-',
                          RIGHT('000000000000' + CAST(s.[id] AS varchar(12)), 12))),
        s.[дата],
        1,                                  -- оплата получена
        s.[клиентId],
        1,                                  -- контрагент = клиент
        CAST(s.[cуммаПринятыхДенег] AS decimal(18,2)),
        1,                                  -- наличные
        NULL
FROM    [Sh_29-06-25].dbo.[КлиентДеньги] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Деньги AS m
    WHERE m.UETR = CONVERT(uniqueidentifier,
                        CONCAT('C0000000-0000-0000-0000-',
                               RIGHT('000000000000' + CAST(s.[id] AS varchar(12)), 12))));
GO

/* ===========================================================================
   4. РЕВИЗИИ: АудитМетка -> Revision, РезультатАудита -> Discrepancy
   =========================================================================== */
INSERT INTO dbo.Ревизия (Code, Type, DateStart, DateEnd, Status)
SELECT  CONCAT('REV-AUD-', RTRIM(s.[номерГруппы])),
        4,                                  -- остатки
        CONVERT(date, s.[датаНазначенияНомера]),
        CONVERT(date, s.[датаНазначенияНомера]),
        3                                   -- завершена
FROM    [Sh_29-06-25].dbo.[АудитМетка] AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Ревизия AS r WHERE r.Code = CONCAT('REV-AUD-', RTRIM(s.[номерГруппы])));
GO

INSERT INTO dbo.Расхождение (КодРевизии, Type, НоменклатураId, AccountingValue, ActualValue, Comment)
SELECT  CONCAT('REV-AUD-', RTRIM(s.[номерГруппы])),
        4,                                  -- остатки
        s.[номенклатура],
        CAST(s.[кол] AS decimal(18,3)),
        CAST(s.[кол] - s.[недостача] AS decimal(18,3)),
        N'Инвентаризация: кол = учёт, кол - недостача = факт'
FROM    [Sh_29-06-25].dbo.[РезультатАудита] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Расхождение AS d
    WHERE d.КодРевизии = CONCAT('REV-AUD-', RTRIM(s.[номерГруппы]))
      AND d.НоменклатураId = s.[номенклатура]
      AND d.AccountingValue = CAST(s.[кол] AS decimal(18,3)));
GO

COMMIT TRANSACTION;
GO

/* --- Итог --- */
SELECT N'Приход'       AS [Таблица], COUNT(*) AS [Строк] FROM dbo.Приход
UNION ALL SELECT N'Расход',      COUNT(*) FROM dbo.Расход
UNION ALL SELECT N'Деньги',        COUNT(*) FROM dbo.Деньги
UNION ALL SELECT N'Ревизия',     COUNT(*) FROM dbo.Ревизия
UNION ALL SELECT N'Расхождение',  COUNT(*) FROM dbo.Расхождение;
GO

SELECT TOP 5 UETR, Date, ПоставщикId, НоменклатураId, Quantity, Price, Sum
FROM dbo.Приход ORDER BY Id;
GO

PRINT N'Движения и деньги эталона перенесены в Sklad-v4.1';
GO