namespace SkladApi.Dtos;

/// <summary>Сводка по поставщику: сколько получено, отдано и сколько мы должны.</summary>
public class ПоставщикSummaryDto
{
    public int ПоставщикId { get; set; }
    public string Название { get; set; } = null!;
    public string? Город { get; set; }
    public string? Телефон { get; set; }
    public int Сделок { get; set; }
    public decimal Получено { get; set; }
    public decimal Отдано { get; set; }
    public decimal Долг { get; set; }
    public decimal Переплата { get; set; }
}
