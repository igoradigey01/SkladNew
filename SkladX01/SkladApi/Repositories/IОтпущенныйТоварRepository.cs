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

    /// <summary>
    /// Создаёт шапку накладной и её строки в одной транзакции.
    /// </summary>
    Task<СозданиеНакладнойРезультат> CreateAsync(
        Guid dealId,
        DateTime? date,
        string? comment,
        IReadOnlyList<СоздатьСтрокуНакладнойDto> rows,
        CancellationToken cancellationToken = default);
}
