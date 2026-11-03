using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IКлиентRepository
{
    Task<IReadOnlyList<КлиентDto>> GetAllAsync(
        string? phone,
        string? alias,
        string? city,
        string? query,
        string sort,
        bool descending,
        bool activeOnly);

    Task<КлиентDto?> GetByIdAsync(int id);

    Task<КлиентSummaryDto?> GetSummaryAsync(int clientId);
}
