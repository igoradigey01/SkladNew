namespace SkladDB.Services;

/// <summary>
/// Текущий пользователь для аудита. Интерфейс живёт в SkladDB, чтобы слой
/// данных не зависел от ASP.NET Core Identity; реализация — в SkladApi.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
}
