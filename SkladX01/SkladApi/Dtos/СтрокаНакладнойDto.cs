namespace SkladApi.Dtos;

/// <summary>Строка накладной с названием номенклатуры — для редактирования.</summary>
public class СтрокаНакладнойDto
{
    public int Id { get; set; }
    public int НоменклатураId { get; set; }
    public string Номенклатура { get; set; } = null!;
    public decimal Количество { get; set; }
    public decimal Цена { get; set; }
    public decimal Сумма { get; set; }
}
