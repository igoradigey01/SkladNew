/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА: индексы
   ----------------------------------------------------------------------------
   БД: Sklad-v4.1
   • UETR — индекс на каждой из трёх глобальных таблиц. Уникальным он НЕ
     может быть: один UETR = один документ, а в документе несколько строк.
     Связность «Приход ↔ Расход ↔ Деньги по одному UETR» обеспечивает
     приложение (валидация при проведении); уникальность обеспечивается тем,
     что новый документ всегда получает новый UETR.
     Исключение — НакладнаяРасход: там UETR уникален, потому что это шапка,
     а не строки.
   • дата + номенклатура — отчёты и остатки;
   • каждый внешний ключ проиндексирован;
   • СотрудникGuid — точка связи с кадровой базой Sklad_v1_HR: по нему
     строятся отчёты «кто что отгрузил» и выборки выплат;
   • по системе характеристик (v4): ТипЗначения — выборка атрибутов по типу,
     СписокЗначений — значения одного атрибута, ЗначениеХарактеристики —
     значения одной позиции и одного атрибута;
   • по взаиморасчётам (v4.1): НомерСделки, КодСделки в накладных и деньгах,
     НакладнаяId в строках — чтобы баланс сделки считался поseek'ами,
     а не сканированием истории клиента.
   Скрипт идемпотентен.
   ========================================================================== */

