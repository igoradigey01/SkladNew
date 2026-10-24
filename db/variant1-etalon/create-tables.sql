/* ============================================================================
   СКЛАД NEW — ВАРИАНТ 1: схема 1:1 по эталону Sh_29-06-25, типы в современном виде
   ----------------------------------------------------------------------------
   Берём ВСЕ 22 таблицы эталона как есть и чистим только техническое:
     • nchar(n)/char(n)  -> nvarchar(n)     (nchar добивал пробелами — ломал сравнения)
     • float             -> decimal(18,3)   (остатки без ошибок округления)
     • коэффициент       -> decimal(18,6)
     • smallmoney/money  -> decimal(18,2)
     • smalldatetime     -> datetime
     • PK из строк       -> int IDENTITY(1,1) + UNIQUE на исходное значение
     • без PK (АУДИД/ПРИХОД/РАСХОД) -> int IDENTITY(1,1)
     • ссылки на строковые ключи стали int: «AliasЕдИм» -> «AliasЕдИмId»,
       «ОснЕдИм» -> «ОснЕдИмId», «аудитНомерГруппы» -> «аудитНомерГруппыId»,
       «матерьял» -> «матерьялId», «основнаяЕдИзмерения» -> «основнаяЕдИзмеренияId»,
       «тараРасхода» -> «тараРасходаId»
     • добавлены CHECK на количество и суммы — по факту проверки эталонных
       данных ограничения не нарушают. Исключение: колAliasЕдИм в эталоне всегда 0
       (упаковочные количества не ведутся), для него CHECK >= 0, а не > 0

   Имена ТАБЛИЦ не переименованы (принцип 1:1), включая опечатку «Скдад» —
   миграция данных идёт прямой переливкой, скрипт migrate-data-from-Sh_29-06-25.sql.
   ========================================================================== */

USE [SkladEtalon];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/* ===========================================================================
   РАЗДЕЛ 0. СБРОС (порядок — от зависимых к зависимым)
   =========================================================================== */
IF OBJECT_ID(N'dbo.СкладРасход', N'U') IS NOT NULL DROP TABLE dbo.СкладРасход;
IF OBJECT_ID(N'dbo.ХарактеристикаНоменклатуры', N'U') IS NOT NULL DROP TABLE dbo.ХарактеристикаНоменклатуры;
IF OBJECT_ID(N'dbo.СкладПриход', N'U') IS NOT NULL DROP TABLE dbo.СкладПриход;
IF OBJECT_ID(N'dbo.СвязиAlisEдИзмНоменклатура', N'U') IS NOT NULL DROP TABLE dbo.СвязиAlisEдИзмНоменклатура;
IF OBJECT_ID(N'dbo.НакладнаяРасход', N'U') IS NOT NULL DROP TABLE dbo.НакладнаяРасход;
IF OBJECT_ID(N'dbo.КлиентДеньги', N'U') IS NOT NULL DROP TABLE dbo.КлиентДеньги;
IF OBJECT_ID(N'dbo.РезультатАудита', N'U') IS NOT NULL DROP TABLE dbo.РезультатАудита;
IF OBJECT_ID(N'dbo.ПеремещениеПоСкладам', N'U') IS NOT NULL DROP TABLE dbo.ПеремещениеПоСкладам;
IF OBJECT_ID(N'dbo.Номенклатура', N'U') IS NOT NULL DROP TABLE dbo.Номенклатура;
IF OBJECT_ID(N'dbo.НакладнаяПриход', N'U') IS NOT NULL DROP TABLE dbo.НакладнаяПриход;
IF OBJECT_ID(N'dbo.КлиентМеткаРасчет', N'U') IS NOT NULL DROP TABLE dbo.КлиентМеткаРасчет;
IF OBJECT_ID(N'dbo.Скдад', N'U') IS NOT NULL DROP TABLE dbo.Скдад;
IF OBJECT_ID(N'dbo.РАСХОД', N'U') IS NOT NULL DROP TABLE dbo.РАСХОД;
IF OBJECT_ID(N'dbo.Поставщик', N'U') IS NOT NULL DROP TABLE dbo.Поставщик;
IF OBJECT_ID(N'dbo.ПРИХОД', N'U') IS NOT NULL DROP TABLE dbo.ПРИХОД;
IF OBJECT_ID(N'dbo.ОсновнаяЕдИзмерения', N'U') IS NOT NULL DROP TABLE dbo.ОсновнаяЕдИзмерения;
IF OBJECT_ID(N'dbo.Матерьял', N'U') IS NOT NULL DROP TABLE dbo.Матерьял;
IF OBJECT_ID(N'dbo.Клиент', N'U') IS NOT NULL DROP TABLE dbo.Клиент;
IF OBJECT_ID(N'dbo.АудитМетка', N'U') IS NOT NULL DROP TABLE dbo.АудитМетка;
IF OBJECT_ID(N'dbo.АтрибутыМатерьяла', N'U') IS NOT NULL DROP TABLE dbo.АтрибутыМатерьяла;
IF OBJECT_ID(N'dbo.АУДИД', N'U') IS NOT NULL DROP TABLE dbo.АУДИД;
IF OBJECT_ID(N'dbo.AliasЕдИзмТовар', N'U') IS NOT NULL DROP TABLE dbo.AliasЕдИзмТовар;
GO

