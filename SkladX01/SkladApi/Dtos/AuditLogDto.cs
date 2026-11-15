namespace SkladApi.Dtos;

/// <summary>Запись журнала аудита для вкладки «История».</summary>
public class AuditLogDto
{
    public long Id { get; set; }
    public string EntityType { get; set; } = null!;
    public long EntityId { get; set; }
    public string Action { get; set; } = null!;
    public long ChangedByEmployeeId { get; set; }
    public string ChangedByEmployeeName { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string? OldData { get; set; }
    public string? NewData { get; set; }
}
