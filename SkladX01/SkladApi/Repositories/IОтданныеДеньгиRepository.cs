using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IОтданныеДеньгиRepository
{
    Task<IReadOnlyList<ОтданныеДеньгиDto>> GetByDealIdAsync(Guid dealId);

    Task<ОтданныеДеньгиDto?> GetByIdAsync(
        Guid dealId,
        int paymentId,
        CancellationToken cancellationToken = default);
}
