using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class МеткаРасчетRepository : IMеткаРасчетRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public МеткаРасчетRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<МеткаРасчетDto>> GetByClientIdAsync(int clientId)
    {
        const string sql = """
            SELECT s.[КодСделки] AS Id,
                   s.[НомерСделки] AS Номер,
                   v.[Долг] AS Долг,
                   s.[Комментарий] AS Комментарий
            FROM dbo.[Сделка] AS s
            INNER JOIN dbo.[vw_БалансСделки] AS v
                ON v.[КодСделки] = s.[КодСделки]
            WHERE s.[КлиентId] = @clientId
            ORDER BY s.[НомерСделки]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var marks = await connection.QueryAsync<МеткаРасчетDto>(sql, new { clientId });

        return marks.ToList();
    }

    public async Task<МеткаРасчетDto> CreateAsync(int clientId, DateTime? date, string? comment)
    {
        const string sql = """
            INSERT INTO dbo.[Сделка] ([КодСделки], [КлиентId], [Дата], [Комментарий])
            OUTPUT INSERTED.[КодСделки] AS Id,
                   INSERTED.[НомерСделки] AS Номер,
                   INSERTED.[Комментарий] AS Комментарий
            VALUES (NEWID(), @clientId, ISNULL(@date, GETDATE()), @comment)
            """;

        // DbType задаём явно: с null/DBNull Dapper не может вывести тип параметра.
        var parameters = new DynamicParameters();
        parameters.Add("clientId", clientId);
        parameters.Add("date", date, System.Data.DbType.DateTime);
        parameters.Add(
            "comment",
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            System.Data.DbType.String);

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleAsync<МеткаРасчетDto>(sql, parameters);
    }
}
