using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IКлиентRepository
{
    Task<IReadOnlyList<КлиентDto>> GetAllAsync(string? phone, string? alias, string? city);
    Task<КлиентDto?> GetByIdAsync(int id);
}
