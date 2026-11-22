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

    public async Task<СозданиеПриходаРезультат> CreateAsync(
        Guid dealId,
        DateTime? date,
        string? comment,
        IReadOnlyList<СоздатьСтрокуПриходаDto> rows,
        CancellationToken cancellationToken = default)
    {
        var дата = date ?? DateTime.Now;
        var uetr = Guid.NewGuid();
        var комментарий = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var поставщикId = await connection.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(
                    "SELECT [ПоставщикId] FROM dbo.[СделкаПоставщика] WHERE [КодСделки] = @dealId",
                    new { dealId },
                    transaction,
                    cancellationToken: cancellationToken));

            if (поставщикId is null)
            {
                return new СозданиеПриходаРезультат(
                    СозданиеПриходаСтатус.СделкаНеНайдена);
            }

            var warehouseId = await connection.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(
                    "SELECT TOP 1 [Id] FROM dbo.[Склад] ORDER BY [Id]",
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (warehouseId is null)
            {
                return new СозданиеПриходаРезультат(
                    СозданиеПриходаСтатус.НетСклада);
            }

            var ids = rows
                .Select(row => row.НоменклатураId)
                .Distinct()
                .ToArray();

            var existing = (await connection.QueryAsync<int>(
                new CommandDefinition(
                    "SELECT [Id] FROM dbo.[Номенклатура] WHERE [Id] IN @ids",
                    new { ids },
                    transaction,
                    cancellationToken: cancellationToken))).ToHashSet();

            var missing = ids
                .Where(id => !existing.Contains(id))
                .Select(id => (int?)id)
                .FirstOrDefault();

            if (missing is not null)
            {
                return new СозданиеПриходаРезультат(
                    СозданиеПриходаСтатус.НоменклатураНеНайдена,
                    НоменклатураId: missing);
            }

            var lines = rows.Select(row => new
            {
                row.НоменклатураId,
                row.Количество,
                row.Цена,
                Sum = Math.Round(
                    row.Количество * row.Цена, 2, MidpointRounding.AwayFromZero),
            }).ToArray();

            // Тип 1 (закупка) обязателен: TR_Приход_Проверка сверяет Type и ПоставщикId.
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO dbo.[Приход]
                        ([UETR], [Date], [Type], [ПоставщикId], [КодСделкиПоставщика],
                         [НоменклатураId], [СкладId], [Quantity], [Price], [Sum], [Comment])
                    VALUES
                        (@uetr, @дата, 1, @поставщикId, @dealId,
                         @НоменклатураId, @warehouseId, @Количество, @Цена, @Sum, @комментарий)
                    """,
                    lines.Select(line => new
                    {
                        uetr,
                        дата,
                        поставщикId,
                        dealId,
                        warehouseId,
                        line.НоменклатураId,
                        line.Количество,
                        line.Цена,
                        line.Sum,
                        комментарий,
                    }),
                    transaction,
                    cancellationToken: cancellationToken));

            var сумма = Math.Round(
                lines.Sum(line => line.Sum), 2, MidpointRounding.AwayFromZero);

            await transaction.CommitAsync(cancellationToken);

            return new СозданиеПриходаРезультат(
                СозданиеПриходаСтатус.Успех,
                new ПолученныйТоварDto
                {
                    Id = uetr,
                    Номер = "П-" + uetr.ToString("N")[^6..].ToUpperInvariant(),
                    Дата = дата,
                    Сумма = сумма,
                    СделкаId = dealId,
                    AuditId = 0,
                    Изменялась = false,
                });
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
