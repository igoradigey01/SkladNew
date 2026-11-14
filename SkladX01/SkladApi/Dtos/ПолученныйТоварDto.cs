namespace SkladApi.Dtos;

/// <summary>Приходная накладная в составе сделки поставщика.</summary>
public class ПолученныйТоварDto
{
    public Guid Id { get; set; }
    public string Номер { get; set; } = null!;
    public DateTime Дата { get; set; }
    public decimal Сумма { get; set; }
    public Guid СделкаId { get; set; }
}
