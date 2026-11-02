namespace SkladApi.Dtos;

public class ПринятыеДеньгиDto
{
    public int Id { get; set; }
    public DateTime Дата { get; set; }
    public decimal Сумма { get; set; }
    public int МеткаId { get; set; }
}
