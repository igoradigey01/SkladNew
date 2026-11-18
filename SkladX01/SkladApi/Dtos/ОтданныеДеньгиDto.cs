namespace SkladApi.Dtos;

/// <summary>Выплата поставщику по сделке.</summary>
public class ОтданныеДеньгиDto
{
    public int Id { get; set; }
    public DateTime Дата { get; set; }
    public decimal Сумма { get; set; }
    public Guid СделкаId { get; set; }

    /// <summary>Есть хотя бы одна запись в журнале аудита.</summary>
    public bool Изменялась { get; set; }
}