/* ---------------------------------------------------------------------------
   AliasЕдИзмТовар
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[AliasЕдИзмТовар]
(
    [Id]                       int IDENTITY(1, 1) NOT NULL,
    [название]                 nvarchar(15)               NOT NULL,
    CONSTRAINT PK_AliasЕдИзмТовар PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT UQ_AliasЕдИзмТовар_название UNIQUE ([название])
);
GO

/* ---------------------------------------------------------------------------
   АУДИД
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[АУДИД]
(
    [Id]                       int IDENTITY(1, 1) NOT NULL,
    [Номенкл]                  int                        NOT NULL,
    [количест]                 decimal(18, 3)             NOT NULL,
    CONSTRAINT CK_АУДИД_количест CHECK ([количест] >= 0),
    [едИзм]                    nvarchar(5)                NOT NULL,
    [имя]                      nvarchar(50)               NOT NULL,
    CONSTRAINT PK_АУДИД PRIMARY KEY CLUSTERED ([Id])
);
GO

/* ---------------------------------------------------------------------------
   АтрибутыМатерьяла
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[АтрибутыМатерьяла]
(
    [Id]                       int IDENTITY(1, 1) NOT NULL,
    [название]                 nvarchar(15)               NOT NULL,
    CONSTRAINT PK_АтрибутыМатерьяла PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT UQ_АтрибутыМатерьяла_название UNIQUE ([название])
);
GO

/* ---------------------------------------------------------------------------
   АудитМетка
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[АудитМетка]
(
    [Id]                       int IDENTITY(1, 1) NOT NULL,
    [номерГруппы]              nvarchar(5)                NOT NULL,
    [датаНазначенияНомера]     datetime                   NOT NULL,
    CONSTRAINT PK_АудитМетка PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT UQ_АудитМетка_номерГруппы UNIQUE ([номерГруппы])
);
GO

/* ---------------------------------------------------------------------------
   Клиент
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[Клиент]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [псевдоним]                nvarchar(25)               NOT NULL,
    [ФИО]                      nvarchar(50)               NOT NULL DEFAULT ('none'),
    [телефон]                  nvarchar(18)               NOT NULL,
    [город]                    nvarchar(20)               NOT NULL,
    [ПределКредита]            decimal(18, 2)             NOT NULL DEFAULT ((10000)),
    CONSTRAINT CK_Клиент_ПределКредита CHECK ([ПределКредита] >= 0),
    CONSTRAINT PK_Клиент PRIMARY KEY CLUSTERED ([id])
);
GO

/* ---------------------------------------------------------------------------
   Матерьял
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[Матерьял]
(
    [Id]                       int IDENTITY(1, 1) NOT NULL,
    [название]                 nvarchar(25)               NOT NULL,
    CONSTRAINT PK_Матерьял PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT UQ_Матерьял_название UNIQUE ([название])
);
GO

/* ---------------------------------------------------------------------------
   ОсновнаяЕдИзмерения
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[ОсновнаяЕдИзмерения]
(
    [Id]                       int IDENTITY(1, 1) NOT NULL,
    [название]                 nvarchar(5)                NOT NULL,
    CONSTRAINT PK_ОсновнаяЕдИзмерения PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT UQ_ОсновнаяЕдИзмерения_название UNIQUE ([название])
);
GO

/* ---------------------------------------------------------------------------
   ПРИХОД
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[ПРИХОД]
(
    [Id]                       int IDENTITY(1, 1) NOT NULL,
    [номенклатура]             int                        NOT NULL,
    [кол]                      decimal(18, 3)             NULL,
    CONSTRAINT CK_ПРИХОД_кол CHECK ([кол] IS NULL OR [кол] > 0),
    [аудитНомерГруппы]         nvarchar(5)                NOT NULL,
    CONSTRAINT PK_ПРИХОД PRIMARY KEY CLUSTERED ([Id])
);
GO

/* ---------------------------------------------------------------------------
   Поставщик
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[Поставщик]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [город]                    nvarchar(15)               NOT NULL,
    CONSTRAINT PK_Поставщик PRIMARY KEY CLUSTERED ([id])
);
GO

/* ---------------------------------------------------------------------------
   РАСХОД
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[РАСХОД]
(
    [Id]                       int IDENTITY(1, 1) NOT NULL,
    [номенклатура]             int                        NOT NULL,
    [кол]                      decimal(18, 3)             NULL,
    CONSTRAINT CK_РАСХОД_кол CHECK ([кол] IS NULL OR [кол] > 0),
    [аудитНомерГруппы]         nvarchar(5)                NOT NULL,
    CONSTRAINT PK_РАСХОД PRIMARY KEY CLUSTERED ([Id])
);
GO

/* ---------------------------------------------------------------------------
   Скдад
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[Скдад]
(
    [id]                       int                        NOT NULL,
    [название]                 nvarchar(15)               NOT NULL,
    [alias]                    nvarchar(5)                NOT NULL,
    CONSTRAINT PK_Скдад PRIMARY KEY CLUSTERED ([id])
);
GO

/* ---------------------------------------------------------------------------
   КлиентМеткаРасчет
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[КлиентМеткаРасчет]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [клиентId]                 int                        NOT NULL,
    [дата]                     datetime                   NOT NULL DEFAULT (getdate()),
    [меткаНомер]               nvarchar(5)                NOT NULL,
    [Долг]                     decimal(18, 2)             NOT NULL,
    CONSTRAINT CK_КлиентМеткаРасчет_Долг CHECK ([Долг] >= 0),
    [коментарий]               nvarchar(50)               NOT NULL,
    CONSTRAINT PK_КлиентМеткаРасчет PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_КлиентМеткаРасчет_клиентId FOREIGN KEY ([клиентId]) REFERENCES dbo.[Клиент] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   НакладнаяПриход
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[НакладнаяПриход]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [поставщик]                int                        NOT NULL,
    [дата]                     datetime                   NOT NULL DEFAULT (getdate()),
    [номерНакладной]           nvarchar(10)               NOT NULL DEFAULT (''),
    [суммаПонаклодной]         decimal(18, 2)             NOT NULL,
    CONSTRAINT CK_НакладнаяПриход_суммаПонаклодной CHECK ([суммаПонаклодной] >= 0),
    CONSTRAINT PK_НакладнаяПриход PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_НакладнаяПриход_поставщик FOREIGN KEY ([поставщик]) REFERENCES dbo.[Поставщик] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   Номенклатура
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[Номенклатура]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [название]                 nvarchar(50)               NOT NULL,
    [основнаяЕдИзмеренияId]    int                        NOT NULL,
    [матерьялId]               int                        NOT NULL,
    [ценаОтпускаOснЕдИзм]      decimal(18, 2)             NOT NULL DEFAULT ((0)),
    CONSTRAINT CK_Номенклатура_ценаОтпускаOснЕдИзм CHECK ([ценаОтпускаOснЕдИзм] >= 0),
    CONSTRAINT PK_Номенклатура PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_Номенклатура_матерьялId FOREIGN KEY ([матерьялId]) REFERENCES dbo.[Матерьял] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_Номенклатура_основнаяЕдИзмеренияId FOREIGN KEY ([основнаяЕдИзмеренияId]) REFERENCES dbo.[ОсновнаяЕдИзмерения] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   ПеремещениеПоСкладам
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[ПеремещениеПоСкладам]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [складНачальный]           int                        NOT NULL,
    [складКонечный]            int                        NOT NULL,
    [дата]                     datetime                   NOT NULL,
    [ктоОсуществлял]           nvarchar(25)               NOT NULL,
    [номенклатура]             int                        NOT NULL,
    [колОснЕдИзм]              decimal(18, 3)             NOT NULL,
    CONSTRAINT CK_ПеремещениеПоСкладам_колОснЕдИзм CHECK ([колОснЕдИзм] >= 0),
    [аудитНомерГруппыId]       int                        NOT NULL,
    [оснЕдИзмId]               int                        NOT NULL,
    CONSTRAINT PK_ПеремещениеПоСкладам PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_ПеремещениеПоСкладам_аудитНомерГруппыId FOREIGN KEY ([аудитНомерГруппыId]) REFERENCES dbo.[АудитМетка] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_ПеремещениеПоСкладам_оснЕдИзмId FOREIGN KEY ([оснЕдИзмId]) REFERENCES dbo.[ОсновнаяЕдИзмерения] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_ПеремещениеПоСкладам_складКонечный FOREIGN KEY ([складКонечный]) REFERENCES dbo.[Скдад] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_ПеремещениеПоСкладам_складНачальный FOREIGN KEY ([складНачальный]) REFERENCES dbo.[Скдад] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   РезультатАудита
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[РезультатАудита]
(
    [номерГруппыId]            int                        NOT NULL,
    [номенклатура]             int                        NOT NULL,
    [ОснЕдИзмId]               int                        NOT NULL,
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [кол]                      decimal(18, 3)             NOT NULL,
    CONSTRAINT CK_РезультатАудита_кол CHECK ([кол] >= 0),
    [недостача]                decimal(18, 3)             NOT NULL,
    CONSTRAINT CK_РезультатАудита_недостача CHECK ([недостача] >= 0),
    CONSTRAINT PK_РезультатАудита PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_РезультатАудита_номерГруппыId FOREIGN KEY ([номерГруппыId]) REFERENCES dbo.[АудитМетка] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_РезультатАудита_ОснЕдИзмId FOREIGN KEY ([ОснЕдИзмId]) REFERENCES dbo.[ОсновнаяЕдИзмерения] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   КлиентДеньги
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[КлиентДеньги]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [клиентId]                 int                        NOT NULL,
    [дата]                     datetime                   NOT NULL,
    [cуммаПринятыхДенег]       decimal(18, 2)             NOT NULL,
    [меткаId]                  int                        NOT NULL,
    CONSTRAINT PK_КлиентДеньги PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_КлиентДеньги_клиентId FOREIGN KEY ([клиентId]) REFERENCES dbo.[Клиент] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_КлиентДеньги_меткаId FOREIGN KEY ([меткаId]) REFERENCES dbo.[КлиентМеткаРасчет] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   НакладнаяРасход
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[НакладнаяРасход]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [номер]                    nvarchar(5)                NOT NULL,
    [дата]                     datetime                   NOT NULL DEFAULT (getdate()),
    [клиентId]                 int                        NOT NULL,
    [суммаПонаклодной]         decimal(18, 2)             NOT NULL,
    CONSTRAINT CK_НакладнаяРасход_суммаПонаклодной CHECK ([суммаПонаклодной] >= 0),
    [меткаId]                  int                        NOT NULL,
    CONSTRAINT PK_НакладнаяРасход PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_НакладнаяРасход_клиентId FOREIGN KEY ([клиентId]) REFERENCES dbo.[Клиент] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_НакладнаяРасход_меткаId FOREIGN KEY ([меткаId]) REFERENCES dbo.[КлиентМеткаРасчет] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   СвязиAlisEдИзмНоменклатура
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[СвязиAlisEдИзмНоменклатура]
(
    [Id]                       int IDENTITY(1, 1) NOT NULL,
    [aliasЕдИзмId]             int                        NOT NULL,
    [idНоменклатура]           int                        NOT NULL,
    [коэфициэнтПриведенияКоснЕдИзм] decimal(18, 6)             NOT NULL,
    [ценаОтпускаДругая]        bit                        NOT NULL,
    [ценаОтпуска]              decimal(18, 2)             NULL,
    CONSTRAINT CK_СвязиAlisEдИзмНоменклатура_ценаОтпуска CHECK ([ценаОтпуска] >= 0),
    [оснЕдИзмId]               int                        NULL,
    CONSTRAINT PK_СвязиAlisEдИзмНоменклатура PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT UQ_СвязиAlisEдИзмНоменклатура UNIQUE ([aliasЕдИзмId], [idНоменклатура]),
    CONSTRAINT FK_СвязиAlisEдИзмНоменклатура_aliasЕдИзмId FOREIGN KEY ([aliasЕдИзмId]) REFERENCES dbo.[AliasЕдИзмТовар] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СвязиAlisEдИзмНоменклатура_idНоменклатура FOREIGN KEY ([idНоменклатура]) REFERENCES dbo.[Номенклатура] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СвязиAlisEдИзмНоменклатура_оснЕдИзмId FOREIGN KEY ([оснЕдИзмId]) REFERENCES dbo.[ОсновнаяЕдИзмерения] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   СкладПриход
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[СкладПриход]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [номенклатура]             int                        NOT NULL,
    [накладнаяId]              int                        NULL,
    [колОснЕдИзм]              decimal(18, 3)             NOT NULL DEFAULT ((0)),
    CONSTRAINT CK_СкладПриход_колОснЕдИзм CHECK ([колОснЕдИзм] >= 0),
    [AliasЕдИзмId]             int                        NOT NULL,
    [колAliasЕдИзм]            decimal(18, 3)             NOT NULL DEFAULT ((0)),
    CONSTRAINT CK_СкладПриход_колAliasЕдИзм CHECK ([колAliasЕдИзм] >= 0),
    [ценаОснЕдИзм]             decimal(18, 2)             NOT NULL DEFAULT ((0)),
    CONSTRAINT CK_СкладПриход_ценаОснЕдИзм CHECK ([ценаОснЕдИзм] >= 0),
    [складId]                  int                        NOT NULL,
    [ценаAliasЕдИзм]           decimal(18, 2)             NOT NULL DEFAULT ((0)),
    CONSTRAINT CK_СкладПриход_ценаAliasЕдИзм CHECK ([ценаAliasЕдИзм] >= 0),
    [ОснЕдИзмId]               int                        NOT NULL,
    [аудитНомерГруппыId]       int                        NOT NULL,
    [тараРасходаId]            int                        NULL,
    CONSTRAINT PK_СкладПриход PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_СкладПриход_AliasЕдИзмId FOREIGN KEY ([AliasЕдИзмId]) REFERENCES dbo.[AliasЕдИзмТовар] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладПриход_аудитНомерГруппыId FOREIGN KEY ([аудитНомерГруппыId]) REFERENCES dbo.[АудитМетка] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладПриход_накладнаяId FOREIGN KEY ([накладнаяId]) REFERENCES dbo.[НакладнаяПриход] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладПриход_номенклатура FOREIGN KEY ([номенклатура]) REFERENCES dbo.[Номенклатура] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладПриход_ОснЕдИзмId FOREIGN KEY ([ОснЕдИзмId]) REFERENCES dbo.[ОсновнаяЕдИзмерения] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладПриход_складId FOREIGN KEY ([складId]) REFERENCES dbo.[Скдад] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладПриход_тараРасходаId FOREIGN KEY ([тараРасходаId]) REFERENCES dbo.[AliasЕдИзмТовар] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   ХарактеристикаНоменклатуры
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[ХарактеристикаНоменклатуры]
(
    [id]                       int                        NOT NULL,
    [номенклатураId]           int                        NULL,
    [атрибут]                  nvarchar(15)               NULL,
    [значениеId]               int                        NULL,
    CONSTRAINT PK_ХарактеристикаНоменклатуры PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_ХарактеристикаНоменклатуры_значениеId FOREIGN KEY ([значениеId]) REFERENCES dbo.[АтрибутыМатерьяла] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_ХарактеристикаНоменклатуры_номенклатураId FOREIGN KEY ([номенклатураId]) REFERENCES dbo.[Номенклатура] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

/* ---------------------------------------------------------------------------
   СкладРасход
   ------------------------------------------------------------------------- */
