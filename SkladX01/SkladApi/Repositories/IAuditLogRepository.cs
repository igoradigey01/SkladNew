using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLogDto>> GetAsync(
        string entityType,
        long entityId,
        int limit,
        CancellationToken cancellationToken = default);
}
