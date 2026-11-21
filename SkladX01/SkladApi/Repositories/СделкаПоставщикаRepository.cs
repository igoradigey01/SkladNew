using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class СделкаПоставщикаRepository : IСделкаПоставщикаRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public СделкаПоставщикаRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<СделкаПоставщикаDto>> GetBySupplierIdAsync(int supplierId)
    {
        const string sql = """
            SELECT s.[КодСделки] AS Id,
                   s.[НомерСделки] AS Номер,
                   v.[Долг] AS Долг,
                   s.[Комментарий] AS Комментарий
            FROM dbo.[СделкаПоставщика] AS s
            INNER JOIN dbo.[vw_БалансСделкиПоставщика] AS v
                ON v.[КодСделки] = s.[КодСделки]
            WHERE s.[ПоставщикId] = @supplierId
            ORDER BY s.[НомерСделки]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var deals = await connection.QueryAsync<СделкаПоставщикаDto>(
            sql, new { supplierId });

        return deals.ToList();
    }
}
