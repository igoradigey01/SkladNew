/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА: создание базы данных Sklad_v1
   ----------------------------------------------------------------------------
   Что взято откуда:
     • справочники и складская логика — из эталона Sh_29-06-25
       (номенклатура, материалы, единицы измерения, упаковка, склады,
        контрагенты, характеристики);
     • движения и деньги — своё ядро: Income / Outcome / Money,
       связанные сквозным идентификатором UETR;
     • ревизии — Revision + Discrepancy с вычисляемой дельтой.

   Подробности: database-schema.md. Сравнение с вариантом 1 — там же.
   ========================================================================== */

USE master;
GO

IF DB_ID(N'Sklad_v1') IS NOT NULL
BEGIN
    ALTER DATABASE [Sklad_v1] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [Sklad_v1];
END
GO

CREATE DATABASE [Sklad_v1];
GO

ALTER DATABASE [Sklad_v1] SET COMPATIBILITY_LEVEL = 150;
GO
ALTER DATABASE [Sklad_v1] COLLATE Cyrillic_General_CI_AS;
GO
ALTER DATABASE [Sklad_v1] SET ANSI_NULLS ON;
GO
ALTER DATABASE [Sklad_v1] SET ANSI_PADDING ON;
GO
ALTER DATABASE [Sklad_v1] SET ANSI_WARNINGS ON;
GO
ALTER DATABASE [Sklad_v1] SET ARITHABORT ON;
GO
ALTER DATABASE [Sklad_v1] SET CONCAT_NULL_YIELDS_NULL ON;
GO
ALTER DATABASE [Sklad_v1] SET QUOTED_IDENTIFIER ON;
GO
ALTER DATABASE [Sklad_v1] SET RECOVERY SIMPLE;
GO

USE [Sklad_v1];
GO

PRINT N'БД Sklad_v1 создана. Далее: create-tables.sql';
GO