using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class ОтпущенныйТоварRepository : IОтпущенныйТоварRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public ОтпущенныйТоварRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<ОтпущенныйТоварDto>> GetByMarkIdAsync(int markId)
    {
        const string sql = """
            SELECT n.[Id] AS Id,
                   n.[Номер] AS Номер,
                   n.[Дата] AS Дата,
                   CAST(SUM(r.[Sum]) AS decimal(18, 2)) AS Сумма,
                   CAST(s.[LegacyId] AS int) AS [МеткаId]
            FROM dbo.[НакладнаяРасход] AS n
            INNER JOIN dbo.[Расход] AS r
                ON r.[НакладнаяId] = n.[Id]
            INNER JOIN dbo.[Сделка] AS s
                ON s.[КодСделки] = n.[КодСделки]
            WHERE s.[LegacyId] = @markId
            GROUP BY n.[Id], n.[Номер], n.[Дата], s.[LegacyId]
            ORDER BY n.[Дата], n.[Id]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var products = await connection.QueryAsync<ОтпущенныйТоварDto>(sql, new { markId });

        return products.ToList();
    }
}
