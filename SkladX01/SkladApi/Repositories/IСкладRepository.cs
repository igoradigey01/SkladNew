using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IСкладRepository
{
    Task<IReadOnlyList<СкладDto>> GetAllAsync();
}
