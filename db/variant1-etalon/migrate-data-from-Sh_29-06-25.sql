/* ============================================================================
   СКЛАД NEW — ВАРИАНТ 1: перенос данных из эталона Sh_29-06-25
   ----------------------------------------------------------------------------
   Скрипт работает, когда обе базы на одном экземпляре SQL Server: переливка
   идёт через трёхчастные имена, без промежуточных файлов.

   Что происходит:
     • строковые ключи справочников становятся int — значение подставляется
       подзапросом по названию (AliasЕдИм -> AliasЕдИмId и т.п.);
     • хвосты пробелов от nchar обрезаются (RTRIM), иначе связи не найдутся;
     • money/smallmoney/float приводятся к decimal;
     • явные id сохраняются через SET IDENTITY_INSERT.

   Порядок: справочники -> деньги/метки -> накладные -> движения -> аудит.
   Скрипт безопасен для повторного запуска.

   Если эталон лежит на другом сервере: выгрузите его через SSMS (Generate
   Scripts + Data) и замените префикс [Sh_29-06-25].dbo на dbo.
   ========================================================================== */

USE [SkladEtalon];
GO
SET ANSI_NULLS ON;
GO
SET XACT_ABORT ON;
GO
BEGIN TRANSACTION;
GO

/* ===========================================================================
   1. СПРАВОЧНИКИ
   =========================================================================== */
/* --- Скдад --- */
INSERT INTO dbo.[Скдад] ([id], [название], [alias])
SELECT s.[id],
       RTRIM(s.[название]),
       RTRIM(s.[alias])
FROM [Sh_29-06-25].dbo.[Скдад] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.[Скдад] AS t
    WHERE t.[id] = s.[id])
GO

/* --- ОсновнаяЕдИзмерения --- */
INSERT INTO dbo.[ОсновнаяЕдИзмерения] ([название])
SELECT RTRIM(s.[название])
FROM [Sh_29-06-25].dbo.[ОсновнаяЕдИзмерения] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.[ОсновнаяЕдИзмерения] AS t
    WHERE t.[название] = s.[название])
GO

/* --- AliasЕдИзмТовар --- */
INSERT INTO dbo.[AliasЕдИзмТовар] ([название])
SELECT RTRIM(s.[название])
FROM [Sh_29-06-25].dbo.[AliasЕдИзмТовар] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.[AliasЕдИзмТовар] AS t
    WHERE t.[название] = s.[название])
GO

/* --- АтрибутыМатерьяла --- */
INSERT INTO dbo.[АтрибутыМатерьяла] ([название])
SELECT RTRIM(s.[название])
FROM [Sh_29-06-25].dbo.[АтрибутыМатерьяла] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.[АтрибутыМатерьяла] AS t
    WHERE t.[название] = s.[название])
GO

/* --- Матерьял --- */
INSERT INTO dbo.[Матерьял] ([название])
SELECT RTRIM(s.[название])
FROM [Sh_29-06-25].dbo.[Матерьял] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.[Матерьял] AS t
    WHERE t.[название] = s.[название])
GO

/* --- Поставщик --- */
SET IDENTITY_INSERT dbo.[Поставщик] ON;
INSERT INTO dbo.[Поставщик] ([id], [город])
SELECT s.[id],
       RTRIM(s.[город])
FROM [Sh_29-06-25].dbo.[Поставщик] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.[Поставщик] AS t
    WHERE t.[id] = s.[id])
GO

SET IDENTITY_INSERT dbo.[Поставщик] OFF;
/* --- АудитМетка --- */
INSERT INTO dbo.[АудитМетка] ([номерГруппы], [датаНазначенияНомера])
SELECT RTRIM(s.[номерГруппы]),
       s.[датаНазначенияНомера]
FROM [Sh_29-06-25].dbo.[АудитМетка] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.[АудитМетка] AS t
    WHERE t.[номерГруппы] = s.[номерГруппы])
GO

/* --- Клиент (id сохраняем) --- */
SET IDENTITY_INSERT dbo.[Клиент] ON;
INSERT INTO dbo.[Клиент] ([id], [псевдоним], [ФИО], [телефон], [город], [ПределКредита])
SELECT s.[id],
       s.[псевдоним],
       s.[ФИО],
       s.[телефон],
       RTRIM(s.[город]),
       CAST(s.[ПределКредита] AS decimal(18,2))
