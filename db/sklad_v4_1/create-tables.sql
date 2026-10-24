/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА Sklad-v4.1: таблицы
   ----------------------------------------------------------------------------
   Кадры вынесены в отдельную базу Sklad_v1_HR (таблицы Сотрудник,
   Специальность) — см. ../sklad_v1_hr/. Связь основной базы с кадрами идёт
   через Guid: в Приход/Расход/Деньги лежит СотрудникGuid, в Sklad_v1_HR у
   сотрудника есть уникальный Guid. FOREIGN KEY между базами невозможен,
   поэтому целостность проверяет приложение и периодическая проверка
   (запрос — в database-schema.md).

   Имена таблиц — на русском (как в эталоне Sh_29-06-25 и в правилах проекта:
   БД по-русски, C# и API по-английски).

   ЧТО ИЗМЕНИЛОСЬ ОТНОСИТЕЛЬНО Sklad-v4
   Ядро (Приход / Расход / Деньги на общем UETR), ревизии, справочники и
   система характеристик перенесены без изменений. Добавлен слой
   взаиморасчётов с клиентом:

     Сделка — договорённость с клиентом. КодСделки (uniqueidentifier) —
       сквозной ключ, НомерСделки (R-000001) — человекочитаемый бизнес-ключ,
       уникальный и неизменяемый. Долг не хранится: он вычисляется в
       представлении vw_БалансСделки.
     НакладнаяРасход — шапка отгрузки: номер, дата, клиент, КодСделки.
       Строки накладной — это Расход (одна строка = одна позиция).
     Расход.НакладнаяId — ссылка строки на шапку, ON DELETE CASCADE.
     Деньги.КодСделки — привязка платежа клиента к сделке.

   Итоговая цепочка:
       Клиент -> Сделка -> НакладнаяРасход -> Расход
                        -> Деньги

   ЧТО БЫЛО ИЗМЕНЕНО В Sklad-v4 (система характеристик)
   Система характеристик переделана по внешним стандартам:

     ТипЗначения (Attribute type) — тип атрибута: Text, Number, Date, Bool,
       List. Задаёт формат значения и допустимые границы.
     Характеристика (Attribute) — атрибут вида номенклатуры: тип значения,
       Минимум / Максимум, МаксимальнаяДлина, Минимальная / МаксимальнаяДата,
       ЗначениеПоУмолчанию (наследуется всеми позициями вида).
     СписокЗначений — допустимые значения для типа List.
     ЗначениеХарактеристики — значение атрибута у конкретной позиции;
       переопределяет унаследованное умолчание.

   Проверку значений по типу выполняют триггеры (create-triggers.sql):
   в CHECK-ограничении нельзя использовать подзапросы, а тип значения лежит
   в другой таблице, поэтому типозависимые правила вынесены в триггеры.

   Что взято откуда:
     • справочники и складская логика — из эталона Sh_29-06-25
       (номенклатура, материалы, единицы измерения, склады, контрагенты);
     • движения и деньги — своё ядро: Приход / Расход / Деньги,
       связанные сквозным идентификатором UETR;
     • ревизии — Ревизия + Расхождение с вычисляемой дельтой;
     • атрибуты — по внешним стандартам (см. выше);
     • взаиморасчёты — развитие эталонных КлиентМеткаРасчет, НакладнаяРасход
       и КлиентДеньги: та же логика, но связь через КодСделки и без
       хранения вычисляемого долга.

   Сопоставление с эталоном:
      Склад                  <- Скдад
      ВидНоменклатуры               <- Матерьял
      Номенклатура           <- Номенклатура
      ЗначениеХарактеристики <- ЗначениеХарактеристики
      (механизм упаковочных единиц не переносится — см. раздел «Почему без упаковки»)
      Клиент / Поставщик     <- Клиент / Поставщик
      Сделка                 <- КлиентМеткаРасчет (без хранения Долга)
      НакладнаяРасход        <- НакладнаяРасход (шапка; строки — в Расход)
      Приход / Расход / Деньги <- СкладПриход / СкладРасход / КлиентДеньги
      Ревизия / Расхождение  <- АудитМетка / РезультатАудита
      (Сотрудник, Специальность — не здесь, а в базе Sklad_v1_HR)
      ТипЗначения, СписокЗначений — новые сущности: во внешних стандартах
      это Attribute type (тип атрибута) и список допустимых значений атрибута

   ПОЧЕМУ БЕЗ УПАКОВКИ
   В эталоне механизм второй единицы (AliasЕдИзмТовар + коэффициенты перевода)
   был заведён, но не работал: kolAliasЕдИм = 0 во всех 6773 строках движений,
   таблица коэффициентов пуста, ценаAliasЕдИм повторяла цену в основной единице.
   В новой схеме он не переносится: одна единица измерения на позицию, отпуск
   ведётся в ней же. Если упаковка понадобится — это отдельная задача с
   коэффициентами перевода, а не пустой справочник.
   ========================================================================== */

USE [Sklad-v4.1];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* ===========================================================================
   РАЗДЕЛ 0. СБРОС (порядок — от зависимых к зависимым)
   =========================================================================== */
IF OBJECT_ID(N'dbo.Приход',                  N'U') IS NOT NULL DROP TABLE dbo.Приход;
IF OBJECT_ID(N'dbo.Расход',                   N'U') IS NOT NULL DROP TABLE dbo.Расход;
IF OBJECT_ID(N'dbo.Деньги',                   N'U') IS NOT NULL DROP TABLE dbo.Деньги;
IF OBJECT_ID(N'dbo.НакладнаяРасход',          N'U') IS NOT NULL DROP TABLE dbo.НакладнаяРасход;
IF OBJECT_ID(N'dbo.Сделка',                   N'U') IS NOT NULL DROP TABLE dbo.Сделка;
IF OBJECT_ID(N'dbo.Расхождение',              N'U') IS NOT NULL DROP TABLE dbo.Расхождение;
IF OBJECT_ID(N'dbo.Ревизия',                  N'U') IS NOT NULL DROP TABLE dbo.Ревизия;
IF OBJECT_ID(N'dbo.ЗначениеХарактеристики', N'U') IS NOT NULL DROP TABLE dbo.ЗначениеХарактеристики;
IF OBJECT_ID(N'dbo.Номенклатура',             N'U') IS NOT NULL DROP TABLE dbo.Номенклатура;
IF OBJECT_ID(N'dbo.СписокЗначений',           N'U') IS NOT NULL DROP TABLE dbo.СписокЗначений;
IF OBJECT_ID(N'dbo.Характеристика',          N'U') IS NOT NULL DROP TABLE dbo.Характеристика;
IF OBJECT_ID(N'dbo.ТипЗначения',              N'U') IS NOT NULL DROP TABLE dbo.ТипЗначения;
IF OBJECT_ID(N'dbo.КатегорияНоменклатуры',  N'U') IS NOT NULL DROP TABLE dbo.КатегорияНоменклатуры;
IF OBJECT_ID(N'dbo.ГруппаНоменклатуры',      N'U') IS NOT NULL DROP TABLE dbo.ГруппаНоменклатуры;
IF OBJECT_ID(N'dbo.ВидНоменклатуры',         N'U') IS NOT NULL DROP TABLE dbo.ВидНоменклатуры;
IF OBJECT_ID(N'dbo.ЕдИзмерения',              N'U') IS NOT NULL DROP TABLE dbo.ЕдИзмерения;
IF OBJECT_ID(N'dbo.Клиент',                   N'U') IS NOT NULL DROP TABLE dbo.Клиент;
IF OBJECT_ID(N'dbo.Поставщик',                N'U') IS NOT NULL DROP TABLE dbo.Поставщик;
IF OBJECT_ID(N'dbo.Склад',                    N'U') IS NOT NULL DROP TABLE dbo.Склад;
GO

/* ===========================================================================
   РАЗДЕЛ 1. СПРАВОЧНИКИ
   =========================================================================== */

/* ---------------------------------------------------------------------------
   Склад — эталон «Скдад» (Основной, Ангар)
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Склад
(
    Id         int IDENTITY(1, 1) NOT NULL,
    Название   nvarchar(50) NOT NULL,
    Псевдоним  nvarchar(10) NOT NULL,

    CONSTRAINT PK_Склад PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Склад_Название UNIQUE (Название),
    CONSTRAINT UQ_Склад_Псевдоним UNIQUE (Псевдоним)
);
GO

/* ---------------------------------------------------------------------------
   ЕдИзмерения — эталон «ОсновнаяЕдИзмерения» (м, кг, шт, пачка, бухта).
   Название расширено до 50 символов, добавлено сокращение.
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.ЕдИзмерения
(
    Id         int IDENTITY(1, 1) NOT NULL,
    Название   nvarchar(50) NOT NULL,
    Сокращение nvarchar(10) NULL,

    CONSTRAINT PK_ЕдИзмерения PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_ЕдИзмерения_Название UNIQUE (Название)
);
GO

/* ---------------------------------------------------------------------------
   ВидНоменклатуры — эталон «Матерьял» (ДСП, фанера, ткань, крепёж ...).
   В v4 добавлены Активен и ДатаСоздания: вид — основа для характеристик,
   поэтому его можно отключить, не удаляя (иначе удаление потянуло бы за собой
   характеристики и значения позиций).
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.ВидНоменклатуры
(
    Id          int IDENTITY(1, 1) NOT NULL,
    Название    nvarchar(50) NOT NULL,
    Описание   nvarchar(500) NULL,
    Активен     bit NOT NULL CONSTRAINT DF_ВидНоменклатуры_Активен DEFAULT (1),
    ДатаСоздания datetime NOT NULL CONSTRAINT DF_ВидНоменклатуры_ДатаСоздания DEFAULT (GETDATE()),

    CONSTRAINT PK_ВидНоменклатуры PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_ВидНоменклатуры_Название UNIQUE (Название)
);
GO

/* ---------------------------------------------------------------------------
   ГруппаНоменклатуры — дерево групп (Ткани -> Ткани мебельные -> ...).
   РодительId NULL означает группу верхнего уровня.
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.ГруппаНоменклатуры
(
    Id         int IDENTITY(1, 1) NOT NULL,
    Название   nvarchar(100) NOT NULL,
    РодительId int NULL,

    CONSTRAINT PK_ГруппаНоменклатуры PRIMARY KEY CLUSTERED (Id),
    -- NO ACTION обязателен: ссылка на саму себя + CASCADE даёт
    -- «multiple cascade paths», который SQL Server не допускает
    CONSTRAINT FK_ГруппаНоменклатуры_Родитель FOREIGN KEY (РодительId)
        REFERENCES dbo.ГруппаНоменклатуры (Id)
        ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   КатегорияНоменклатуры — Товар / Материал / Услуга и т.п.
   Категория отвечает на вопрос «что это», ВидНоменклатуры — «из чего это».
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.КатегорияНоменклатуры
(
    Id       int IDENTITY(1, 1) NOT NULL,
    Название nvarchar(100) NOT NULL,

    CONSTRAINT PK_КатегорияНоменклатуры PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_КатегорияНоменклатуры_Название UNIQUE (Название)
);
GO

/* ---------------------------------------------------------------------------
   ТипЗначения — ТИП АТРИБУТА (Attribute type, по внешним стандартам).

   Тип определяет, какое значение может принимать характеристика и как его
   проверять:
     Text   — строка; проверяется длина (МаксимальнаяДлина у характеристики);
     Number — число; проверяется попадание в [Минимум, Максимум];
     Date   — дата;   проверяется попадание в [МинимальнаяДата, МаксимальнаяДата];
     Bool   — логическое значение: 0/1, true/false, да/нет;
     List   — значение из фиксированного списка (СписокЗначений).

   Это справочник, а не перечисление: новый тип можно завести, не меняя
   структуру таблиц (так же устроены типы атрибутов во внешних стандартах).
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.ТипЗначения
(
    Id         int IDENTITY(1, 1) NOT NULL,
    Название   nvarchar(50) NOT NULL,
    Описание   nvarchar(200) NULL,

    CONSTRAINT PK_ТипЗначения PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_ТипЗначения_Название UNIQUE (Название)
);
GO

/* ---------------------------------------------------------------------------
   Характеристика — АТРИБУТ (Attribute, по внешним стандартам).

   Принадлежит виду номенклатуры: у ткани это «Цвет», «Плотность», «Ширина»,
   у мебельного щита — «Толщина», «Цвет кромки». По внешним стандартам атрибут
   заводится один раз на категорию (здесь — вид номенклатуры) и наследуется
   всеми позициями этой категории.

   Правила значения задаются здесь же:
     • Number — Минимум / Максимум;
     • Text   — МаксимальнаяДлина;
     • Date   — МинимальнаяДата / МаксимальнаяДата;
     • List   — набор значений в СписокЗначений.

   ЗначениеПоУмолчанию наследуется всеми позициями вида; конкретная позиция
   переопределяет его в ЗначениеХарактеристики.

   Порядок заполнения для типа List: сначала сама характеристика, затем
   СписокЗначений, и только потом значение по умолчанию — иначе триггер
   не найдёт значения в списке (список ссылается на характеристику).
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Характеристика
(
    Id               int IDENTITY(1, 1) NOT NULL,
    ВидНоменклатурыId int NOT NULL,
    ТипЗначенияId    int NOT NULL,
    Название         nvarchar(100) NOT NULL,
    Описание         nvarchar(500) NULL,
    Порядок          int NULL,
    Обязательный     bit NOT NULL CONSTRAINT DF_Характеристика_Обязательный DEFAULT (0),

    -- Для числовых значений
    Минимум          decimal(18, 3) NULL,
    Максимум         decimal(18, 3) NULL,

    -- Для строк
    МаксимальнаяДлина int NULL,

    -- Для дат
    МинимальнаяДата  date NULL,
    МаксимальнаяДата date NULL,

    -- Значение по умолчанию: наследуется всеми позициями вида
    ЗначениеПоУмолчанию nvarchar(200) NULL,

    Активен          bit NOT NULL CONSTRAINT DF_Характеристика_Активен DEFAULT (1),
    ДатаСоздания     datetime NOT NULL CONSTRAINT DF_Характеристика_ДатаСоздания DEFAULT (GETDATE()),

    CONSTRAINT PK_Характеристика PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Характеристика_ВидНоменклатуры FOREIGN KEY (ВидНоменклатурыId)
        REFERENCES dbo.ВидНоменклатуры (Id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT FK_Характеристика_ТипЗначения FOREIGN KEY (ТипЗначенияId)
        REFERENCES dbo.ТипЗначения (Id)
        ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT UQ_Характеристика_Вид_Название UNIQUE (ВидНоменклатурыId, Название),
    CONSTRAINT CK_Характеристика_Диапазон CHECK (
        Минимум IS NULL OR Максимум IS NULL OR Максимум >= Минимум),
    CONSTRAINT CK_Характеристика_Даты CHECK (
        МинимальнаяДата IS NULL OR МаксимальнаяДата IS NULL
        OR МаксимальнаяДата >= МинимальнаяДата),
    CONSTRAINT CK_Характеристика_Длина CHECK (
        МаксимальнаяДлина IS NULL OR МаксимальнаяДлина > 0)
);
GO

/* ---------------------------------------------------------------------------
   СписокЗначений — допустимые значения характеристики типа List.
   Аналог фиксированного набора значений атрибута во внешних стандартах.
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.СписокЗначений
(
    Id               int IDENTITY(1, 1) NOT NULL,
    ХарактеристикаId int NOT NULL,
    Значение         nvarchar(200) NOT NULL,
    Порядок          int NULL,

    CONSTRAINT PK_СписокЗначений PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_СписокЗначений_Характеристика FOREIGN KEY (ХарактеристикаId)
        REFERENCES dbo.Характеристика (Id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT UQ_СписокЗначений_Характеристика_Значение UNIQUE (ХарактеристикаId, Значение)
);
GO

/* ---------------------------------------------------------------------------
   Номенклатура — эталон «Номенклатура» (198 позиций).
   Guid нужен для внешних систем и для связи по UETR; Код — человекочитаемый
   идентификатор для накладных.
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Номенклатура
(
    -- Идентификация
    Id                 int IDENTITY(1, 1) NOT NULL,
    Guid               uniqueidentifier NOT NULL CONSTRAINT DF_Номенклатура_Guid DEFAULT (NEWID()),
    Код                nvarchar(20) NOT NULL,
    Артикул            nvarchar(50) NULL,
    Название           nvarchar(200) NOT NULL,
    КраткоеНазвание    nvarchar(50) NULL,

    -- Классификация
    ГруппаId           int NULL,
    КатегорияId        int NULL,
    ТипId              int NOT NULL,             -- 1 = Товар, 2 = Услуга, 3 = Материал
    ВидНоменклатурыId  int NULL,

    -- Единицы измерения
    БазоваяЕдИзмId     int NOT NULL,

    -- Цены
    ЦенаЗакупочная     decimal(18, 2) NULL,
    ЦенаПродажная      decimal(18, 2) NULL,
    ЦенаОптовая       decimal(18, 2) NULL,

    -- Складские параметры
    МинимальныйОстаток decimal(18, 3) NULL,
    МаксимальныйОстаток decimal(18, 3) NULL,
    СрокГодности       int NULL,
    СтранаПроисхождения nvarchar(50) NULL,

    -- Дополнительно
    Фото               varbinary(MAX) NULL,
    Описание           nvarchar(1000) NULL,
    Активен            bit NOT NULL CONSTRAINT DF_Номенклатура_Активен DEFAULT (1),
    ДатаСоздания       datetime NOT NULL CONSTRAINT DF_Номенклатура_ДатаСоздания DEFAULT (GETDATE()),
    ДатаИзменения      datetime NULL,

    CONSTRAINT PK_Номенклатура PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Номенклатура_Guid UNIQUE (Guid),
    CONSTRAINT UQ_Номенклатура_Код UNIQUE (Код),
    CONSTRAINT FK_Номенклатура_БазоваяЕдИзм FOREIGN KEY (БазоваяЕдИзмId)
        REFERENCES dbo.ЕдИзмерения (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT FK_Номенклатура_ВидНоменклатуры FOREIGN KEY (ВидНоменклатурыId)
        REFERENCES dbo.ВидНоменклатуры (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT FK_Номенклатура_Группа FOREIGN KEY (ГруппаId)
        REFERENCES dbo.ГруппаНоменклатуры (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT FK_Номенклатура_Категория FOREIGN KEY (КатегорияId)
        REFERENCES dbo.КатегорияНоменклатуры (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT CK_Номенклатура_Тип CHECK (ТипId IN (1, 2, 3)),
    CONSTRAINT CK_Номенклатура_ЦенаЗакупочная CHECK (ЦенаЗакупочная IS NULL OR ЦенаЗакупочная >= 0),
    CONSTRAINT CK_Номенклатура_ЦенаПродажная CHECK (ЦенаПродажная IS NULL OR ЦенаПродажная >= 0),
    CONSTRAINT CK_Номенклатура_ЦенаОптовая CHECK (ЦенаОптовая IS NULL OR ЦенаОптовая >= 0),
    CONSTRAINT CK_Номенклатура_Остатки CHECK (
        (МинимальныйОстаток IS NULL OR МинимальныйОстаток >= 0)
    AND (МаксимальныйОстаток IS NULL OR МаксимальныйОстаток >= 0)
    AND (МинимальныйОстаток IS NULL OR МаксимальныйОстаток IS NULL
         OR МаксимальныйОстаток >= МинимальныйОстаток)),
    CONSTRAINT CK_Номенклатура_СрокГодности CHECK (СрокГодности IS NULL OR СрокГодности >= 0)
);
GO

/* ---------------------------------------------------------------------------
   ЗначениеХарактеристики — значение характеристики для конкретной позиции.
   Связка (номенклатура, характеристика) уникальна: один атрибут = одно
   значение. Если строки нет — действует ЗначениеПоУмолчанию характеристики
   (наследование от вида номенклатуры по внешним стандартам).
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.ЗначениеХарактеристики
(
    Id               int IDENTITY(1, 1) NOT NULL,
    НоменклатураId   int NOT NULL,
    ХарактеристикаId int NOT NULL,
    Значение         nvarchar(500) NOT NULL,
    ДатаИзменения    datetime NOT NULL CONSTRAINT DF_ЗначениеХарактеристики_ДатаИзменения DEFAULT (GETDATE()),

    CONSTRAINT PK_ЗначениеХарактеристики PRIMARY KEY CLUSTERED (Id),
    -- каскад только на удаление номенклатуры; ON UPDATE CASCADE здесь дал бы
    -- два пути каскада: ВидНоменклатуры -> Характеристика ->
    -- ЗначениеХарактеристики и Номенклатура -> Значение…
    CONSTRAINT FK_ЗначениеХарактеристики_Номенклатура FOREIGN KEY (НоменклатураId)
        REFERENCES dbo.Номенклатура (Id) ON UPDATE NO ACTION ON DELETE CASCADE,
    CONSTRAINT FK_ЗначениеХарактеристики_Характеристика FOREIGN KEY (ХарактеристикаId)
        REFERENCES dbo.Характеристика (Id) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_ЗначениеХарактеристики_Ном_Хар UNIQUE (НоменклатураId, ХарактеристикаId)
);
GO

/* ---------------------------------------------------------------------------
   Клиент — эталон «Клиент»
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Клиент
(
    Id            int IDENTITY(1, 1) NOT NULL,
    Псевдоним     nvarchar(25) NOT NULL,
    Фио           nvarchar(100) NOT NULL CONSTRAINT DF_Клиент_Фио DEFAULT N'none',
    Телефон       nvarchar(18) NOT NULL,
    Город         nvarchar(50) NOT NULL,
    ПределКредита decimal(18, 2) NOT NULL CONSTRAINT DF_Клиент_ПределКредита DEFAULT (10000),
    ДатаСоздания  datetime NOT NULL CONSTRAINT DF_Клиент_ДатаСоздания DEFAULT (GETDATE()),
    Активен       bit NOT NULL CONSTRAINT DF_Клиент_Активен DEFAULT (1),

    CONSTRAINT PK_Клиент PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Клиент_Псевдоним UNIQUE (Псевдоним),
    CONSTRAINT CK_Клиент_ПределКредита CHECK (ПределКредита >= 0)
);
GO

/* ---------------------------------------------------------------------------
   Поставщик — эталон «Поставщик» (в эталоне только город;
   при переносе название берём из города)
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Поставщик
(
    Id       int IDENTITY(1, 1) NOT NULL,
    Название nvarchar(50) NOT NULL,
    Город    nvarchar(50) NULL,
    Телефон  nvarchar(18) NULL,

    CONSTRAINT PK_Поставщик PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Поставщик_Название UNIQUE (Название)
);
GO

/* ===========================================================================
   РАЗДЕЛ 1.5. ВЗАИМОРАСЧЁТЫ: СДЕЛКА И ШАПКА НАКЛАДНОЙ
   =========================================================================== */

/* ---------------------------------------------------------------------------
   СДЕЛКА — договорённость с клиентом.

   КодСделки — сквозной идентификатор (uniqueidentifier): на него ссылаются
   накладные и деньги. НомерСделки — человекочитаемый бизнес-ключ вида
   R-000001: он уникален, неизменяем и не переиспользуется.

   Номер выдаёт последовательность seq_НомерСделки через DEFAULT: это
   concurrency-safe и не требует вычисляемой колонки. Последовательность
   сбрасывается после переноса данных (см. migrate-settlements...).

   LegacyId — id метки в эталоне Sh_29-06-25. Нужен только для аудита
   переноса и сверки; в бизнес-логике не участвует.

   Долг здесь НЕ хранится: он вычисляется в vw_БалансСделки. В эталоне
   хранимый Долг у 70 из 150 сделок не сходился с движениями.
   ------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.seq_НомерСделки', N'SO') IS NULL
    CREATE SEQUENCE dbo.seq_НомерСделки AS int START WITH 1 INCREMENT BY 1;
GO

CREATE TABLE dbo.Сделка
(
    КодСделки    uniqueidentifier NOT NULL,
    НомерСделки  nvarchar(20) NOT NULL
                 CONSTRAINT DF_Сделка_НомерСделки
                 DEFAULT ('R-' + RIGHT('000000' + CAST(NEXT VALUE FOR dbo.seq_НомерСделки AS varchar(10)), 6)),
    КлиентId     int NOT NULL,
    Дата         datetime NOT NULL CONSTRAINT DF_Сделка_Дата DEFAULT (GETDATE()),
    Комментарий  nvarchar(500) NULL,
    LegacyId     int NULL,
    ДатаСоздания datetime NOT NULL CONSTRAINT DF_Сделка_ДатаСоздания DEFAULT (GETDATE()),
    Активна      bit NOT NULL CONSTRAINT DF_Сделка_Активна DEFAULT (1),

    CONSTRAINT PK_Сделка PRIMARY KEY CLUSTERED (КодСделки),
    CONSTRAINT UQ_Сделка_НомерСделки UNIQUE (НомерСделки),
    CONSTRAINT FK_Сделка_Клиент FOREIGN KEY (КлиентId)
        REFERENCES dbo.Клиент (Id) ON UPDATE CASCADE ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   НАКЛАДНАЯ РАСХОД — шапка отгрузки.

   Одна накладная = один UETR = много строк в Расход. UETR переносится из
   эталона без изменений, поэтому шапку и строки можно сопоставить один
   в один. Номер накладной в эталоне не уникален, поэтому UNIQUE на него не
   ставится — только индекс.

   Сумма накладной не хранится: она вычисляется как сумма строк Расход.
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.НакладнаяРасход
(
    Id           int IDENTITY(1, 1) NOT NULL,
    UETR         uniqueidentifier NOT NULL,
    Номер        nvarchar(20) NOT NULL,
    Дата         datetime NOT NULL,
    КлиентId     int NOT NULL,
    КодСделки    uniqueidentifier NOT NULL,
    Комментарий  nvarchar(500) NULL,
    ДатаСоздания datetime NOT NULL CONSTRAINT DF_НакладнаяРасход_ДатаСоздания DEFAULT (GETDATE()),

    CONSTRAINT PK_НакладнаяРасход PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_НакладнаяРасход_UETR UNIQUE (UETR),
    CONSTRAINT FK_НакладнаяРасход_Клиент FOREIGN KEY (КлиентId)
        REFERENCES dbo.Клиент (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT FK_НакладнаяРасход_Сделка FOREIGN KEY (КодСделки)
        REFERENCES dbo.Сделка (КодСделки) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ===========================================================================
   РАЗДЕЛ 2. ЯДРО: ПРИХОД / РАСХОД / ДЕНЬГИ
   Три таблицы связаны сквозным идентификатором UETR: одна операция =
   один UETR = все строки документа.
   =========================================================================== */

/* ---------------------------------------------------------------------------
   ПРИХОД

   Type:
     1 — закупка у поставщика
     2 — возврат от клиента
     3 — выпуск собственного производства
     4 — перемещение между складами
     5 — инвентаризационная прибавка

   Количество и цена — в основной единице измерения (ЕдИзмерения).
   Упаковочных единиц нет: см. раздел «Почему без упаковки» в database-schema.md.
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Приход
(
    Id             int IDENTITY(1, 1) NOT NULL,
    UETR           uniqueidentifier NOT NULL,
    Date           datetime NOT NULL,
    Type           int NOT NULL,
    ПоставщикId    int NOT NULL,
    НоменклатураId int NOT NULL,
    СкладId        int NOT NULL,
    Quantity       decimal(18, 3) NOT NULL,
    Price          decimal(18, 2) NOT NULL,
    Sum            decimal(18, 2) NOT NULL,
    СотрудникGuid  uniqueidentifier NULL,      -- -> Sklad_v1_HR.dbo.Сотрудник.Guid
    Comment        nvarchar(500) NULL,

    CONSTRAINT PK_Приход PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Приход_Поставщик FOREIGN KEY (ПоставщикId)
        REFERENCES dbo.Поставщик (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT FK_Приход_Номенклатура FOREIGN KEY (НоменклатураId)
        REFERENCES dbo.Номенклатура (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT FK_Приход_Склад FOREIGN KEY (СкладId)
        REFERENCES dbo.Склад (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT CK_Приход_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_Приход_Price CHECK (Price >= 0),
    CONSTRAINT CK_Приход_Sum CHECK (Sum >= 0)
);
GO

/* ---------------------------------------------------------------------------
   РАСХОД

   Type:
     1 — продажа клиенту
     2 — возврат поставщику
     3 — списание / брак
     4 — перемещение между складами
     5 — инвентаризационная убыль
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Расход
(
    Id             int IDENTITY(1, 1) NOT NULL,
    UETR           uniqueidentifier NOT NULL,
    Date           datetime NOT NULL,
    Type           int NOT NULL,
    КлиентId       int NOT NULL,
    НакладнаяId    int NULL,                  -- шапка отгрузки; NULL для перемещений и списаний
    НоменклатураId int NOT NULL,
    СкладId        int NOT NULL,
    Quantity       decimal(18, 3) NOT NULL,
    Price          decimal(18, 2) NOT NULL,
    Sum            decimal(18, 2) NOT NULL,
    СотрудникGuid  uniqueidentifier NULL,      -- -> Sklad_v1_HR.dbo.Сотрудник.Guid
    Comment        nvarchar(500) NULL,

    CONSTRAINT PK_Расход PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Расход_Клиент FOREIGN KEY (КлиентId)
        REFERENCES dbo.Клиент (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT FK_Расход_Накладная FOREIGN KEY (НакладнаяId)
        REFERENCES dbo.НакладнаяРасход (Id) ON UPDATE NO ACTION ON DELETE CASCADE,
    CONSTRAINT FK_Расход_Номенклатура FOREIGN KEY (НоменклатураId)
        REFERENCES dbo.Номенклатура (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT FK_Расход_Склад FOREIGN KEY (СкладId)
        REFERENCES dbo.Склад (Id) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT CK_Расход_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_Расход_Price CHECK (Price >= 0),
    CONSTRAINT CK_Расход_Sum CHECK (Sum >= 0)
);
GO

/* ---------------------------------------------------------------------------
   ДЕНЬГИ

   OperationType:
     1 — оплата получена
     2 — оплата выдана
     3 — заработная плата
     4 — аванс / предоплата

   CounterpartyType:
     0 — не задан
     1 — клиент     (CounterpartyId -> Клиент.Id)
     2 — поставщик  (CounterpartyId -> Поставщик.Id)
     3 — сотрудник  (CounterpartyId -> Id сотрудника в Sklad_v1_HR)

   CounterpartyId полиморфен — FK не создаётся, целостность проверяет
   приложение (проверочный запрос — в database-schema.md). Для сотрудника
   (CounterpartyType = 3) контрагентом считается СотрудникGuid, а
   CounterpartyId не заполняется: идентификатор сотрудника принадлежит другой
   базе и в основной не имеет смысла.

   СотрудникGuid — для выплат (OperationType = 3): ссылка на кадровую базу
   Sklad_v1_HR по Guid. FK между базами невозможен, связь «по договору».

   PaymentMethod:
     1 — наличные
     2 — безналичный расчёт
     3 — банковская карта

   КодСделки связывает платёж клиента со сделкой (NULL — для поставщиков,
   сотрудников и платежей вне сделки). НомерДокумента — номер платёжного
   поручения или кассового ордера; в эталоне его не было, поэтому у
   перенесённых строк он пуст.
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Деньги
(
    Id               int IDENTITY(1, 1) NOT NULL,
    UETR             uniqueidentifier NOT NULL,
    Date             datetime NOT NULL,
    OperationType    int NOT NULL,
    CounterpartyId   int NULL,
    CounterpartyType int NOT NULL CONSTRAINT DF_Деньги_ТипКонтрагента DEFAULT (0),
    КодСделки        uniqueidentifier NULL,
    Sum              decimal(18, 2) NOT NULL,
    PaymentMethod    int NOT NULL CONSTRAINT DF_Деньги_СпособОплаты DEFAULT (1),
    НомерДокумента   nvarchar(30) NULL,
    СотрудникGuid    uniqueidentifier NULL,    -- -> Sklad_v1_HR.dbo.Сотрудник.Guid
    Comment          nvarchar(500) NULL,

    CONSTRAINT PK_Деньги PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Деньги_Сделка FOREIGN KEY (КодСделки)
        REFERENCES dbo.Сделка (КодСделки) ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT CK_Деньги_Sum CHECK (Sum >= 0),
    -- контрагент проверяем по типу: сотрудник живёт в другой базе, поэтому
    -- для него обязателен СотрудникGuid, а CounterpartyId остаётся пустым
    CONSTRAINT CK_Деньги_Контрагент CHECK (
        (CounterpartyType = 0 AND CounterpartyId IS NULL)
     OR (CounterpartyType IN (1, 2) AND CounterpartyId IS NOT NULL)
     OR (CounterpartyType = 3 AND СотрудникGuid IS NOT NULL))
);
GO

/* ===========================================================================
   РАЗДЕЛ 3. РЕВИЗИИ
   =========================================================================== */
IF OBJECT_ID(N'dbo.Расхождение', N'U') IS NOT NULL DROP TABLE dbo.Расхождение;
IF OBJECT_ID(N'dbo.Ревизия',     N'U') IS NOT NULL DROP TABLE dbo.Ревизия;
GO

/* ---------------------------------------------------------------------------
   Ревизия

   Type:
     1 — приход
     2 — расход
     3 — деньги
     4 — остатки
     5 — полная (все движения)

   Status:
     1 — создана
     2 — в работе
     3 — завершена
     4 — отменена
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Ревизия
(
    Code        nvarchar(30) NOT NULL,
    Type        int NOT NULL,
    DateStart   date NOT NULL,
    DateEnd     date NOT NULL,
    Status      int NOT NULL,
    DateCreated datetime NOT NULL CONSTRAINT DF_Ревизия_DateCreated DEFAULT (GETDATE()),

    CONSTRAINT PK_Ревизия PRIMARY KEY CLUSTERED (Code),
    CONSTRAINT CK_Ревизия_Период CHECK (DateEnd >= DateStart)
);
GO

/* ---------------------------------------------------------------------------
   Расхождение — «учёт против факта» внутри ревизии.
   Delta вычисляется, а не хранится.
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Расхождение
(
    Id              int IDENTITY(1, 1) NOT NULL,
    КодРевизии       nvarchar(30) NOT NULL,
    Type            int NOT NULL,
    НоменклатураId  int NULL,
    AccountingValue decimal(18, 3) NOT NULL,
    ActualValue     decimal(18, 3) NOT NULL,
    Delta           AS (ActualValue - AccountingValue),
    Comment         nvarchar(500) NULL,

    CONSTRAINT PK_Расхождение PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Расхождение_Ревизия FOREIGN KEY (КодРевизии)
        REFERENCES dbo.Ревизия (Code) ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT FK_Расхождение_Номенклатура FOREIGN KEY (НоменклатураId)
        REFERENCES dbo.Номенклатура (Id) ON UPDATE CASCADE ON DELETE NO ACTION
);
GO

PRINT N'БД Sklad-v4.1: таблицы созданы (кадры — в Sklad_v1_HR). Далее create-indexes.sql';
GO
