namespace SkladApi.Dtos;

public class ОтпущенныйТоварDto
{
    public int Id { get; set; }
    public string Номер { get; set; } = null!;
    public DateTime Дата { get; set; }
    public decimal Сумма { get; set; }
    public Guid СделкаId { get; set; }
}
