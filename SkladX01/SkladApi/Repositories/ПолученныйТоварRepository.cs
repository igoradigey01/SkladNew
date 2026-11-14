using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class ПолученныйТоварRepository : IПолученныйТоварRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public ПолученныйТоварRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<ПолученныйТоварDto>> GetByDealIdAsync(Guid dealId)
    {
        const string sql = """
            SELECT i.[UETR] AS Id,
                   N'П-' + RIGHT(CONVERT(varchar(36), i.[UETR]), 6) AS Номер,
                   MIN(i.[Date]) AS Дата,
                   CAST(SUM(i.[Sum]) AS decimal(18, 2)) AS Сумма,
                   s.[КодСделки] AS [СделкаId]
            FROM dbo.[Приход] AS i
            INNER JOIN dbo.[СделкаПоставщика] AS s
                ON s.[КодСделки] = i.[КодСделкиПоставщика]
            WHERE s.[КодСделки] = @dealId
            GROUP BY i.[UETR], s.[КодСделки]
            ORDER BY MIN(i.[Date]), i.[UETR]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var products = await connection.QueryAsync<ПолученныйТоварDto>(
            sql, new { dealId });

        return products.ToList();
    }
}
