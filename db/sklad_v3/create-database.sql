/* ============================================================================
   СКЛАД NEW — ОСНОВНАЯ БАЗА: создание базы данных Sklad-v3
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

IF DB_ID(N'Sklad-v3') IS NOT NULL
BEGIN
    ALTER DATABASE [Sklad-v3] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [Sklad-v3];
END
GO

CREATE DATABASE [Sklad-v3];
GO

ALTER DATABASE [Sklad-v3] SET COMPATIBILITY_LEVEL = 150;
GO
ALTER DATABASE [Sklad-v3] COLLATE Cyrillic_General_CI_AS;
GO
ALTER DATABASE [Sklad-v3] SET ANSI_NULLS ON;
GO
ALTER DATABASE [Sklad-v3] SET ANSI_PADDING ON;
GO
ALTER DATABASE [Sklad-v3] SET ANSI_WARNINGS ON;
GO
ALTER DATABASE [Sklad-v3] SET ARITHABORT ON;
GO
ALTER DATABASE [Sklad-v3] SET CONCAT_NULL_YIELDS_NULL ON;
GO
ALTER DATABASE [Sklad-v3] SET QUOTED_IDENTIFIER ON;
GO
ALTER DATABASE [Sklad-v3] SET RECOVERY SIMPLE;
GO

USE [Sklad-v3];
GO

PRINT N'БД Sklad-v3 создана. Далее: create-tables.sql';
GO