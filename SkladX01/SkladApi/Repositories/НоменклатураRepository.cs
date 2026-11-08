using Dapper;
using Microsoft.Data.SqlClient;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class НоменклатураRepository : IНоменклатураRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public НоменклатураRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<НоменклатураDto>> GetActiveAsync()
    {
        const string sql = """
            SELECT [Id] AS Id,
                   [Название] AS Название,
                   [Артикул] AS Артикул
            FROM dbo.[Номенклатура]
            WHERE [Активен] = 1
            ORDER BY [Название]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var products = await connection.QueryAsync<НоменклатураDto>(sql);

        return products.ToList();
    }
}
