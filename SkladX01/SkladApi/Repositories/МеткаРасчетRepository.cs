using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class МеткаРасчетRepository : IMеткаРасчетRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public МеткаРасчетRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString(ConnectionStringKey)
            ?? throw new InvalidOperationException(
                $"Строка подключения '{ConnectionStringKey}' не задана. " +
                "Задайте её через dotnet user-secrets или переменные окружения.");

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                $"Строка подключения '{ConnectionStringKey}' не задана. " +
                "Задайте её через dotnet user-secrets или переменные окружения.");
        }
    }

    public async Task<IReadOnlyList<МеткаРасчетDto>> GetByClientIdAsync(int clientId)
    {
        const string sql = """
            SELECT CAST(s.[LegacyId] AS int) AS Id,
                   s.[НомерСделки] AS Номер,
                   v.[Долг] AS Долг
            FROM dbo.[Сделка] AS s
            INNER JOIN dbo.[vw_БалансСделки] AS v
                ON v.[КодСделки] = s.[КодСделки]
            WHERE s.[КлиентId] = @clientId
            ORDER BY s.[НомерСделки]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var marks = await connection.QueryAsync<МеткаРасчетDto>(sql, new { clientId });

        return marks.ToList();
    }
}
