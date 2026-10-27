namespace SkladDB.Models;

public class Расход : AuditableEntity
{
    public int Id { get; set; }
    public Guid UETR { get; set; }
    public DateTime Date { get; set; }
    public int Type { get; set; }
    public int КлиентId { get; set; }
    public int? НакладнаяId { get; set; }
    public int НоменклатураId { get; set; }
    public int СкладId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Sum { get; set; }
    public Guid? СотрудникGuid { get; set; }
    public string? Comment { get; set; }
}
