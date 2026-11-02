using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class ПринятыеДеньгиRepository : IПринятыеДеньгиRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public ПринятыеДеньгиRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<ПринятыеДеньгиDto>> GetByMarkIdAsync(int markId)
    {
        const string sql = """
            SELECT m.[Id] AS Id,
                   m.[Date] AS Дата,
                   m.[Sum] AS Сумма,
                   CAST(@markId AS int) AS [МеткаId]
            FROM dbo.[Деньги] AS m
            INNER JOIN dbo.[Сделка] AS s
                ON s.[КодСделки] = m.[КодСделки]
            WHERE s.[LegacyId] = @markId
            ORDER BY m.[Date]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var payments = await connection.QueryAsync<ПринятыеДеньгиDto>(sql, new { markId });

        return payments.ToList();
    }
}
