namespace SkladApi.Dtos;

/// <summary>Строка приходной накладной — для редактирования.</summary>
public class СтрокаПриходаDto
{
    public int Id { get; set; }
    public int НоменклатураId { get; set; }
    public string Номенклатура { get; set; } = null!;
    public decimal Количество { get; set; }
    public decimal Цена { get; set; }
    public decimal Сумма { get; set; }
}
