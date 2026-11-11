using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class СкладRepository : IСкладRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public СкладRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString(ConnectionStringKey)!;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                $"Строка подключения '{ConnectionStringKey}' не задана. " +
                "Задайте её через user-secrets или переменные окружения.");
        }
    }

    public async Task<IReadOnlyList<СкладDto>> GetAllAsync()
    {
        const string sql = """
            SELECT [Id], [Название], [Псевдоним]
            FROM dbo.[Склад]
            ORDER BY [Название]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var warehouses = await connection.QueryAsync<СкладDto>(sql);

        return warehouses.ToList();
    }
}