FROM [Sh_29-06-25].dbo.[Клиент] AS s;
SET IDENTITY_INSERT dbo.[Клиент] OFF;
GO

/* --- Номенклатура (строковые ссылки -> int) --- */
SET IDENTITY_INSERT dbo.[Номенклатура] ON;
INSERT INTO dbo.[Номенклатура] ([id],[название],[основнаяЕдИзмеренияId],[матерьялId],[ценаОтпускаOснЕдИзм])
SELECT  s.[id],
        s.[название],
        eu.[Id],
        m.[Id],
        CAST(s.[ценаОтпускаOснЕдИзм] AS decimal(18,2))
FROM    [Sh_29-06-25].dbo.[Номенклатура] AS s
LEFT JOIN dbo.[ОсновнаяЕдИзмерения] AS eu ON eu.[название] = RTRIM(s.[основнаяЕдИзмерения])
LEFT JOIN dbo.[Матерьял] AS m ON m.[название] = RTRIM(s.[матерьял]);
SET IDENTITY_INSERT dbo.[Номенклатура] OFF;
GO

/* --- КлиентМеткаРасчет --- */
SET IDENTITY_INSERT dbo.[КлиентМеткаРасчет] ON;
INSERT INTO dbo.[КлиентМеткаРасчет] ([id], [клиентId], [дата], [меткаНомер], [Долг], [коментарий])
SELECT s.[id],
       s.[клиентId],
       s.[дата],
       RTRIM(s.[меткаНомер]),
       CAST(s.[Долг] AS decimal(18,2)),
       s.[коментарий]
FROM [Sh_29-06-25].dbo.[КлиентМеткаРасчет] AS s;
SET IDENTITY_INSERT dbo.[КлиентМеткаРасчет] OFF;
GO

/* --- КлиентДеньги --- */
SET IDENTITY_INSERT dbo.[КлиентДеньги] ON;
INSERT INTO dbo.[КлиентДеньги] ([id], [клиентId], [дата], [cуммаПринятыхДенег], [меткаId])
SELECT s.[id],
       s.[клиентId],
       s.[дата],
       CAST(s.[cуммаПринятыхДенег] AS decimal(18,2)),
       s.[меткаId]
FROM [Sh_29-06-25].dbo.[КлиентДеньги] AS s;
SET IDENTITY_INSERT dbo.[КлиентДеньги] OFF;
GO

/* --- НакладнаяПриход --- */
SET IDENTITY_INSERT dbo.[НакладнаяПриход] ON;
INSERT INTO dbo.[НакладнаяПриход] ([id], [поставщик], [дата], [номерНакладной], [суммаПонаклодной])
SELECT s.[id],
       s.[поставщик],
       s.[дата],
       s.[номерНакладной],
       CAST(s.[суммаПонаклодной] AS decimal(18,2))
FROM [Sh_29-06-25].dbo.[НакладнаяПриход] AS s;
SET IDENTITY_INSERT dbo.[НакладнаяПриход] OFF;
GO

/* --- НакладнаяРасход --- */
SET IDENTITY_INSERT dbo.[НакладнаяРасход] ON;
INSERT INTO dbo.[НакладнаяРасход] ([id], [номер], [дата], [клиентId], [суммаПонаклодной], [меткаId])
SELECT s.[id],
       s.[номер],
       s.[дата],
       s.[клиентId],
       CAST(s.[суммаПонаклодной] AS decimal(18,2)),
       s.[меткаId]
FROM [Sh_29-06-25].dbo.[НакладнаяРасход] AS s;
SET IDENTITY_INSERT dbo.[НакладнаяРасход] OFF;
GO

