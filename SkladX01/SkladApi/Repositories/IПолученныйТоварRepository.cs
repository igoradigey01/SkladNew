using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IПолученныйТоварRepository
{
    Task<IReadOnlyList<ПолученныйТоварDto>> GetByDealIdAsync(Guid dealId);

    Task<bool> ReceiptBelongsToDealAsync(
        Guid dealId,
        Guid receiptId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<СтрокаПриходаDto>> GetRowsAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default);

    /// <summary>Создаёт приходную накладную поставщика (строки в dbo.Приход).</summary>
    Task<СозданиеПриходаРезультат> CreateAsync(
        Guid dealId,
        DateTime? date,
        string? comment,
        IReadOnlyList<СоздатьСтрокуПриходаDto> rows,
        CancellationToken cancellationToken = default);
}
