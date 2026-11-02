using SkladApi.Dtos;

namespace SkladApi.Repositories;

public interface IMеткаРасчетRepository
{
    Task<IReadOnlyList<МеткаРасчетDto>> GetByClientIdAsync(int clientId);
}
