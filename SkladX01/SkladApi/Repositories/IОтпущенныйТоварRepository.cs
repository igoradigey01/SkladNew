using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IОтпущенныйТоварRepository
{
    Task<IReadOnlyList<ОтпущенныйТоварDto>> GetByDealIdAsync(Guid dealId);
}
