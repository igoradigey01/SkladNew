namespace SkladApi.Dtos;

/// <summary>Меняется только сумма платежа; дата, способ и контрагент неизменны.</summary>
public class ОбновитьПлатежDto
{
    public decimal Сумма { get; set; }
}
