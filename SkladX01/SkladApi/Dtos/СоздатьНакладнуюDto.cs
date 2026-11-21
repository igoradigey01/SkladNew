namespace SkladApi.Dtos;

/// <summary>
/// Новая накладная клиента: шапка и строки создаются одной транзакцией.
/// </summary>
public class СоздатьНакладнуюDto
{
    /// <summary>Дата накладной; null — текущая дата сервера.</summary>
    public DateTime? Дата { get; set; }

    public string? Комментарий { get; set; }

    public List<СоздатьСтрокуНакладнойDto> Строки { get; set; } = [];
}

public class СоздатьСтрокуНакладнойDto
{
    public int НоменклатураId { get; set; }
    public decimal Количество { get; set; }
    public decimal Цена { get; set; }
}

public enum СозданиеНакладнойСтатус
{
    Успех,
    СделкаНеНайдена,
    НоменклатураНеНайдена,
    НетСклада,
}

public sealed record СозданиеНакладнойРезультат(
    СозданиеНакладнойСтатус Статус,
    ОтпущенныйТоварDto? Накладная = null,
    int? НоменклатураId = null);
