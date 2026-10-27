namespace SkladDB.Models;

public class Деньги : AuditableEntity
{
    public int Id { get; set; }
    public Guid UETR { get; set; }
    public DateTime Date { get; set; }
    public int OperationType { get; set; }
    public int? CounterpartyId { get; set; }
    public int CounterpartyType { get; set; }
    public Guid? КодСделки { get; set; }
    public decimal Sum { get; set; }
    public int PaymentMethod { get; set; }
    public string? НомерДокумента { get; set; }
    public Guid? СотрудникGuid { get; set; }
    public string? Comment { get; set; }
}
