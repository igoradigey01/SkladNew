namespace SkladApi.Dtos;

/// <summary>
/// Новое содержимое накладной. Меняются только количество и цена строк;
/// добавление и удаление строк не поддерживается.
/// </summary>
public class ОбновитьНакладнуюDto
{
    public List<ОбновитьСтрокуНакладнойDto> Строки { get; set; } = [];
}

public class ОбновитьСтрокуНакладнойDto
{
    public int Id { get; set; }
    public decimal Количество { get; set; }
    public decimal Цена { get; set; }
}
