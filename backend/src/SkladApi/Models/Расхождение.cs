using System.ComponentModel.DataAnnotations.Schema;

namespace SkladApi.Models;

public class Расхождение : AuditableEntity
{
    public int Id { get; set; }
    public string КодРевизии { get; set; } = null!;
    public int Type { get; set; }
    public int? НоменклатураId { get; set; }
    public decimal AccountingValue { get; set; }
    public decimal ActualValue { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public decimal Delta { get; set; }

    public string? Comment { get; set; }
}
