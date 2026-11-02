using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IПринятыеДеньгиRepository
{
    Task<IReadOnlyList<ПринятыеДеньгиDto>> GetByMarkIdAsync(int markId);
}
