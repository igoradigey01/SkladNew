/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА: создание базы данных Sklad-v4.1
   ----------------------------------------------------------------------------
   Версия 4.1: база Sklad-v4 плюс слой взаиморасчётов с клиентом.
   Сделка связывает контрагента, накладные на отгрузку и полученные деньги
   через КодСделки; долг по сделке вычисляется, а не хранится.

   Что взято откуда:
     • справочники и складская логика — из эталона Sh_29-06-25
       (номенклатура, материалы, единицы измерения, склады, контрагенты);
     • движения и деньги — своё ядро: Приход / Расход / Деньги,
       связанные сквозным идентификатором UETR;
     • ревизии — Ревизия + Расхождение с вычисляемой дельтой;
     • атрибуты — по внешним стандартам: ТипЗначения (Attribute type),
       Характеристика (Attribute), СписокЗначений (список допустимых
       значений), ЗначениеХарактеристики (значение атрибута у позиции);
     • взаиморасчёты — Сделка, НакладнаяРасход и КодСделки в Деньги;
       долг и баланс — в представлениях.

   Подробности: database-schema.md и client-settlement.md.
   ========================================================================== */

USE master;
GO

IF DB_ID(N'Sklad-v4.1') IS NOT NULL
BEGIN
    ALTER DATABASE [Sklad-v4.1] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [Sklad-v4.1];
END
GO

CREATE DATABASE [Sklad-v4.1];
GO

ALTER DATABASE [Sklad-v4.1] SET COMPATIBILITY_LEVEL = 150;
GO
ALTER DATABASE [Sklad-v4.1] COLLATE Cyrillic_General_CI_AS;
GO
ALTER DATABASE [Sklad-v4.1] SET ANSI_NULLS ON;
GO
ALTER DATABASE [Sklad-v4.1] SET ANSI_PADDING ON;
GO
ALTER DATABASE [Sklad-v4.1] SET ANSI_WARNINGS ON;
GO
ALTER DATABASE [Sklad-v4.1] SET ARITHABORT ON;
GO
ALTER DATABASE [Sklad-v4.1] SET CONCAT_NULL_YIELDS_NULL ON;
GO
ALTER DATABASE [Sklad-v4.1] SET QUOTED_IDENTIFIER ON;
GO
ALTER DATABASE [Sklad-v4.1] SET RECOVERY SIMPLE;
GO

USE [Sklad-v4.1];
GO

PRINT N'БД Sklad-v4.1 создана. Далее: create-tables.sql';
GO