CREATE TABLE dbo.[СкладРасход]
(
    [id]                       int IDENTITY(1, 1)         NOT NULL,
    [номенклатура]             int                        NOT NULL,
    [накладнаяId]              int                        NOT NULL,
    [AliasЕдИзмId]             int                        NOT NULL,
    [колAliasЕдИзм]            decimal(18, 3)             NULL,
    CONSTRAINT CK_СкладРасход_колAliasЕдИзм CHECK ([колAliasЕдИзм] IS NULL OR [колAliasЕдИзм] >= 0),
    [колОснЕдИзм]              decimal(18, 3)             NOT NULL,
    CONSTRAINT CK_СкладРасход_колОснЕдИзм CHECK ([колОснЕдИзм] >= 0),
    [ценаОснЕдИзм]             decimal(18, 2)             NOT NULL,
    CONSTRAINT CK_СкладРасход_ценаОснЕдИзм CHECK ([ценаОснЕдИзм] >= 0),
    [ценаAliasЕдИзм]           decimal(18, 2)             NOT NULL,
    CONSTRAINT CK_СкладРасход_ценаAliasЕдИзм CHECK ([ценаAliasЕдИзм] >= 0),
    [складId]                  int                        NOT NULL,
    [ОснЕдИзмId]               int                        NOT NULL,
    [аудитНомерГруппыId]       int                        NOT NULL,
    [тараРасходаId]            int                        NULL,
    CONSTRAINT PK_СкладРасход PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT FK_СкладРасход_AliasЕдИзмId FOREIGN KEY ([AliasЕдИзмId]) REFERENCES dbo.[AliasЕдИзмТовар] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладРасход_аудитНомерГруппыId FOREIGN KEY ([аудитНомерГруппыId]) REFERENCES dbo.[АудитМетка] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладРасход_накладнаяId FOREIGN KEY ([накладнаяId]) REFERENCES dbo.[НакладнаяРасход] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладРасход_номенклатура FOREIGN KEY ([номенклатура]) REFERENCES dbo.[Номенклатура] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладРасход_ОснЕдИзмId FOREIGN KEY ([ОснЕдИзмId]) REFERENCES dbo.[ОсновнаяЕдИзмерения] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладРасход_складId FOREIGN KEY ([складId]) REFERENCES dbo.[Скдад] ([id]) ON UPDATE NO ACTION ON DELETE NO ACTION,
    CONSTRAINT FK_СкладРасход_тараРасходаId FOREIGN KEY ([тараРасходаId]) REFERENCES dbo.[AliasЕдИзмТовар] ([Id]) ON UPDATE NO ACTION ON DELETE NO ACTION
);
GO

PRINT N'Вариант 1: 22 таблиц созданы. Далее create-indexes.sql';
GO