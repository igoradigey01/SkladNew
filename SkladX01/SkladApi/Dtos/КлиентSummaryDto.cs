namespace SkladApi.Dtos;

public class КлиентSummaryDto
{
    public int КлиентId { get; set; }
    public string Псевдоним { get; set; } = null!;
    public decimal ПределКредита { get; set; }
    public bool Активен { get; set; }
    public int Сделок { get; set; }
    public int АктивныхСделок { get; set; }
    public decimal Отгружено { get; set; }
    public decimal Оплачено { get; set; }
    public decimal Долг { get; set; }
    public decimal Переплата { get; set; }
    public bool ПревышенЛимит { get; set; }
}
