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
                   s.[КодСделки] AS [СделкаId],
                   MIN(i.[Id]) AS AuditId,
                   CAST(MAX(CASE WHEN a.[Id] IS NULL THEN 0 ELSE 1 END)
                        AS bit) AS Изменялась
            FROM dbo.[Приход] AS i
            INNER JOIN dbo.[СделкаПоставщика] AS s
                ON s.[КодСделки] = i.[КодСделкиПоставщика]
            LEFT JOIN dbo.[AuditLog] AS a
                ON a.[EntityType] = N'SupplierInvoice'
               AND a.[EntityId] = i.[Id]
            WHERE s.[КодСделки] = @dealId
            GROUP BY i.[UETR], s.[КодСделки]
            ORDER BY MIN(i.[Date]), i.[UETR]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var products = await connection.QueryAsync<ПолученныйТоварDto>(
            sql, new { dealId });

        return products.ToList();
    }

    public async Task<bool> ReceiptBelongsToDealAsync(
        Guid dealId,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP 1 1
            FROM dbo.[Приход] AS i
            WHERE i.[UETR] = @receiptId
              AND i.[КодСделкиПоставщика] = @dealId
            """;

        await using var connection = new SqlConnection(_connectionString);
        var found = await connection.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(
                sql, new { dealId, receiptId }, cancellationToken: cancellationToken));

        return found is not null;
    }

    public async Task<IReadOnlyList<СтрокаПриходаDto>> GetRowsAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT i.[Id] AS Id,
                   i.[НоменклатураId] AS НоменклатураId,
                   n.[Название] AS Номенклатура,
                   i.[Quantity] AS Количество,
                   i.[Price] AS Цена,
                   i.[Sum] AS Сумма
            FROM dbo.[Приход] AS i
            INNER JOIN dbo.[Номенклатура] AS n ON n.[Id] = i.[НоменклатураId]
            WHERE i.[UETR] = @receiptId
            ORDER BY i.[Id]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<СтрокаПриходаDto>(
            new CommandDefinition(
                sql, new { receiptId }, cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
