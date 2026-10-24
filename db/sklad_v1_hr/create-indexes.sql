/* ============================================================================
   СКЛАД NEW — КАДРОВАЯ БАЗА Sklad_v1_HR: индексы
   ----------------------------------------------------------------------------
   • Guid проиндексирован (UNIQUE из DDL покрывает поиск по нему — отдельный
     индекс не нужен, он нужен только для выборок кадров по должности);
   • СпециальностьId — внешний ключ на Специальность.
   Скрипт идемпотентен.
   ========================================================================== */

USE [Sklad_v1_HR];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Сотрудник_Специальность' AND object_id = OBJECT_ID(N'dbo.Сотрудник'))
    CREATE NONCLUSTERED INDEX IX_Сотрудник_Специальность ON dbo.Сотрудник (СпециальностьId)
        INCLUDE (Имя);
GO

/* Поиск сотрудников по ФИО — основной сценарий кадровой базы */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Сотрудник_Имя' AND object_id = OBJECT_ID(N'dbo.Сотрудник'))
    CREATE NONCLUSTERED INDEX IX_Сотрудник_Имя ON dbo.Сотрудник (Имя);
GO

PRINT N'Индексы Sklad_v1_HR созданы';
GO