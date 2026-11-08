namespace SkladApi.Dtos;

public class НоменклатураDto
{
    public int Id { get; set; }
    public string Название { get; set; } = null!;
    public string? Артикул { get; set; }
}
