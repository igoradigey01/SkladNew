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

USE [Sklad-v3];
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
INSERT INTO dbo.ЕдИзмерения (Название, Сокращение)
SELECT RTRIM(s.[название]), RTRIM(s.[название])
FROM [Sh_29-06-25].dbo.[ОсновнаяЕдИзмерения] AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.ЕдИзмерения AS t WHERE t.Название = RTRIM(s.[название]));
GO

/* --- Материалы --- */
INSERT INTO dbo.ВидНоменклатуры (Название, Описание)
SELECT RTRIM(s.[название]), NULL
FROM [Sh_29-06-25].dbo.[Матерьял] AS s
WHERE NOT EXISTS (SELECT 1 FROM dbo.ВидНоменклатуры AS t WHERE t.Название = RTRIM(s.[название]));
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
INSERT INTO dbo.Номенклатура (Id, Guid, Код, Название, ТипId, ВидНоменклатурыId,
                              БазоваяЕдИзмId, ЦенаПродажная, Активен)
SELECT s.[id],
       CONVERT(uniqueidentifier,
               CONCAT('4E000000-0000-0000-0000-',
                      RIGHT('000000000000' + CAST(s.[id] AS varchar(12)), 12))),
       CONCAT('НМ-', RIGHT('00000' + CAST(s.[id] AS varchar(10)), 5)),
       s.[название],
       1,                                   -- ТипId = 1 (Товар)
       вн.[Id],
       еи.[Id],
       CAST(s.[ценаОтпускаOснЕдИзм] AS decimal(18,2)),
       1
FROM [Sh_29-06-25].dbo.[Номенклатура] AS s
LEFT JOIN dbo.ВидНоменклатуры AS вн ON вн.[Название] = RTRIM(s.[матерьял])
LEFT JOIN dbo.ЕдИзмерения     AS еи ON еи.[Название] = RTRIM(s.[основнаяЕдИзмерения])
WHERE NOT EXISTS (SELECT 1 FROM dbo.Номенклатура AS t WHERE t.Id = s.[id]);
SET IDENTITY_INSERT dbo.Номенклатура OFF;
GO

/* --- Категории (в эталоне нет) --- */
INSERT INTO dbo.КатегорияНоменклатуры (Название)
SELECT v.Название
FROM (VALUES (N'Товар'), (N'Материал'), (N'Услуга')) AS v (Название)
WHERE NOT EXISTS (SELECT 1 FROM dbo.КатегорияНоменклатуры AS t WHERE t.[Название] = v.[Название]);
GO

/* --- Группы номенклатуры: дерево, корень + четыре ветки --- */
INSERT INTO dbo.ГруппаНоменклатуры (Название, РодительId)
SELECT N'Все номенклатуры', NULL
WHERE NOT EXISTS (SELECT 1 FROM dbo.ГруппаНоменклатуры WHERE [Название] = N'Все номенклатуры');
GO

INSERT INTO dbo.ГруппаНоменклатуры (Название, РодительId)
SELECT g.Название, r.[Id]
FROM (VALUES (N'Ткани'), (N'Плитные материалы'), (N'Крепёж'), (N'Фурнитура')) AS g (Название)
CROSS JOIN dbo.ГруппаНоменклатуры AS r
WHERE r.[Название] = N'Все номенклатуры'
  AND NOT EXISTS (SELECT 1 FROM dbo.ГруппаНоменклатуры AS t WHERE t.[Название] = g.[Название]);
GO

/* --- Характеристики по видам номенклатуры (в эталоне пусто) ---
   Характеристика принадлежит виду: у ткани — цвет и плотность,
   у ДСП — толщина и цвет кромки, у крепежа — диаметр. */
INSERT INTO dbo.Характеристика (ВидНоменклатурыId, Название, Порядок, Обязательный)
SELECT вн.[Id], х.Название, х.Порядок, х.Обязательный
FROM (VALUES (N'Ткань',    N'Цвет',        1, 1),
             (N'Ткань',    N'Плотность',   2, 0),
             (N'ДСП',      N'Толщина',     1, 1),
             (N'ДСП',      N'Цвет кромки', 2, 0),
             (N'фанера',   N'Толщина',     1, 0),
             (N'саморезы', N'Диаметр',     1, 0),
             (N'болт',     N'Диаметр',     1, 0)) AS х(Вид, Название, Порядок, Обязательный)
INNER JOIN dbo.ВидНоменклатуры AS вн ON вн.[Название] = х.Вид
WHERE NOT EXISTS (SELECT 1 FROM dbo.Характеристика AS t
                  WHERE t.[ВидНоменклатурыId] = вн.[Id] AND t.[Название] = х.Название);
GO

/* --- Значения характеристик номенклатуры (в эталоне пусто) --- */
INSERT INTO dbo.ЗначениеХарактеристики (НоменклатураId, ХарактеристикаId, Значение)
SELECT н.[Id], х.[Id], RTRIM(s.[значение])
FROM [Sh_29-06-25].dbo.[ХарактеристикаНоменклатуры] AS s
LEFT JOIN dbo.Номенклатура   AS н ON н.[Id] = s.[номенклатураId]
LEFT JOIN dbo.Характеристика AS х ON х.[Название] = RTRIM(s.[атрибут])
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ЗначениеХарактеристики AS t
    WHERE t.НоменклатураId = н.[Id]
      AND t.ХарактеристикаId = х.[Id]);
GO

COMMIT TRANSACTION;
GO

/* --- Итог --- */
SELECT N'Склад'          AS [Справочник], COUNT(*) AS [Записей] FROM dbo.Склад
UNION ALL SELECT N'Ед. измерения',  COUNT(*) FROM dbo.ЕдИзмерения
UNION ALL SELECT N'ВидНоменклатуры',       COUNT(*) FROM dbo.ВидНоменклатуры
UNION ALL SELECT N'Номенклатура',   COUNT(*) FROM dbo.Номенклатура
UNION ALL SELECT N'Группа',         COUNT(*) FROM dbo.ГруппаНоменклатуры
UNION ALL SELECT N'Категория',      COUNT(*) FROM dbo.КатегорияНоменклатуры
UNION ALL SELECT N'Характеристика', COUNT(*) FROM dbo.Характеристика
UNION ALL SELECT N'ЗначениеХар-ки', COUNT(*) FROM dbo.ЗначениеХарактеристики
UNION ALL SELECT N'Клиент',         COUNT(*) FROM dbo.Клиент
UNION ALL SELECT N'Поставщик',      COUNT(*) FROM dbo.Поставщик
;
GO

PRINT N'Справочники (Sklad-v3) наполнены';
GO
