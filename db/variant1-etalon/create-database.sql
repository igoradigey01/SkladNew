/* ============================================================================
   СКЛАД NEW — ВАРИАНТ 1: создание базы данных
   ----------------------------------------------------------------------------
   Схема повторяет эталон Sh_29-06-25 (22 таблицы) с чисткой типов.
   Подробности: database-schema.md. Отличия от варианта 2 — там же.
   ========================================================================== */

USE master;
GO

IF DB_ID(N'SkladEtalon') IS NOT NULL
BEGIN
    ALTER DATABASE [SkladEtalon] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [SkladEtalon];
END
GO

CREATE DATABASE [SkladEtalon];
GO

ALTER DATABASE [SkladEtalon] SET COMPATIBILITY_LEVEL = 150;
GO
ALTER DATABASE [SkladEtalon] COLLATE Cyrillic_General_CI_AS;
GO
ALTER DATABASE [SkladEtalon] SET ANSI_NULLS ON;
GO
ALTER DATABASE [SkladEtalon] SET ANSI_PADDING ON;
GO
ALTER DATABASE [SkladEtalon] SET ANSI_WARNINGS ON;
GO
ALTER DATABASE [SkladEtalon] SET ARITHABORT ON;
GO
ALTER DATABASE [SkladEtalon] SET CONCAT_NULL_YIELDS_NULL ON;
GO
ALTER DATABASE [SkladEtalon] SET QUOTED_IDENTIFIER ON;
GO
ALTER DATABASE [SkladEtalon] SET RECOVERY SIMPLE;
GO

USE [SkladEtalon];
GO

PRINT N'БД SkladEtalon создана. Далее: create-tables.sql';
GO
