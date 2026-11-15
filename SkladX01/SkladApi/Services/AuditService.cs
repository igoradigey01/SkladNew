using System.Text.Json;
using SkladDB.Data;
using SkladDB.Models;

namespace SkladApi.Services;

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SkladContext _context;
    private readonly IEmployeeContext _employeeContext;

    public AuditService(SkladContext context, IEmployeeContext employeeContext)
    {
        _context = context;
        _employeeContext = employeeContext;
    }

    public async Task LogChangeAsync(
        string entityType,
        long entityId,
        string action,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeContext.GetAsync(cancellationToken);

        _context.AuditLogs.Add(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            ChangedByEmployeeId = employee.Id,
            ChangedByEmployeeName = employee.Name,
            Timestamp = DateTime.UtcNow,
            OldData = oldValue is null ? null : JsonSerializer.Serialize(oldValue, JsonOptions),
            NewData = newValue is null ? null : JsonSerializer.Serialize(newValue, JsonOptions),
        });
    }
}
