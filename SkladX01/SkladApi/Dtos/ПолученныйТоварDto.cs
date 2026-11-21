namespace SkladApi.Dtos;

/// <summary>Приходная накладная в составе сделки поставщика.</summary>
public class ПолученныйТоварDto
{
    public Guid Id { get; set; }
    public string Номер { get; set; } = null!;
    public DateTime Дата { get; set; }
    public decimal Сумма { get; set; }
    public Guid СделкаId { get; set; }

    /// <summary>Первая строка прихода — числовой ключ для журнала аудита.</summary>
    public int AuditId { get; set; }

    /// <summary>Есть хотя бы одна запись в журнале аудита.</summary>
    public bool Изменялась { get; set; }
}
