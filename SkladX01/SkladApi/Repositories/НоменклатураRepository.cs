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

    public async Task EnsureVectorColumnAsync()
    {
        const string sql = """
            IF COL_LENGTH('dbo.Номенклатура', 'Вектор') IS NULL
                ALTER TABLE dbo.[Номенклатура] ADD [Вектор] varbinary(3072) NULL;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql);
    }

    public async Task<IReadOnlyList<НоменклатураВекторDto>> GetWithoutVectorAsync()
    {
        const string sql = """
            SELECT [Id] AS Id, [Название] AS Название
            FROM dbo.[Номенклатура]
            WHERE [Активен] = 1
              AND ([Вектор] IS NULL OR DATALENGTH([Вектор]) <> 3072)
            ORDER BY [Id]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<НоменклатураВекторDto>(sql);

        return rows.ToList();
    }

    public async Task<IReadOnlyList<НоменклатураВекторDto>> GetWithVectorAsync()
    {
        const string sql = """
            SELECT [Id] AS Id, [Название] AS Название, [Вектор] AS Вектор
            FROM dbo.[Номенклатура]
            WHERE [Активен] = 1
              AND [Вектор] IS NOT NULL
              AND DATALENGTH([Вектор]) = 3072
            ORDER BY [Id]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<НоменклатураВекторDto>(sql);

        return rows.ToList();
    }

    public async Task UpdateVectorAsync(int id, byte[] vector)
    {
        const string sql = """
            UPDATE dbo.[Номенклатура] SET [Вектор] = @vector WHERE [Id] = @id
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { id, vector });
    }
}
