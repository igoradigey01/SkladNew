using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IПоставщикRepository
{
    Task<IReadOnlyList<ПоставщикDto>> GetAllAsync(
        string? query, string sort, bool descending);

    Task<ПоставщикDto?> GetByIdAsync(int id);
}
