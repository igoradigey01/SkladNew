/* ============================================================================
   СКЛАД NEW — ВАРИАНТ 1: индексы
   ----------------------------------------------------------------------------
   • каждый внешний ключ получает индекс (иначе UPDATE/DELETE блокируют таблицу)
   • даты и номера накладных — частые выборки в отчётах
   • составные индексы под остатки: (номенклатура, складId)
   Скрипт идемпотентен.
   ========================================================================== */

USE [SkladEtalon];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_КлиентДеньги_клиентId' AND object_id = OBJECT_ID(N'dbo.КлиентДеньги'))
    CREATE NONCLUSTERED INDEX IX_КлиентДеньги_клиентId
        ON dbo.[КлиентДеньги] ([клиентId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_КлиентДеньги_меткаId' AND object_id = OBJECT_ID(N'dbo.КлиентДеньги'))
    CREATE NONCLUSTERED INDEX IX_КлиентДеньги_меткаId
        ON dbo.[КлиентДеньги] ([меткаId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_КлиентМеткаРасчет_клиентId' AND object_id = OBJECT_ID(N'dbo.КлиентМеткаРасчет'))
    CREATE NONCLUSTERED INDEX IX_КлиентМеткаРасчет_клиентId
        ON dbo.[КлиентМеткаРасчет] ([клиентId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_НакладнаяПриход_поставщик' AND object_id = OBJECT_ID(N'dbo.НакладнаяПриход'))
    CREATE NONCLUSTERED INDEX IX_НакладнаяПриход_поставщик
        ON dbo.[НакладнаяПриход] ([поставщик]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_НакладнаяРасход_клиентId' AND object_id = OBJECT_ID(N'dbo.НакладнаяРасход'))
    CREATE NONCLUSTERED INDEX IX_НакладнаяРасход_клиентId
        ON dbo.[НакладнаяРасход] ([клиентId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_НакладнаяРасход_меткаId' AND object_id = OBJECT_ID(N'dbo.НакладнаяРасход'))
    CREATE NONCLUSTERED INDEX IX_НакладнаяРасход_меткаId
        ON dbo.[НакладнаяРасход] ([меткаId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Номенклатура_матерьялId' AND object_id = OBJECT_ID(N'dbo.Номенклатура'))
    CREATE NONCLUSTERED INDEX IX_Номенклатура_матерьялId
        ON dbo.[Номенклатура] ([матерьялId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Номенклатура_основнаяЕдИзмеренияId' AND object_id = OBJECT_ID(N'dbo.Номенклатура'))
    CREATE NONCLUSTERED INDEX IX_Номенклатура_основнаяЕдИзмеренияId
        ON dbo.[Номенклатура] ([основнаяЕдИзмеренияId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ПеремещениеПоСкладам_аудитНомерГруппыId' AND object_id = OBJECT_ID(N'dbo.ПеремещениеПоСкладам'))
    CREATE NONCLUSTERED INDEX IX_ПеремещениеПоСкладам_аудитНомерГруппыId
        ON dbo.[ПеремещениеПоСкладам] ([аудитНомерГруппыId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ПеремещениеПоСкладам_оснЕдИзмId' AND object_id = OBJECT_ID(N'dbo.ПеремещениеПоСкладам'))
    CREATE NONCLUSTERED INDEX IX_ПеремещениеПоСкладам_оснЕдИзмId
        ON dbo.[ПеремещениеПоСкладам] ([оснЕдИзмId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ПеремещениеПоСкладам_складКонечный' AND object_id = OBJECT_ID(N'dbo.ПеремещениеПоСкладам'))
    CREATE NONCLUSTERED INDEX IX_ПеремещениеПоСкладам_складКонечный
        ON dbo.[ПеремещениеПоСкладам] ([складКонечный]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ПеремещениеПоСкладам_складНачальный' AND object_id = OBJECT_ID(N'dbo.ПеремещениеПоСкладам'))
    CREATE NONCLUSTERED INDEX IX_ПеремещениеПоСкладам_складНачальный
        ON dbo.[ПеремещениеПоСкладам] ([складНачальный]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_РезультатАудита_номерГруппыId' AND object_id = OBJECT_ID(N'dbo.РезультатАудита'))
    CREATE NONCLUSTERED INDEX IX_РезультатАудита_номерГруппыId
        ON dbo.[РезультатАудита] ([номерГруппыId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_РезультатАудита_ОснЕдИзмId' AND object_id = OBJECT_ID(N'dbo.РезультатАудита'))
    CREATE NONCLUSTERED INDEX IX_РезультатАудита_ОснЕдИзмId
        ON dbo.[РезультатАудита] ([ОснЕдИзмId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СвязиAlisEдИзмНоменклатура_aliasЕдИзмId' AND object_id = OBJECT_ID(N'dbo.СвязиAlisEдИзмНоменклатура'))
    CREATE NONCLUSTERED INDEX IX_СвязиAlisEдИзмНоменклатура_aliasЕдИзмId
        ON dbo.[СвязиAlisEдИзмНоменклатура] ([aliasЕдИзмId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СвязиAlisEдИзмНоменклатура_idНоменклатура' AND object_id = OBJECT_ID(N'dbo.СвязиAlisEдИзмНоменклатура'))
    CREATE NONCLUSTERED INDEX IX_СвязиAlisEдИзмНоменклатура_idНоменклатура
        ON dbo.[СвязиAlisEдИзмНоменклатура] ([idНоменклатура]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СвязиAlisEдИзмНоменклатура_оснЕдИзмId' AND object_id = OBJECT_ID(N'dbo.СвязиAlisEдИзмНоменклатура'))
    CREATE NONCLUSTERED INDEX IX_СвязиAlisEдИзмНоменклатура_оснЕдИзмId
        ON dbo.[СвязиAlisEдИзмНоменклатура] ([оснЕдИзмId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладПриход_AliasЕдИзмId' AND object_id = OBJECT_ID(N'dbo.СкладПриход'))
    CREATE NONCLUSTERED INDEX IX_СкладПриход_AliasЕдИзмId
        ON dbo.[СкладПриход] ([AliasЕдИзмId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладПриход_аудитНомерГруппыId' AND object_id = OBJECT_ID(N'dbo.СкладПриход'))
    CREATE NONCLUSTERED INDEX IX_СкладПриход_аудитНомерГруппыId
        ON dbo.[СкладПриход] ([аудитНомерГруппыId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладПриход_накладнаяId' AND object_id = OBJECT_ID(N'dbo.СкладПриход'))
    CREATE NONCLUSTERED INDEX IX_СкладПриход_накладнаяId
        ON dbo.[СкладПриход] ([накладнаяId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладПриход_номенклатура' AND object_id = OBJECT_ID(N'dbo.СкладПриход'))
    CREATE NONCLUSTERED INDEX IX_СкладПриход_номенклатура
        ON dbo.[СкладПриход] ([номенклатура]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладПриход_ОснЕдИзмId' AND object_id = OBJECT_ID(N'dbo.СкладПриход'))
    CREATE NONCLUSTERED INDEX IX_СкладПриход_ОснЕдИзмId
        ON dbo.[СкладПриход] ([ОснЕдИзмId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладПриход_складId' AND object_id = OBJECT_ID(N'dbo.СкладПриход'))
    CREATE NONCLUSTERED INDEX IX_СкладПриход_складId
        ON dbo.[СкладПриход] ([складId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладПриход_тараРасходаId' AND object_id = OBJECT_ID(N'dbo.СкладПриход'))
    CREATE NONCLUSTERED INDEX IX_СкладПриход_тараРасходаId
        ON dbo.[СкладПриход] ([тараРасходаId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладРасход_AliasЕдИзмId' AND object_id = OBJECT_ID(N'dbo.СкладРасход'))
    CREATE NONCLUSTERED INDEX IX_СкладРасход_AliasЕдИзмId
        ON dbo.[СкладРасход] ([AliasЕдИзмId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладРасход_аудитНомерГруппыId' AND object_id = OBJECT_ID(N'dbo.СкладРасход'))
    CREATE NONCLUSTERED INDEX IX_СкладРасход_аудитНомерГруппыId
        ON dbo.[СкладРасход] ([аудитНомерГруппыId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладРасход_накладнаяId' AND object_id = OBJECT_ID(N'dbo.СкладРасход'))
    CREATE NONCLUSTERED INDEX IX_СкладРасход_накладнаяId
        ON dbo.[СкладРасход] ([накладнаяId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладРасход_номенклатура' AND object_id = OBJECT_ID(N'dbo.СкладРасход'))
    CREATE NONCLUSTERED INDEX IX_СкладРасход_номенклатура
        ON dbo.[СкладРасход] ([номенклатура]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладРасход_ОснЕдИзмId' AND object_id = OBJECT_ID(N'dbo.СкладРасход'))
    CREATE NONCLUSTERED INDEX IX_СкладРасход_ОснЕдИзмId
        ON dbo.[СкладРасход] ([ОснЕдИзмId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладРасход_складId' AND object_id = OBJECT_ID(N'dbo.СкладРасход'))
    CREATE NONCLUSTERED INDEX IX_СкладРасход_складId
        ON dbo.[СкладРасход] ([складId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладРасход_тараРасходаId' AND object_id = OBJECT_ID(N'dbo.СкладРасход'))
    CREATE NONCLUSTERED INDEX IX_СкладРасход_тараРасходаId
        ON dbo.[СкладРасход] ([тараРасходаId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ХарактеристикаНоменклатуры_значениеId' AND object_id = OBJECT_ID(N'dbo.ХарактеристикаНоменклатуры'))
    CREATE NONCLUSTERED INDEX IX_ХарактеристикаНоменклатуры_значениеId
        ON dbo.[ХарактеристикаНоменклатуры] ([значениеId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ХарактеристикаНоменклатуры_номенклатураId' AND object_id = OBJECT_ID(N'dbo.ХарактеристикаНоменклатуры'))
    CREATE NONCLUSTERED INDEX IX_ХарактеристикаНоменклатуры_номенклатураId
        ON dbo.[ХарактеристикаНоменклатуры] ([номенклатураId]);
GO

-- подбор движений по единице измерения и номенклатуре
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладПриход_Период' AND object_id = OBJECT_ID(N'dbo.СкладПриход'))
    CREATE NONCLUSTERED INDEX IX_СкладПриход_Период
        ON dbo.[СкладПриход] ([ОснЕдИзмId], [AliasЕдИзмId], [номенклатура]);
GO

-- подбор движений по единице измерения и номенклатуре
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладРасход_Период' AND object_id = OBJECT_ID(N'dbo.СкладРасход'))
    CREATE NONCLUSTERED INDEX IX_СкладРасход_Период
        ON dbo.[СкладРасход] ([ОснЕдИзмId], [AliasЕдИзмId], [номенклатура]);
GO

-- расчёт остатка: Приход − Расход
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладПриход_Остаток' AND object_id = OBJECT_ID(N'dbo.СкладПриход'))
    CREATE NONCLUSTERED INDEX IX_СкладПриход_Остаток
        ON dbo.[СкладПриход] ([номенклатура], [складId]);
GO

-- расчёт остатка: Приход − Расход
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_СкладРасход_Остаток' AND object_id = OBJECT_ID(N'dbo.СкладРасход'))
    CREATE NONCLUSTERED INDEX IX_СкладРасход_Остаток
        ON dbo.[СкладРасход] ([номенклатура], [складId]);
GO

-- накладные за период по клиенту
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_НакладнаяРасход_ДатаКлиент' AND object_id = OBJECT_ID(N'dbo.НакладнаяРасход'))
    CREATE NONCLUSTERED INDEX IX_НакладнаяРасход_ДатаКлиент
        ON dbo.[НакладнаяРасход] ([дата], [клиентId]);
GO

-- накладные за период по поставщику
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_НакладнаяПриход_ДатаПоставщик' AND object_id = OBJECT_ID(N'dbo.НакладнаяПриход'))
    CREATE NONCLUSTERED INDEX IX_НакладнаяПриход_ДатаПоставщик
        ON dbo.[НакладнаяПриход] ([дата], [поставщик]);
GO

-- движение денег клиента
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_КлиентДеньги_КлиентДата' AND object_id = OBJECT_ID(N'dbo.КлиентДеньги'))
    CREATE NONCLUSTERED INDEX IX_КлиентДеньги_КлиентДата
        ON dbo.[КлиентДеньги] ([клиентId], [дата]);
GO

-- история расчётов (меток) клиента
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_КлиентМеткаРасчет_КлиентДата' AND object_id = OBJECT_ID(N'dbo.КлиентМеткаРасчет'))
    CREATE NONCLUSTERED INDEX IX_КлиентМеткаРасчет_КлиентДата
        ON dbo.[КлиентМеткаРасчет] ([клиентId], [дата]);
GO

-- итог по ревизии (аудит-метке)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_РезультатАудита_Группа' AND object_id = OBJECT_ID(N'dbo.РезультатАудита'))
    CREATE NONCLUSTERED INDEX IX_РезультатАудита_Группа
        ON dbo.[РезультатАудита] ([номерГруппыId], [номенклатура]);
GO

-- поиск клиента по псевдониму
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Клиент_Псевдоним' AND object_id = OBJECT_ID(N'dbo.Клиент'))
    CREATE NONCLUSTERED INDEX IX_Клиент_Псевдоним
        ON dbo.[Клиент] ([псевдоним]);
GO

-- поиск товара по названию
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Номенклатура_Название' AND object_id = OBJECT_ID(N'dbo.Номенклатура'))
    CREATE NONCLUSTERED INDEX IX_Номенклатура_Название
        ON dbo.[Номенклатура] ([название]);
GO

PRINT N'Индексы созданы (44 шт). Далее: insert-reference-data.sql';
GO