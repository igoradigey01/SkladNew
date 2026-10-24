/* ============================================================================
   СКЛАД NEW — КАДРОВАЯ БАЗА Sklad_v1_HR: создание базы данных
   ----------------------------------------------------------------------------
   Кадры вынесены из основной базы Sklad_v1 в отдельную. Причина — разные
   циклы работы и разный доступ: кадровая база нужна кадровику и бухгалтерии,
   складская — кладовщику; права и резервирование разводятся независимо.

   Связь между базами — через Guid: основная база хранит СотрудникGuid
   в Приход/Расход/Деньги, здесь у сотрудника есть уникальный Guid.
   FOREIGN KEY между базами невозможен, поэтому целостность держится на:
     • UNIQUE-ограничении на Guid в этой базе;
     • валидации в приложении при записи операции;
     • периодической сверке (запрос — в database-schema.md).
   ========================================================================== */

USE master;
GO

IF DB_ID(N'Sklad_v1_HR') IS NOT NULL
BEGIN
    ALTER DATABASE [Sklad_v1_HR] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [Sklad_v1_HR];
END
GO

CREATE DATABASE [Sklad_v1_HR];
GO

ALTER DATABASE [Sklad_v1_HR] SET COMPATIBILITY_LEVEL = 150;
GO
ALTER DATABASE [Sklad_v1_HR] COLLATE Cyrillic_General_CI_AS;
GO
ALTER DATABASE [Sklad_v1_HR] SET ANSI_NULLS ON;
GO
ALTER DATABASE [Sklad_v1_HR] SET ANSI_PADDING ON;
GO
ALTER DATABASE [Sklad_v1_HR] SET ANSI_WARNINGS ON;
GO
ALTER DATABASE [Sklad_v1_HR] SET ARITHABORT ON;
GO
ALTER DATABASE [Sklad_v1_HR] SET CONCAT_NULL_YIELDS_NULL ON;
GO
ALTER DATABASE [Sklad_v1_HR] SET QUOTED_IDENTIFIER ON;
GO
ALTER DATABASE [Sklad_v1_HR] SET RECOVERY SIMPLE;
GO

USE [Sklad_v1_HR];
GO

PRINT N'БД Sklad_v1_HR создана. Далее: create-tables.sql';
GO