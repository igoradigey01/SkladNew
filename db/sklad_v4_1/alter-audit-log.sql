/* ============================================================================
   ЖУРНАЛ АУДИТА (Audit Trail)

   Техническая таблица для хранения истории изменений накладных и платежей.
   Сознательно без FK на dbo.Сотрудник: сотрудники живут в другой базе
   (Sklad_v1_HR), поэтому имя и Id денормализованы.

   Id, EntityId и ChangedByEmployeeId — bigint: EntityId ссылается на int-ключи
   (НакладнаяРасход.Id, Деньги.Id), а сотрудник в HRDB тоже int, но bigint
   оставляет запас и не требует менять схему при переходе на другие ключи.

   Скрипт идемпотентен.
   ========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
CREATE TABLE dbo.AuditLog
(
    Id                    bigint IDENTITY(1, 1) NOT NULL,
    EntityType            nvarchar(100) NOT NULL,   -- Invoice | Payment
    EntityId              bigint NOT NULL,
    Action                nvarchar(20) NOT NULL,    -- Update
    ChangedByEmployeeId   bigint NOT NULL,
    ChangedByEmployeeName nvarchar(200) NOT NULL,
    Timestamp             datetime2 NOT NULL
                          CONSTRAINT DF_AuditLog_Timestamp DEFAULT (SYSUTCDATETIME()),
    OldData               nvarchar(max) NULL,
    NewData               nvarchar(max) NULL,

    CONSTRAINT PK_AuditLog PRIMARY KEY CLUSTERED (Id)
);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AuditLog_Entity'
      AND object_id = OBJECT_ID(N'dbo.AuditLog')
)
    CREATE NONCLUSTERED INDEX IX_AuditLog_Entity
        ON dbo.AuditLog (EntityType, EntityId)
        INCLUDE (Timestamp);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_AuditLog_Timestamp'
      AND object_id = OBJECT_ID(N'dbo.AuditLog')
)
    CREATE NONCLUSTERED INDEX IX_AuditLog_Timestamp
        ON dbo.AuditLog (Timestamp DESC);
GO

PRINT N'Журнал аудита готов';
GO
