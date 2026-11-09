namespace SkladApi.Dtos;

/// <summary>Строка номенклатуры вместе с сохранённым вектором эмбеддинга.</summary>
public class НоменклатураВекторDto
{
    public int Id { get; set; }
    public string Название { get; set; } = null!;
    public byte[]? Вектор { get; set; }
}
