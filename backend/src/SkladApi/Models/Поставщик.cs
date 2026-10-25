namespace SkladApi.Models;

public class Поставщик : AuditableEntity
{
    public int Id { get; set; }
    public string Название { get; set; } = null!;
    public string? Город { get; set; }
    public string? Телефон { get; set; }
}
