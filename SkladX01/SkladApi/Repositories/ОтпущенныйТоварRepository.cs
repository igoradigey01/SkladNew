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

    public async Task<IReadOnlyList<ОтпущенныйТоварDto>> GetByDealIdAsync(Guid dealId)
    {
        const string sql = """
            SELECT n.[Id] AS Id,
                   n.[Номер] AS Номер,
                   n.[Дата] AS Дата,
                   CAST(SUM(r.[Sum]) AS decimal(18, 2)) AS Сумма,
                   s.[КодСделки] AS [СделкаId]
            FROM dbo.[НакладнаяРасход] AS n
            INNER JOIN dbo.[Расход] AS r
                ON r.[НакладнаяId] = n.[Id]
            INNER JOIN dbo.[Сделка] AS s
                ON s.[КодСделки] = n.[КодСделки]
            WHERE s.[КодСделки] = @dealId
            GROUP BY n.[Id], n.[Номер], n.[Дата], s.[КодСделки]
            ORDER BY n.[Дата], n.[Id]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var products = await connection.QueryAsync<ОтпущенныйТоварDto>(sql, new { dealId });

        return products.ToList();
    }

    public async Task<bool> InvoiceBelongsToDealAsync(
        Guid dealId,
        int invoiceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP 1 1
            FROM dbo.[НакладнаяРасход] AS n
            WHERE n.[Id] = @invoiceId
              AND n.[КодСделки] = @dealId
            """;

        await using var connection = new SqlConnection(_connectionString);
        var found = await connection.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(
                sql, new { dealId, invoiceId }, cancellationToken: cancellationToken));

        return found is not null;
    }

    public async Task<IReadOnlyList<СтрокаНакладнойDto>> GetRowsAsync(
        int invoiceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT r.[Id] AS Id,
                   r.[НоменклатураId] AS НоменклатураId,
                   n.[Название] AS Номенклатура,
                   r.[Quantity] AS Количество,
                   r.[Price] AS Цена,
                   r.[Sum] AS Сумма
            FROM dbo.[Расход] AS r
            INNER JOIN dbo.[Номенклатура] AS n ON n.[Id] = r.[НоменклатураId]
            WHERE r.[НакладнаяId] = @invoiceId
            ORDER BY r.[Id]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<СтрокаНакладнойDto>(
            new CommandDefinition(
                sql, new { invoiceId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
