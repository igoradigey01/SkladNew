namespace SkladDB.Models;

public class Номенклатура : AuditableEntity
{
    public int Id { get; set; }
    public Guid Guid { get; set; }
    public string Код { get; set; } = null!;
    public string? Артикул { get; set; }
    public string Название { get; set; } = null!;
    public string? КраткоеНазвание { get; set; }
    public int? ГруппаId { get; set; }
    public int? КатегорияId { get; set; }
    public int ТипId { get; set; }
    public int? ВидНоменклатурыId { get; set; }
    public int БазоваяЕдИзмId { get; set; }
    public decimal? ЦенаЗакупочная { get; set; }
    public decimal? ЦенаПродажная { get; set; }
    public decimal? ЦенаОптовая { get; set; }
    public decimal? МинимальныйОстаток { get; set; }
    public decimal? МаксимальныйОстаток { get; set; }
    public int? СрокГодности { get; set; }
    public string? СтранаПроисхождения { get; set; }
    public byte[]? Фото { get; set; }
    public string? Описание { get; set; }
    public bool Активен { get; set; }
}
