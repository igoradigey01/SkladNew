namespace SkladApi.Dtos;

/// <summary>Сделка поставщика (поставка) — отдельная сущность, не связана со сделками клиента.</summary>
public class СделкаПоставщикаDto
{
    public Guid Id { get; set; }
    public string Номер { get; set; } = null!;
    public decimal Долг { get; set; }
    public string? Комментарий { get; set; }
}
