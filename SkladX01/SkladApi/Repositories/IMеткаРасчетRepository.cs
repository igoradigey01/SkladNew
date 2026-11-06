using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IMеткаРасчетRepository
{
    Task<IReadOnlyList<МеткаРасчетDto>> GetByClientIdAsync(int clientId);

    Task<МеткаРасчетDto> CreateAsync(int clientId, DateTime? date, string? comment);
}
