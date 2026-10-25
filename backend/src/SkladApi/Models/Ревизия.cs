namespace SkladApi.Models;

public class Ревизия : AuditableEntity
{
    public string Code { get; set; } = null!;
    public int Type { get; set; }
    public DateTime DateStart { get; set; }
    public DateTime DateEnd { get; set; }
    public int Status { get; set; }
    public DateTime DateCreated { get; set; }
}
