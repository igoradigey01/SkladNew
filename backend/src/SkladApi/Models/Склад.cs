namespace SkladApi.Models;

public class Склад : AuditableEntity
{
    public int Id { get; set; }
    public string Название { get; set; } = null!;
    public string Псевдоним { get; set; } = null!;
}