/* --- СкладПриход --- */
SET IDENTITY_INSERT dbo.[СкладПриход] ON;
INSERT INTO dbo.[СкладПриход] ([id], [номенклатура], [накладнаяId], [AliasЕдИзмId], [колAliasЕдИзм], [колОснЕдИзм], [ценаОснЕдИзм], [складId], [ценаAliasЕдИзм], [ОснЕдИзмId], [аудитНомерГруппыId], [тараРасходаId])
SELECT  s.[id],
        s.[номенклатура],
        s.[накладнаяId],
        au.[Id],
        CAST(s.[колAliasЕдИзм] AS decimal(18,3)),
        CAST(s.[колОснЕдИзм] AS decimal(18,3)),
        CAST(s.[ценаОснЕдИзм] AS decimal(18,2)),
        s.[складId],
        CAST(s.[ценаAliasЕдИзм] AS decimal(18,2)),
        eu.[Id],
        am.[Id],
        tr.[Id]
FROM    [Sh_29-06-25].dbo.[СкладПриход] AS s
LEFT JOIN dbo.[AliasЕдИзмТовар] AS au ON au.[название] = RTRIM(s.[AliasЕдИзм])
LEFT JOIN dbo.[ОсновнаяЕдИзмерения] AS eu ON eu.[название] = RTRIM(s.[ОснЕдИзм])
LEFT JOIN dbo.[АудитМетка] AS am ON am.[номерГруппы] = RTRIM(s.[аудитНомерГруппы])
LEFT JOIN dbo.[AliasЕдИзмТовар] AS tr ON tr.[название] = RTRIM(s.[тараРасхода])
   ;
SET IDENTITY_INSERT dbo.[СкладПриход] OFF;
GO

/* --- СкладРасход --- */
SET IDENTITY_INSERT dbo.[СкладРасход] ON;
INSERT INTO dbo.[СкладРасход] ([id], [номенклатура], [накладнаяId], [AliasЕдИзмId], [колAliasЕдИзм], [колОснЕдИзм], [ценаОснЕдИзм], [складId], [ценаAliasЕдИзм], [ОснЕдИзмId], [аудитНомерГруппыId], [тараРасходаId])
SELECT  s.[id],
        s.[номенклатура],
        s.[накладнаяId],
        au.[Id],
        CAST(s.[колAliasЕдИзм] AS decimal(18,3)),
        CAST(s.[колОснЕдИзм] AS decimal(18,3)),
        CAST(s.[ценаОснЕдИзм] AS decimal(18,2)),
        s.[складId],
        CAST(s.[ценаAliasЕдИзм] AS decimal(18,2)),
        eu.[Id],
        am.[Id],
        tr.[Id]
FROM    [Sh_29-06-25].dbo.[СкладРасход] AS s
LEFT JOIN dbo.[AliasЕдИзмТовар] AS au ON au.[название] = RTRIM(s.[AliasЕдИзм])
LEFT JOIN dbo.[ОсновнаяЕдИзмерения] AS eu ON eu.[название] = RTRIM(s.[ОснЕдИзм])
LEFT JOIN dbo.[АудитМетка] AS am ON am.[номерГруппы] = RTRIM(s.[аудитНомерГруппы])
LEFT JOIN dbo.[AliasЕдИзмТовар] AS tr ON tr.[название] = RTRIM(s.[тараРасхода])
   ;
SET IDENTITY_INSERT dbo.[СкладРасход] OFF;
GO

/* --- РезультатАудита --- */
SET IDENTITY_INSERT dbo.[РезультатАудита] ON;
INSERT INTO dbo.[РезультатАудита] ([id], [номерГруппыId], [номенклатура], [ОснЕдИзмId], [кол], [недостача])
SELECT s.[id], am.[Id], s.[номенклатура], eu.[Id],
       CAST(s.[кол] AS decimal(18,3)),
       CAST(s.[недостача] AS decimal(18,3))
FROM [Sh_29-06-25].dbo.[РезультатАудита] AS s
LEFT JOIN dbo.[АудитМетка] AS am ON am.[номерГруппы] = RTRIM(s.[номерГруппы])
LEFT JOIN dbo.[ОсновнаяЕдИзмерения] AS eu ON eu.[название] = RTRIM(s.[ОснЕдИзм]);
SET IDENTITY_INSERT dbo.[РезультатАудита] OFF;
GO

/* --- ПРИХОД --- */
INSERT INTO dbo.[ПРИХОД] ([номенклатура],[кол],[аудитНомерГруппы])
SELECT s.[номенклатура],
       CAST(s.[кол] AS decimal(18,3)),
       am.[Id]
