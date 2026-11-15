namespace SkladDB.Models;

/// <summary>
/// Технический журнал изменений. Живёт в SkladDB, но ссылается на сотрудника
/// из HRDB только по Id и имени — внешнего ключа между базами нет.
/// </summary>
public class AuditLog
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
