namespace SkladApi.Dtos;

public class МеткаРасчетDto
{
    public Guid Id { get; set; }
    public string Номер { get; set; } = null!;
    public decimal Долг { get; set; }
}
