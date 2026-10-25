namespace SkladApi.Models;

public class Сделка : AuditableEntity
{
    public Guid КодСделки { get; set; }
    public string? НомерСделки { get; set; }
    public int КлиентId { get; set; }
    public DateTime Дата { get; set; }
    public string? Комментарий { get; set; }
    public int? LegacyId { get; set; }
    public bool Активна { get; set; }
}
