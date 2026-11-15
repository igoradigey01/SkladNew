using Microsoft.AspNetCore.Mvc;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    private readonly IAuditLogRepository _auditLogRepository;

    public AuditController(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    /// <summary>История изменений сущности: invoice или payment.</summary>
    [HttpGet("{entityType}/{entityId:long}")]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> GetHistory(
        string entityType,
        long entityId,
        [FromQuery] int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        var canonicalType = AuditEntityTypes.Normalize(entityType);

        if (canonicalType is null)
        {
            return BadRequest("Неизвестный тип сущности. Ожидается invoice или payment.");
        }

        if (entityId <= 0)
        {
            return BadRequest("Идентификатор сущности должен быть больше нуля.");
        }

        if (limit is < 1 or > MaxLimit)
        {
            return BadRequest($"limit должен быть от 1 до {MaxLimit}.");
        }

        var history = await _auditLogRepository.GetAsync(
            canonicalType, entityId, limit, cancellationToken);

        return Ok(history);
    }
}
