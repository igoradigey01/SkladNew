namespace SkladApi.Services;

/// <summary>Автор изменения: Id и имя сотрудника на момент записи.</summary>
public sealed record EmployeeInfo(long Id, string Name);

/// <summary>
/// Текущий сотрудник для журнала аудита. Берётся из claims (если появится
/// аутентификация) либо из мока в конфигурации, связанного с dbo.Сотрудник.
/// </summary>
public interface IEmployeeContext
{
    Task<EmployeeInfo> GetAsync(CancellationToken cancellationToken = default);
}
