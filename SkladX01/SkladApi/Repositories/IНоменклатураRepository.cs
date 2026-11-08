using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IНоменклатураRepository
{
    Task<IReadOnlyList<НоменклатураDto>> GetActiveAsync();
}
