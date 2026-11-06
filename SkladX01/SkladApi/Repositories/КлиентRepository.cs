using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class КлиентRepository : IКлиентRepository
{
    private const string ConnectionStringKey = "SkladDb";

    // Белый список: в SQL попадают только эти константы, никогда — строка из запроса.
    private static readonly Dictionary<string, string> SortColumns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = "[Id]",
            ["alias"] = "[Псевдоним]",
            ["name"] = "[Фио]",
            ["phone"] = "[Телефон]",
            ["city"] = "[Город]",
        };

    private readonly string _connectionString;

    public КлиентRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString(ConnectionStringKey)!;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                $"Строка подключения '{ConnectionStringKey}' не задана. " +
                "Задайте её через user-secrets или переменные окружения.");
        }
    }

    public async Task<IReadOnlyList<КлиентDto>> GetAllAsync(
        string? phone,
        string? alias,
        string? city,
        string? query,
        string sort,
        bool descending,
        bool activeOnly)
    {
        var sortColumn = SortColumns.TryGetValue(sort, out var column)
            ? column
            : SortColumns["alias"];

        // Направление — только из двух констант.
        var direction = descending ? "DESC" : "ASC";

        var sql = """
            SELECT [Id], [Псевдоним], [Фио], [Телефон], [Город], [ПределКредита], [Активен]
            FROM dbo.[Клиент]
            WHERE 1 = 1
            """;

        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(phone))
        {
            sql += " AND [Телефон] LIKE @phone";
            parameters.Add("phone", $"%{phone.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(alias))
        {
            sql += " AND [Псевдоним] LIKE @alias";
            parameters.Add("alias", $"%{alias.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            sql += " AND [Город] LIKE @city";
            parameters.Add("city", $"%{city.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            sql += """
                 AND ([Псевдоним] LIKE @query
                      OR [Фио] LIKE @query
                      OR [Телефон] LIKE @query)
                """;
            parameters.Add("query", $"%{query.Trim()}%");
        }

        if (activeOnly)
        {
            sql += " AND [Активен] = 1";
        }

        // [Id] обязателен: без него порядок равных значений «прыгает».
        // При сортировке по самому [Id] второй ключ не добавляем — SQL Server
        // падает с «A column has been specified more than once in the order by list».
        sql += sortColumn == "[Id]"
            ? $" ORDER BY [Id] {direction}"
            : $" ORDER BY {sortColumn} {direction}, [Id] {direction}";

        await using var connection = new SqlConnection(_connectionString);
        var clients = await connection.QueryAsync<КлиентDto>(sql, parameters);

        return clients.ToList();
    }

    public async Task<КлиентDto?> GetByIdAsync(int id)
    {
        const string sql = """
            SELECT [Id], [Псевдоним], [Фио], [Телефон], [Город], [ПределКредита], [Активен]
            FROM dbo.[Клиент]
            WHERE [Id] = @id
            """;

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<КлиентDto>(sql, new { id });
    }

    public async Task<КлиентSummaryDto?> GetSummaryAsync(int clientId)
    {
        const string sql = """
            SELECT [КлиентId], [Клиент] AS [Псевдоним], [ПределКредита], [Активен],
                   [Сделок], [АктивныхСделок], [Отгружено], [Оплачено], [Долг],
                   [Переплата], [ПревышенЛимит]
            FROM dbo.[vw_БалансКлиента]
            WHERE [КлиентId] = @clientId
            """;

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<КлиентSummaryDto>(
            sql, new { clientId });
    }
}
