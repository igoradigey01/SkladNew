namespace SkladApi.Dtos;

/// <summary>Строка быстрого поиска для ввода номенклатуры в документ.</summary>
public class НоменклатураQuickDto
{
    public int Id { get; set; }
    public string Код { get; set; } = null!;
    public string? Артикул { get; set; }
    public string Название { get; set; } = null!;
    public string ЕдИзм { get; set; } = null!;
    public decimal? ЦенаЗакупочная { get; set; }
    public decimal? ЦенаПродажная { get; set; }
}

public class СправочникDto
{
    public int Id { get; set; }
    public string Название { get; set; } = null!;
}

public class НоменклатураReferencesDto
{
    public IReadOnlyList<СправочникDto> Группы { get; set; } = [];
    public IReadOnlyList<СправочникDto> Виды { get; set; } = [];
    public IReadOnlyList<СправочникDto> Категории { get; set; } = [];
    public IReadOnlyList<СправочникDto> Единицы { get; set; } = [];
}

public class ХарактеристикаВидаDto
{
    public int Id { get; set; }
    public string Название { get; set; } = null!;
    public string ТипЗначения { get; set; } = null!;
    public bool Обязательный { get; set; }
    public decimal? Минимум { get; set; }
    public decimal? Максимум { get; set; }
    public int? МаксимальнаяДлина { get; set; }
    public IReadOnlyList<string> СписокЗначений { get; set; } = [];
}

public class ЗначениеХарактеристикиRequest
{
    public int ХарактеристикаId { get; set; }
    public string Значение { get; set; } = null!;
}

public class СоздатьНоменклатуруRequest
{
    public string Код { get; set; } = null!;
    public string? Артикул { get; set; }
    public string Название { get; set; } = null!;
    public string? КраткоеНазвание { get; set; }
    public int? ГруппаId { get; set; }
    public int? КатегорияId { get; set; }
    public int ТипId { get; set; } = 1;
    public int? ВидНоменклатурыId { get; set; }
    public int БазоваяЕдИзмId { get; set; }
    public decimal? ЦенаЗакупочная { get; set; }
    public decimal? ЦенаПродажная { get; set; }
    public List<ЗначениеХарактеристикиRequest> Характеристики { get; set; } = [];
}
