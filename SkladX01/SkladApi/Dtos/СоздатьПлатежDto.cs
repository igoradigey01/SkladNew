namespace SkladApi.Dtos;

/// <summary>
/// Приём денег от клиента по сделке. Платёж всегда клиентский
/// (OperationType = 1, CounterpartyType = 1).
/// </summary>
public class СоздатьПлатежDto
{
    /// <summary>Дата платежа; null — текущая дата сервера.</summary>
    public DateTime? Дата { get; set; }

    public decimal Сумма { get; set; }

    /// <summary>1 — наличные, 2 — безналичный расчёт, 3 — банковская карта.</summary>
    public int? СпособОплаты { get; set; }

    /// <summary>Номер платёжного поручения или кассового ордера.</summary>
    public string? НомерДокумента { get; set; }

    public string? Комментарий { get; set; }
}

public enum СозданиеПлатежаСтатус
{
    Успех,
    СделкаНеНайдена,
}

public sealed record СозданиеПлатежаРезультат(
    СозданиеПлатежаСтатус Статус,
    ПринятыеДеньгиDto? Платеж = null);