USE [Sklad-v4.1];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* ===========================================================================
   1. ЯДРО: ПРИХОД / РАСХОД / ДЕНЬГИ
   =========================================================================== */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Приход_UETR' AND object_id = OBJECT_ID(N'dbo.Приход'))
    CREATE NONCLUSTERED INDEX IX_Приход_UETR ON dbo.Приход (UETR);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Приход_Date' AND object_id = OBJECT_ID(N'dbo.Приход'))
    CREATE NONCLUSTERED INDEX IX_Приход_Date ON dbo.Приход (Date)
        INCLUDE (НоменклатураId, Quantity, Sum);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Приход_Остаток' AND object_id = OBJECT_ID(N'dbo.Приход'))
    CREATE NONCLUSTERED INDEX IX_Приход_Остаток ON dbo.Приход (НоменклатураId, СкладId, Date);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Приход_Поставщик' AND object_id = OBJECT_ID(N'dbo.Приход'))
    CREATE NONCLUSTERED INDEX IX_Приход_Поставщик ON dbo.Приход (ПоставщикId, Date);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Приход_Склад' AND object_id = OBJECT_ID(N'dbo.Приход'))
    CREATE NONCLUSTERED INDEX IX_Приход_Склад ON dbo.Приход (СкладId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Расход_UETR' AND object_id = OBJECT_ID(N'dbo.Расход'))
    CREATE NONCLUSTERED INDEX IX_Расход_UETR ON dbo.Расход (UETR);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Расход_Date' AND object_id = OBJECT_ID(N'dbo.Расход'))
    CREATE NONCLUSTERED INDEX IX_Расход_Date ON dbo.Расход (Date)
        INCLUDE (КлиентId, НоменклатураId, Quantity, Sum);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Расход_Остаток' AND object_id = OBJECT_ID(N'dbo.Расход'))
    CREATE NONCLUSTERED INDEX IX_Расход_Остаток ON dbo.Расход (НоменклатураId, СкладId, Date);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Расход_Клиент' AND object_id = OBJECT_ID(N'dbo.Расход'))
    CREATE NONCLUSTERED INDEX IX_Расход_Клиент ON dbo.Расход (КлиентId, Date);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Расход_Склад' AND object_id = OBJECT_ID(N'dbo.Расход'))
    CREATE NONCLUSTERED INDEX IX_Расход_Склад ON dbo.Расход (СкладId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Деньги_UETR' AND object_id = OBJECT_ID(N'dbo.Деньги'))
    CREATE NONCLUSTERED INDEX IX_Деньги_UETR ON dbo.Деньги (UETR);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Деньги_Date' AND object_id = OBJECT_ID(N'dbo.Деньги'))
    CREATE NONCLUSTERED INDEX IX_Деньги_Date ON dbo.Деньги (Date)
        INCLUDE (OperationType, CounterpartyType, Sum);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Деньги_Контрагент' AND object_id = OBJECT_ID(N'dbo.Деньги'))
    CREATE NONCLUSTERED INDEX IX_Деньги_Контрагент ON dbo.Деньги (CounterpartyType, CounterpartyId, Date);
GO

/* ===========================================================================
   2. СПРАВОЧНИКИ
   =========================================================================== */
-- поиск номенклатуры: по виду, по коду, по названию, по базовой единице
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Номенклатура_ВидНоменклатурыId' AND object_id = OBJECT_ID(N'dbo.Номенклатура'))
    CREATE NONCLUSTERED INDEX IX_Номенклатура_ВидНоменклатурыId ON dbo.Номенклатура (ВидНоменклатурыId)
        INCLUDE (Название, Код, ЦенаПродажная);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Номенклатура_Код' AND object_id = OBJECT_ID(N'dbo.Номенклатура'))
    CREATE NONCLUSTERED INDEX IX_Номенклатура_Код ON dbo.Номенклатура (Код);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Номенклатура_Название' AND object_id = OBJECT_ID(N'dbo.Номенклатура'))
    CREATE NONCLUSTERED INDEX IX_Номенклатура_Название ON dbo.Номенклатура (Название)
        INCLUDE (Код, БазоваяЕдИзмId, ЦенаПродажная);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Номенклатура_БазоваяЕдИзмId' AND object_id = OBJECT_ID(N'dbo.Номенклатура'))
    CREATE NONCLUSTERED INDEX IX_Номенклатура_БазоваяЕдИзмId ON dbo.Номенклатура (БазоваяЕдИзмId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Номенклатура_Группа' AND object_id = OBJECT_ID(N'dbo.Номенклатура'))
    CREATE NONCLUSTERED INDEX IX_Номенклатура_Группа ON dbo.Номенклатура (ГруппаId, КатегорияId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Номенклатура_Активен' AND object_id = OBJECT_ID(N'dbo.Номенклатура'))
    CREATE NONCLUSTERED INDEX IX_Номенклатура_Активен ON dbo.Номенклатура (Активен);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Характеристика_ВидНоменклатурыId' AND object_id = OBJECT_ID(N'dbo.Характеристика'))
    CREATE NONCLUSTERED INDEX IX_Характеристика_ВидНоменклатурыId ON dbo.Характеристика (ВидНоменклатурыId)
        INCLUDE (Название, Порядок, ТипЗначенияId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Характеристика_ТипЗначенияId' AND object_id = OBJECT_ID(N'dbo.Характеристика'))
    CREATE NONCLUSTERED INDEX IX_Характеристика_ТипЗначенияId ON dbo.Характеристика (ТипЗначенияId)
        INCLUDE (ВидНоменклатурыId, Название);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СписокЗначений_ХарактеристикаId' AND object_id = OBJECT_ID(N'dbo.СписокЗначений'))
    CREATE NONCLUSTERED INDEX IX_СписокЗначений_ХарактеристикаId ON dbo.СписокЗначений (ХарактеристикаId)
        INCLUDE (Значение, Порядок);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ЗначениеХарактеристики_НоменклатураId' AND object_id = OBJECT_ID(N'dbo.ЗначениеХарактеристики'))
    CREATE NONCLUSTERED INDEX IX_ЗначениеХарактеристики_НоменклатураId ON dbo.ЗначениеХарактеристики (НоменклатураId)
        INCLUDE (ХарактеристикаId, Значение);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ЗначениеХарактеристики_ХарактеристикаId' AND object_id = OBJECT_ID(N'dbo.ЗначениеХарактеристики'))
    CREATE NONCLUSTERED INDEX IX_ЗначениеХарактеристики_ХарактеристикаId ON dbo.ЗначениеХарактеристики (ХарактеристикаId)
        INCLUDE (НоменклатураId, Значение);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ЗначениеХарактеристики_НоменклатураId' AND object_id = OBJECT_ID(N'dbo.ЗначениеХарактеристики'))
    CREATE NONCLUSTERED INDEX IX_ЗначениеХарактеристики_НоменклатураId ON dbo.ЗначениеХарактеристики (НоменклатураId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ЗначениеХарактеристики_ХарактеристикаId' AND object_id = OBJECT_ID(N'dbo.ЗначениеХарактеристики'))
    CREATE NONCLUSTERED INDEX IX_ЗначениеХарактеристики_ХарактеристикаId ON dbo.ЗначениеХарактеристики (ХарактеристикаId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ГруппаНоменклатуры_Родитель' AND object_id = OBJECT_ID(N'dbo.ГруппаНоменклатуры'))
    CREATE NONCLUSTERED INDEX IX_ГруппаНоменклатуры_Родитель ON dbo.ГруппаНоменклатуры (РодительId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Клиент_Фио' AND object_id = OBJECT_ID(N'dbo.Клиент'))
    CREATE NONCLUSTERED INDEX IX_Клиент_Фио ON dbo.Клиент (Фио);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Клиент_Город' AND object_id = OBJECT_ID(N'dbo.Клиент'))
    CREATE NONCLUSTERED INDEX IX_Клиент_Город ON dbo.Клиент (Город);
GO
/* --- Связь с кадровой базой Sklad_v1_HR ------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Приход_Сотрудник' AND object_id = OBJECT_ID(N'dbo.Приход'))
    CREATE NONCLUSTERED INDEX IX_Приход_Сотрудник ON dbo.Приход (СотрудникGuid)
        INCLUDE (Date, НоменклатураId, Quantity);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Расход_Сотрудник' AND object_id = OBJECT_ID(N'dbo.Расход'))
    CREATE NONCLUSTERED INDEX IX_Расход_Сотрудник ON dbo.Расход (СотрудникGuid)
        INCLUDE (Date, НоменклатураId, Quantity);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Деньги_Сотрудник' AND object_id = OBJECT_ID(N'dbo.Деньги'))
    CREATE NONCLUSTERED INDEX IX_Деньги_Сотрудник ON dbo.Деньги (СотрудникGuid)
        INCLUDE (Date, OperationType, [Sum]);
GO

/* ===========================================================================
   3. РЕВИЗИИ
   =========================================================================== */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Ревизия_Период' AND object_id = OBJECT_ID(N'dbo.Ревизия'))
    CREATE NONCLUSTERED INDEX IX_Ревизия_Период ON dbo.Ревизия (DateStart, DateEnd);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Ревизия_Статус' AND object_id = OBJECT_ID(N'dbo.Ревизия'))
    CREATE NONCLUSTERED INDEX IX_Ревизия_Статус ON dbo.Ревизия (Status, Type);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Расхождение_КодРевизии' AND object_id = OBJECT_ID(N'dbo.Расхождение'))
    CREATE NONCLUSTERED INDEX IX_Расхождение_КодРевизии ON dbo.Расхождение (КодРевизии)
        INCLUDE (Type, НоменклатураId, AccountingValue, ActualValue);
GO

/* ===========================================================================
   4. ВЗАИМОРАСЧЁТЫ
   ---------------------------------------------------------------------------
   Баланс сделки читается по трём индексам:
     • IX_НакладнаяРасход_КодСделки — найти накладные сделки;
     • IX_Расход_Накладная        — сложить их строки;
     • IX_Деньги_КодСделки        — сложить платежи.
   Уникальность НомерСделки обеспечивает UNIQUE-ограничение UQ_Сделка_НомерСделки,
   оно уже создало индекс. LegacyId намеренно не индексируется: он нужен
   только при переносе, а фильтрованный индекс заставлял бы клиентов включать
   QUOTED_IDENTIFIER при любой записи в Сделка.
   =========================================================================== */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Сделка_Клиент' AND object_id = OBJECT_ID(N'dbo.Сделка'))
    CREATE NONCLUSTERED INDEX IX_Сделка_Клиент ON dbo.Сделка (КлиентId, Дата)
        INCLUDE (НомерСделки, Активна);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Сделка_Дата' AND object_id = OBJECT_ID(N'dbo.Сделка'))
    CREATE NONCLUSTERED INDEX IX_Сделка_Дата ON dbo.Сделка (Дата)
        INCLUDE (КлиентId, НомерСделки, Активна);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_НакладнаяРасход_КодСделки' AND object_id = OBJECT_ID(N'dbo.НакладнаяРасход'))
    CREATE NONCLUSTERED INDEX IX_НакладнаяРасход_КодСделки ON dbo.НакладнаяРасход (КодСделки, Дата)
        INCLUDE (КлиентId, Номер);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_НакладнаяРасход_Клиент' AND object_id = OBJECT_ID(N'dbo.НакладнаяРасход'))
    CREATE NONCLUSTERED INDEX IX_НакладнаяРасход_Клиент ON dbo.НакладнаяРасход (КлиентId, Дата)
        INCLUDE (КодСделки, Номер);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Расход_Накладная' AND object_id = OBJECT_ID(N'dbo.Расход'))
    CREATE NONCLUSTERED INDEX IX_Расход_Накладная ON dbo.Расход (НакладнаяId)
        INCLUDE ([Sum], Quantity, НоменклатураId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Деньги_КодСделки' AND object_id = OBJECT_ID(N'dbo.Деньги'))
    CREATE NONCLUSTERED INDEX IX_Деньги_КодСделки ON dbo.Деньги (КодСделки, Date)
        INCLUDE ([Sum], OperationType, CounterpartyId);
GO

PRINT N'Индексы Sklad-v4.1 созданы';
GO