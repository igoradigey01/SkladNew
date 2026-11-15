using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IОтпущенныйТоварRepository
{
    Task<IReadOnlyList<ОтпущенныйТоварDto>> GetByDealIdAsync(Guid dealId);

    Task<bool> InvoiceBelongsToDealAsync(
        Guid dealId,
        int invoiceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<СтрокаНакладнойDto>> GetRowsAsync(
        int invoiceId,
        CancellationToken cancellationToken = default);
}
