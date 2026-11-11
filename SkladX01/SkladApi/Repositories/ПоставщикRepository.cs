using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class ПоставщикRepository : IПоставщикRepository
{
    private const string ConnectionStringKey = "SkladDb";

    // Белый список: в SQL попадают только эти константы, никогда — строка из запроса.
    private static readonly Dictionary<string, string> SortColumns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "[Id]",
            ["name"] = "[Название]",
            ["city"] = "[Город]",
        };

    private readonly string _connectionString;

    public ПоставщикRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString(ConnectionStringKey)!;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                $"Строка подключения '{ConnectionStringKey}' не задана. " +
                "Задайте её через user-secrets или переменные окружения.");
        }
    }

    public async Task<IReadOnlyList<ПоставщикDto>> GetAllAsync(
        string? query, string sort, bool descending)
    {
        var sortColumn = SortColumns.TryGetValue(sort, out var column)
            ? column
            : SortColumns["name"];

        var direction = descending ? "DESC" : "ASC";

        var sql = """
            SELECT [Id], [Название], [Город], [Телефон]
            FROM dbo.[Поставщик]
            WHERE 1 = 1
            """;

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query))
        {
            sql += """
                 AND ([Название] LIKE @query
                      OR [Город] LIKE @query
                      OR [Телефон] LIKE @query)
                """;
            parameters.Add("query", $"%{query.Trim()}%");
        }

        // [Id] обязателен вторым ключом: без него порядок равных значений «прыгает».
        sql += sortColumn == "[Id]"
            ? $" ORDER BY [Id] {direction}"
            : $" ORDER BY {sortColumn} {direction}, [Id] {direction}";

        await using var connection = new SqlConnection(_connectionString);
        var suppliers = await connection.QueryAsync<ПоставщикDto>(sql, parameters);

        return suppliers.ToList();
    }

    public async Task<ПоставщикDto?> GetByIdAsync(int id)
    {
        const string sql = """
            SELECT [Id], [Название], [Город], [Телефон]
            FROM dbo.[Поставщик]
            WHERE [Id] = @id
            """;

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<ПоставщикDto>(sql, new { id });
    }
}
