using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IПринятыеДеньгиRepository
{
    Task<IReadOnlyList<ПринятыеДеньгиDto>> GetByDealIdAsync(Guid dealId);
}
