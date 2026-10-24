/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА: создание базы данных Sklad-v4
   ----------------------------------------------------------------------------
   Версия 4: та же база, что и Sklad-v3, но система характеристик переделана
   по внешним стандартам — с типами значения, списками допустимых
   значений и наследованием значений по виду номенклатуры.

   Что взято откуда:
     • справочники и складская логика — из эталона Sh_29-06-25
       (номенклатура, материалы, единицы измерения, склады, контрагенты);
     • движения и деньги — своё ядро: Приход / Расход / Деньги,
       связанные сквозным идентификатором UETR;
     • ревизии — Ревизия + Расхождение с вычисляемой дельтой;
     • атрибуты — по внешним стандартам: ТипЗначения (Attribute type),
       Характеристика (Attribute), СписокЗначений (список допустимых
       значений), ЗначениеХарактеристики (значение атрибута у позиции).

   Подробности: database-schema.md.
   ========================================================================== */

USE master;
GO

IF DB_ID(N'Sklad-v4') IS NOT NULL
BEGIN
    ALTER DATABASE [Sklad-v4] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [Sklad-v4];
END
GO

CREATE DATABASE [Sklad-v4];
GO

ALTER DATABASE [Sklad-v4] SET COMPATIBILITY_LEVEL = 150;
GO
ALTER DATABASE [Sklad-v4] COLLATE Cyrillic_General_CI_AS;
GO
ALTER DATABASE [Sklad-v4] SET ANSI_NULLS ON;
GO
ALTER DATABASE [Sklad-v4] SET ANSI_PADDING ON;
GO
ALTER DATABASE [Sklad-v4] SET ANSI_WARNINGS ON;
GO
ALTER DATABASE [Sklad-v4] SET ARITHABORT ON;
GO
ALTER DATABASE [Sklad-v4] SET CONCAT_NULL_YIELDS_NULL ON;
GO
ALTER DATABASE [Sklad-v4] SET QUOTED_IDENTIFIER ON;
GO
ALTER DATABASE [Sklad-v4] SET RECOVERY SIMPLE;
GO

USE [Sklad-v4];
GO

PRINT N'БД Sklad-v4 создана. Далее: create-tables.sql';
GO
