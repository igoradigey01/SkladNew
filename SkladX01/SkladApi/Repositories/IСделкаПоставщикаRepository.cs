using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IСделкаПоставщикаRepository
{
    Task<IReadOnlyList<СделкаПоставщикаDto>> GetBySupplierIdAsync(int supplierId);
}
