namespace SkladApi.Dtos;

/// <summary>
/// Новая приходная накладная поставщика: строки пишутся в dbo.Приход одной транзакцией.
/// Отдельной шапки нет — документ группируется по UETR.
/// </summary>
public class СоздатьПриходDto
{
    /// <summary>Дата прихода; null — текущая дата сервера.</summary>
    public DateTime? Дата { get; set; }

    /// <summary>Комментарий приёмщика (пишется в Comment каждой строки).</summary>
    public string? Комментарий { get; set; }

    public List<СоздатьСтрокуПриходаDto> Строки { get; set; } = [];
}

public class СоздатьСтрокуПриходаDto
{
    public int НоменклатураId { get; set; }
    public decimal Количество { get; set; }
    public decimal Цена { get; set; }
}

public enum СозданиеПриходаСтатус
{
    Успех,
    СделкаНеНайдена,
    НоменклатураНеНайдена,
    НетСклада,
}

public sealed record СозданиеПриходаРезультат(
    СозданиеПриходаСтатус Статус,
    ПолученныйТоварDto? Приход = null,
    int? НоменклатураId = null);
