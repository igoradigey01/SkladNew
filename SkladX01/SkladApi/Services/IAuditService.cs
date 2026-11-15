namespace SkladApi.Services;

/// <summary>
/// Готовит запись журнала аудита. Не вызывает SaveChanges: строка аудита
/// добавляется в тот же SkladContext, что и изменение сущности, и коммитится
/// одним SaveChangesAsync в контроллере.
/// </summary>
public interface IAuditService
{
    Task LogChangeAsync(
        string entityType,
        long entityId,
        string action,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken = default);
}
