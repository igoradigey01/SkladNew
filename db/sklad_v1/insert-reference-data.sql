/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА: наполнение справочников
   ----------------------------------------------------------------------------
   Данные берём из эталона Sh_29-06-25, чтобы новая система стартовала с тем же
   словарём, что и работающая (202 позиции номенклатуры, 53 клиента,
   15 материалов, 2 склада, 5 единиц измерения, 5 упаковок, 9 поставщиков).

   Порядок важен: сначала справочники, потом номенклатура — она ссылается на
   них по названию. Скрипт можно запускать повторно (NOT EXISTS).

   Кадров (Сотрудник, Специальность) здесь нет — они в отдельной базе
   Sklad_v1_HR, связь с ними идёт через СотрудникGuid.
   ========================================================================== */

USE [Sklad_v1];
GO

SET NOCOUNT ON;
GO
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO
/* ===========================================================================
   1. СКЛАДЫ
   Id сохраняем из эталона — на него ссылаются движения.
   =========================================================================== */
SET IDENTITY_INSERT dbo.Склад ON;
INSERT INTO dbo.Склад (Id, Название, Псевдоним)
SELECT s.[id], RTRIM(s.[название]), RTRIM(s.[alias])
FROM [Sh_29-06-25].dbo.[Скдад] AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Склад AS t WHERE t.Id = s.[id]);
SET IDENTITY_INSERT dbo.Склад OFF;
GO

/* --- Единицы измерения --- */
INSERT INTO dbo.ЕдИзмерения (Название)
SELECT RTRIM(s.[название])
FROM [Sh_29-06-25].dbo.[ОсновнаяЕдИзмерения] AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.ЕдИзмерения AS t WHERE t.Название = RTRIM(s.[название]));
GO

/* --- Материалы --- */
INSERT INTO dbo.Материал (Название)
SELECT RTRIM(s.[название])
FROM [Sh_29-06-25].dbo.[Матерьял] AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Материал AS t WHERE t.Название = RTRIM(s.[название]));
GO

/* --- Поставщики (в эталоне хранится только город — он же и название) --- */
SET IDENTITY_INSERT dbo.Поставщик ON;
INSERT INTO dbo.Поставщик (Id, Название, Город)
SELECT s.[id], RTRIM(s.[город]), RTRIM(s.[город])
FROM [Sh_29-06-25].dbo.[Поставщик] AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Поставщик AS t WHERE t.Id = s.[id]);
SET IDENTITY_INSERT dbo.Поставщик OFF;
GO

/* --- Клиенты --- */
SET IDENTITY_INSERT dbo.Клиент ON;
INSERT INTO dbo.Клиент (Id, Псевдоним, Фио, Телефон, Город, ПределКредита)
SELECT s.[id], s.[псевдоним], s.[ФИО], s.[телефон], s.[город],
       CAST(s.[ПределКредита] AS decimal(18,2))
FROM [Sh_29-06-25].dbo.[Клиент] AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Клиент AS t WHERE t.Id = s.[id]);
SET IDENTITY_INSERT dbo.Клиент OFF;
GO

/* ===========================================================================
   2. НОМЕНКЛАТУРА (товары)
   =========================================================================== */
SET IDENTITY_INSERT dbo.Номенклатура ON;
INSERT INTO dbo.Номенклатура (Id, Название, МатериалId, ЕдИзмеренияId, Цена)
SELECT s.[id], s.[название], m.[Id], u.[Id],
       CAST(s.[ценаОтпускаOснЕдИзм] AS decimal(18,2))
FROM [Sh_29-06-25].dbo.[Номенклатура] AS s
LEFT JOIN dbo.Материал    AS m ON m.[Название] = RTRIM(s.[матерьял])
LEFT JOIN dbo.ЕдИзмерения AS u ON u.[Название] = RTRIM(s.[основнаяЕдИзмерения])
WHERE NOT EXISTS (SELECT 1 FROM dbo.Номенклатура AS t WHERE t.Id = s.[id]);
SET IDENTITY_INSERT dbo.Номенклатура OFF;
GO

/* --- Характеристики номенклатуры (в эталоне пусто) --- */
INSERT INTO dbo.ХарактеристикаНоменклатуры (НоменклатураId, ХарактеристикаId, Значение)
SELECT s.[номенклатураId], RTRIM(s.[атрибут]), RTRIM(s.[значение])
FROM [Sh_29-06-25].dbo.[ХарактеристикаНоменклатуры] AS s
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ХарактеристикаНоменклатуры AS t
    WHERE t.НоменклатураId = s.[номенклатураId]
      AND t.Атрибут = RTRIM(s.[атрибут]));
GO

COMMIT TRANSACTION;
GO

/* --- Итог --- */
SELECT N'Склад'          AS [Справочник], COUNT(*) AS [Записей] FROM dbo.Склад
UNION ALL SELECT N'Ед. измерения',  COUNT(*) FROM dbo.ЕдИзмерения
UNION ALL SELECT N'Материал',       COUNT(*) FROM dbo.Материал
UNION ALL SELECT N'Номенклатура',   COUNT(*) FROM dbo.Номенклатура
UNION ALL SELECT N'Клиент',         COUNT(*) FROM dbo.Клиент
UNION ALL SELECT N'Поставщик',      COUNT(*) FROM dbo.Поставщик
;
GO

PRINT N'Справочники (Sklad_v1) наполнены';
GO
