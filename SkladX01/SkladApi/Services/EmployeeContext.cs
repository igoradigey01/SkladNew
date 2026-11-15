using HRDB.Data;
using Microsoft.EntityFrameworkCore;
using SkladDB.Services;

namespace SkladApi.Services;

/// <summary>
/// Пока в API нет логина, автор изменения берётся из мока. Если в запросе
/// появится аутентифицированный пользователь (Identity/claims), приоритет
/// отдаётся его карточке в HRDB. Id сотрудника в HRDB — int, наружу отдаём
/// long, как в AuditLog.
/// </summary>
public sealed class EmployeeContext : IEmployeeContext
{
    private const string MockEmployeeIdKey = "Audit:MockEmployeeId";

    private readonly ICurrentUserService _currentUserService;
    private readonly HRContext _hrContext;
    private readonly IConfiguration _configuration;

    public EmployeeContext(
        ICurrentUserService currentUserService,
        HRContext hrContext,
        IConfiguration configuration)
    {
        _currentUserService = currentUserService;
        _hrContext = hrContext;
        _configuration = configuration;
    }

    public async Task<EmployeeInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var user = await _hrContext.Users
                .AsNoTracking()
                .Include(x => x.Сотрудник)
                .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

            if (user is not null)
            {
                var name = string.IsNullOrWhiteSpace(user.Фио)
                    ? user.Сотрудник?.Имя
                    : user.Фио;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    return new EmployeeInfo(user.СотрудникId, name);
                }
            }
        }

        var mockId = _configuration.GetValue<int?>(MockEmployeeIdKey);

        if (mockId is > 0)
        {
            var employee = await _hrContext.Сотрудники
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == mockId.Value, cancellationToken);

            return employee is null
                ? new EmployeeInfo(mockId.Value, $"Сотрудник #{mockId}")
                : new EmployeeInfo(employee.Id, employee.Имя);
        }

        return new EmployeeInfo(0, "Система");
    }
}
