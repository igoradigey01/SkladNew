namespace SkladApi.Dtos;

public class ПоставщикDto
{
    public int Id { get; set; }
    public string Название { get; set; } = null!;
    public string? Город { get; set; }
    public string? Телефон { get; set; }
}
