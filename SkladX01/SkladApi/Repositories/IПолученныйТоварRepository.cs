using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IПолученныйТоварRepository
{
    Task<IReadOnlyList<ПолученныйТоварDto>> GetByDealIdAsync(Guid dealId);
}
