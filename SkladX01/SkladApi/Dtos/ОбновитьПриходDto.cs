namespace SkladApi.Dtos;

/// <summary>
/// Новое содержимое приходной накладной. Меняются только количество и цена
/// строк; добавление и удаление строк не поддерживается.
/// </summary>
public class ОбновитьПриходDto
{
    public List<ОбновитьСтрокуПриходаDto> Строки { get; set; } = [];
}

public class ОбновитьСтрокуПриходаDto
{
    public int Id { get; set; }
    public decimal Количество { get; set; }
    public decimal Цена { get; set; }
}
