namespace SkladApi.Models;

public class НакладнаяРасход : AuditableEntity
{
    public int Id { get; set; }
    public Guid UETR { get; set; }
    public string Номер { get; set; } = null!;
    public DateTime Дата { get; set; }
    public int КлиентId { get; set; }
    public Guid КодСделки { get; set; }
    public string? Комментарий { get; set; }
}