FROM [Sh_29-06-25].dbo.[ПРИХОД] AS s
LEFT JOIN dbo.[АудитМетка] AS am ON am.[номерГруппы] = RTRIM(s.[аудитНомерГруппы]);
GO

/* --- РАСХОД --- */
INSERT INTO dbo.[РАСХОД] ([номенклатура],[кол],[аудитНомерГруппы])
SELECT s.[номенклатура],
       CAST(s.[кол] AS decimal(18,3)),
       am.[Id]
FROM [Sh_29-06-25].dbo.[РАСХОД] AS s
LEFT JOIN dbo.[АудитМетка] AS am ON am.[номерГруппы] = RTRIM(s.[аудитНомерГруппы]);
GO

/* --- АУДИД --- */
INSERT INTO dbo.[АУДИД] ([Номенкл], [количест], [едИзм], [имя])
SELECT s.[Номенкл],
       CAST(s.[количест] AS decimal(18,3)),
       RTRIM(s.[едИзм]),
       RTRIM(s.[имя])
FROM [Sh_29-06-25].dbo.[АУДИД] AS s;
GO

/* --- ХарактеристикаНоменклатуры (в эталоне пусто) --- */
INSERT INTO dbo.[ХарактеристикаНоменклатуры] ([id],[номенклатураId],[атрибут],[значениеId])
SELECT s.[id], s.[номенклатураId], RTRIM(s.[атрибут]), am.[Id]
FROM [Sh_29-06-25].dbo.[ХарактеристикаНоменклатуры] AS s
LEFT JOIN dbo.[АтрибутыМатерьяла] AS am ON am.[название] = RTRIM(s.[значение]);
GO

/* --- СвязиAlisEдИзмНоменклатура (в эталоне пусто) --- */
INSERT INTO dbo.[СвязиAlisEдИзмНоменклатура] ([aliasЕдИзмId], [idНоменклатура], [коэфициэнтПриведенияКоснЕдИзм], [ценаОтпускаДругая], [ценаОтпуска], [оснЕдИзмId])
SELECT au.[Id], s.[idНоменклатура],
       CAST(s.[коэфициэнтПриведенияКоснЕдИзм] AS decimal(18,6)),
       s.[ценаОтпускаДругая],
       CAST(s.[ценаОтпуска] AS decimal(18,2)),
       eu.[Id]
FROM [Sh_29-06-25].dbo.[СвязиAlisEдИзмНоменклатура] AS s
LEFT JOIN dbo.[AliasЕдИзмТовар] AS au ON au.[название] = RTRIM(s.[aliasЕдИзм])
LEFT JOIN dbo.[ОсновнаяЕдИзмерения] AS eu ON eu.[название] = RTRIM(s.[оснЕдИзм]);
GO

COMMIT TRANSACTION;
GO

/* --- Итог --- */
SELECT N'Скдад' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[Скдад];
SELECT N'Матерьял' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[Матерьял];
SELECT N'ОсновнаяЕдИзмерения' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[ОсновнаяЕдИзмерения];
SELECT N'AliasЕдИмТовар' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[AliasЕдИзмТовар];
SELECT N'АудитМетка' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[АудитМетка];
SELECT N'Поставщик' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[Поставщик];
SELECT N'Клиент' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[Клиент];
SELECT N'Номенклатура' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[Номенклатура];
SELECT N'КлиентМеткаРасчет' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[КлиентМеткаРасчет];
SELECT N'КлиентДеньги' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[КлиентДеньги];
SELECT N'НакладнаяПриход' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[НакладнаяПриход];
SELECT N'НакладнаяРасход' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[НакладнаяРасход];
SELECT N'СкладПриход' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[СкладПриход];
SELECT N'СкладРасход' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[СкладРасход];
SELECT N'РезультатАудита' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[РезультатАудита];
SELECT N'ПРИХОД' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[ПРИХОД];
SELECT N'РАСХОД' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[РАСХОД];
SELECT N'АУДИД' AS [Таблица], COUNT(*) AS [Строк] FROM dbo.[АУДИД];
GO

PRINT N'Данные эталона перенесены в SkladEtalon';
GO