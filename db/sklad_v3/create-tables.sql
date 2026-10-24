/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА Sklad-v3: таблицы
   ----------------------------------------------------------------------------
   Кадры вынесены в отдельную базу Sklad_v1_HR (таблицы Сотрудник,
   Специальность) — см. ../sklad_v1_hr/. Связь основной базы с кадрами идёт
   через Guid: в Приход/Расход/Деньги лежит СотрудникGuid, в Sklad_v1_HR у
   сотрудника есть уникальный Guid. FOREIGN KEY между базами невозможен,
   поэтому целостность проверяет приложение и периодическая проверка
   (запрос — в database-schema.md).

   Имена таблиц — на русском (как в эталоне Sh_29-06-25 и в правилах проекта:
   БД по-русски, C# и API по-английски).

   Что взято откуда:
     • справочники и складская логика — из эталона Sh_29-06-25
       (номенклатура, материалы, единицы измерения, упаковка, склады,
        контрагенты, характеристики);
     • движения и деньги — своё ядро: Приход / Расход / Деньги,
       связанные сквозным идентификатором UETR;
     • ревизии — Ревизия + Расхождение с вычисляемой дельтой.

   Сопоставление с эталоном:
     Склад                  <- Скдад
     ВидНоменклатуры               <- Матерьял
     Характеристика       <- АтрибутыМатерьяла
     Номенклатура           <- Номенклатура
     ЗначениеХарактеристики <- ЗначениеХарактеристики
     (механизм упаковочных единиц не переносится — см. раздел «Почему без упаковки»)
     Клиент / Поставщик     <- Клиент / Поставщик
     Приход / Расход / Деньги <- СкладПриход / СкладРасход / КлиентДеньги
     Ревизия / Расхождение  <- АудитМетка / РезультатАудита
     (Сотрудник, Специальность — не здесь, а в базе Sklad_v1_HR)
   ПОЧЕМУ БЕЗ УПАКОВКИ
   В эталоне механизм второй единицы (AliasЕдИзмТовар + коэффициенты перевода)
   был заведён, но не работал: kolAliasЕдИм = 0 во всех 6773 строках движений,
   таблица коэффициентов пуста, ценаAliasЕдИм повторяла цену в основной единице.
   В новой схеме он не переносится: одна единица измерения на позицию, отпуск
   ведётся в ней же. Если упаковка понадобится — это отдельная задача с
   коэффициентами перевода, а не пустой справочник.
   ========================================================================== */

USE [Sklad-v3];
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
IF OBJECT_ID(N'dbo.Расхождение',              N'U') IS NOT NULL DROP TABLE dbo.Расхождение;
IF OBJECT_ID(N'dbo.Ревизия',                  N'U') IS NOT NULL DROP TABLE dbo.Ревизия;
IF OBJECT_ID(N'dbo.ЗначениеХарактеристики', N'U') IS NOT NULL DROP TABLE dbo.ЗначениеХарактеристики;
IF OBJECT_ID(N'dbo.Номенклатура',             N'U') IS NOT NULL DROP TABLE dbo.Номенклатура;
IF OBJECT_ID(N'dbo.Характеристика',         N'U') IS NOT NULL DROP TABLE dbo.Характеристика;
IF OBJECT_ID(N'dbo.КатегорияНоменклатуры',  N'U') IS NOT NULL DROP TABLE dbo.КатегорияНоменклатуры;
IF OBJECT_ID(N'dbo.ГруппаНоменклатуры',      N'U') IS NOT NULL DROP TABLE dbo.ГруппаНоменклатуры;
IF OBJECT_ID(N'dbo.ВидНоменклатуры',                 N'U') IS NOT NULL DROP TABLE dbo.ВидНоменклатуры;
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
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.ВидНоменклатуры
(
    Id         int IDENTITY(1, 1) NOT NULL,
    Название   nvarchar(50) NOT NULL,
    Описание  nvarchar(500) NULL,

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
   Характеристика — набор характеристик ВИДА номенклатуры.
   Смысл поменялся по сравнению с v2: характеристика больше не самостоятельный
   словарь, а принадлежит виду номенклатуры. Для ткани это «Цвет», «Плотность»,
   для мебельного щита — «Толщина», «Цвет кромки».
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Характеристика
(
    Id               int IDENTITY(1, 1) NOT NULL,
    ВидНоменклатурыId int NOT NULL,
    Название         nvarchar(50) NOT NULL,
    Порядок          int NULL,
    Обязательный     bit NOT NULL CONSTRAINT DF_Характеристика_Обязательный DEFAULT (0),

    CONSTRAINT PK_Характеристика PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Характеристика_ВидНоменклатуры FOREIGN KEY (ВидНоменклатурыId)
        REFERENCES dbo.ВидНоменклатуры (Id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT UQ_Характеристика_Вид_Название UNIQUE (ВидНоменклатурыId, Название)
);
GO

/* ---------------------------------------------------------------------------
   Номенклатура — эталон «Номенклатура» (202 позиции).
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
   Связка (номенклатура, характеристика) уникальна.
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.ЗначениеХарактеристики
(
    Id               int IDENTITY(1, 1) NOT NULL,
    НоменклатураId   int NOT NULL,
    ХарактеристикаId int NOT NULL,
    Значение         nvarchar(200) NOT NULL,

    CONSTRAINT PK_ЗначениеХарактеристики PRIMARY KEY CLUSTERED (Id),
    -- каскад только на удаление номенклатуры (как в скрипте заказчика);
    -- ON UPDATE CASCADE здесь дал бы два пути каскада: ВидНоменклатуры ->
    -- Характеристика -> ЗначениеХарактеристики и Номенклатура -> Значение…
    CONSTRAINT FK_ЗначениеХарактеристики_Номенклатура FOREIGN KEY (НоменклатураId)
        REFERENCES dbo.Номенклатура (Id) ON UPDATE NO ACTION ON DELETE CASCADE,
    CONSTRAINT FK_ЗначениеХарактеристики_Характеристика FOREIGN KEY (ХарактеристикаId)
        REFERENCES dbo.Характеристика (Id) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT UQ_ЗначениеХарактеристики UNIQUE (НоменклатураId, ХарактеристикаId)
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
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.Деньги
(
    Id               int IDENTITY(1, 1) NOT NULL,
    UETR             uniqueidentifier NOT NULL,
    Date             datetime NOT NULL,
    OperationType    int NOT NULL,
    CounterpartyId   int NULL,
    CounterpartyType int NOT NULL CONSTRAINT DF_Деньги_ТипКонтрагента DEFAULT (0),
    Sum              decimal(18, 2) NOT NULL,
    PaymentMethod    int NOT NULL CONSTRAINT DF_Деньги_СпособОплаты DEFAULT (1),
    СотрудникGuid    uniqueidentifier NULL,    -- -> Sklad_v1_HR.dbo.Сотрудник.Guid
    Comment          nvarchar(500) NULL,

    CONSTRAINT PK_Деньги PRIMARY KEY CLUSTERED (Id),
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

PRINT N'БД Sklad-v3: таблицы созданы (кадры — в Sklad_v1_HR). Далее create-indexes.sql';
GO