namespace SkladApi.Dtos;

public class ПринятыеДеньгиDto
{
    public int Id { get; set; }
    public DateTime Дата { get; set; }
    public decimal Сумма { get; set; }
    public Guid СделкаId { get; set; }

    /// <summary>1 — наличные, 2 — безналичный расчёт, 3 — банковская карта.</summary>
    public int СпособОплаты { get; set; }

    /// <summary>Номер платёжного поручения или кассового ордера.</summary>
    public string? НомерДокумента { get; set; }

    public string? Комментарий { get; set; }

    /// <summary>Есть хотя бы одна запись в журнале аудита.</summary>
    public bool Изменялась { get; set; }
}
