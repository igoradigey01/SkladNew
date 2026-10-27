namespace SkladDB.Models;

/// <summary>
/// Поля аудита, которые автоматически заполняет SkladContext.
/// Реализуется только транзакционными таблицами и основными справочниками
/// (Группы 1 и 2 из задачи 02-audit-trail-implementation.md).
/// </summary>
public interface IAuditable
{
    string? СоздалId { get; set; }
    string? ИзменилId { get; set; }
    DateTime ДатаСоздания { get; set; }
    DateTime? ДатаИзменения { get; set; }
}
