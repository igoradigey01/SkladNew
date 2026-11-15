using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IПринятыеДеньгиRepository
{
    Task<IReadOnlyList<ПринятыеДеньгиDto>> GetByDealIdAsync(Guid dealId);

    Task<ПринятыеДеньгиDto?> GetByIdAsync(
        Guid dealId,
        int paymentId,
        CancellationToken cancellationToken = default);
}
