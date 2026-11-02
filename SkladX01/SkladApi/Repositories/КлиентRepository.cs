using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class КлиентRepository : IКлиентRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public КлиентRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<КлиентDto>> GetAllAsync(string? phone, string? alias, string? city)
    {
        var sql = """
            SELECT [Id], [Псевдоним], [Фио], [Телефон], [Город], [ПределКредита]
            FROM dbo.[Клиент]
            WHERE 1 = 1
            """;

        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(phone))
        {
            sql += " AND [Телефон] LIKE @phone";
            parameters["phone"] = $"%{phone}%";
        }

        if (!string.IsNullOrWhiteSpace(alias))
        {
            sql += " AND [Псевдоним] LIKE @alias";
            parameters["alias"] = $"%{alias}%";
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            sql += " AND [Город] LIKE @city";
            parameters["city"] = $"%{city}%";
        }

        sql += " ORDER BY [Id]";

        await using var connection = new SqlConnection(_connectionString);
        var clients = await connection.QueryAsync<КлиентDto>(sql, parameters);

        return clients.ToList();
    }

    public async Task<КлиентDto?> GetByIdAsync(int id)
    {
        const string sql = """
            SELECT [Id], [Псевдоним], [Фио], [Телефон], [Город], [ПределКредита]
            FROM dbo.[Клиент]
            WHERE [Id] = @id
            """;

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<КлиентDto>(sql, new { id });
    }
}
