namespace SkladDB.Models;

/// <summary>
/// Базовая реализация полей аудита, чтобы не дублировать их в каждой модели.
/// </summary>
public abstract class AuditableEntity : IAuditable
{
    public string? СоздалId { get; set; }
    public string? ИзменилId { get; set; }
    public DateTime ДатаСоздания { get; set; }
    public DateTime? ДатаИзменения { get; set; }
}
