namespace SkladApi.Dtos;

public class ПринятыеДеньгиDto
{
    public int Id { get; set; }
    public DateTime Дата { get; set; }
    public decimal Сумма { get; set; }
    public Guid СделкаId { get; set; }

    /// <summary>Есть хотя бы одна запись в журнале аудита.</summary>
    public bool Изменялась { get; set; }
}
