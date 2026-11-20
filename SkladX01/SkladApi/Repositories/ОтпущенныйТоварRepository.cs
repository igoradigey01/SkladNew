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
                   s.[КодСделки] AS [СделкаId],
                   CAST(CASE WHEN EXISTS
                   (
                       SELECT 1 FROM dbo.[AuditLog] AS a
                       WHERE a.[EntityType] = N'Invoice'
                         AND a.[EntityId] = n.[Id]
                   ) THEN 1 ELSE 0 END AS bit) AS Изменялась
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

    public async Task<СозданиеНакладнойРезультат> CreateAsync(
        Guid dealId,
        DateTime? date,
        string? comment,
        IReadOnlyList<СоздатьСтрокуНакладнойDto> rows,
        CancellationToken cancellationToken = default)
    {
        var дата = date ?? DateTime.Now;
        var uetr = Guid.NewGuid();
        var номер = "О-" + uetr.ToString("N")[^6..].ToUpperInvariant();
        var комментарий = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var clientId = await connection.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(
                    "SELECT [КлиентId] FROM dbo.[Сделка] WHERE [КодСделки] = @dealId",
                    new { dealId },
                    transaction,
                    cancellationToken: cancellationToken));

            if (clientId is null)
            {
                return new СозданиеНакладнойРезультат(
                    СозданиеНакладнойСтатус.СделкаНеНайдена);
            }

            var warehouseId = await connection.QueryFirstOrDefaultAsync<int?>(
                new CommandDefinition(
                    "SELECT TOP 1 [Id] FROM dbo.[Склад] ORDER BY [Id]",
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (warehouseId is null)
            {
                return new СозданиеНакладнойРезультат(
                    СозданиеНакладнойСтатус.НетСклада);
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
                return new СозданиеНакладнойРезультат(
                    СозданиеНакладнойСтатус.НоменклатураНеНайдена,
                    НоменклатураId: missing);
            }

            // OUTPUT без INTO нельзя: на шапке висит триггер проверки клиента.
            var invoiceId = await connection.QuerySingleAsync<int>(
                new CommandDefinition(
                    """
                    DECLARE @Ids TABLE ([Id] int);

                    INSERT INTO dbo.[НакладнаяРасход]
                        ([UETR], [Номер], [Дата], [КлиентId], [КодСделки], [Комментарий])
                    OUTPUT INSERTED.[Id] INTO @Ids
                    VALUES (@uetr, @номер, @дата, @clientId, @dealId, @комментарий);

                    SELECT [Id] FROM @Ids;
                    """,
                    new { uetr, номер, дата, clientId, dealId, комментарий },
                    transaction,
                    cancellationToken: cancellationToken));

            var lines = rows.Select(row => new
            {
                row.НоменклатураId,
                row.Количество,
                row.Цена,
                Sum = Math.Round(
                    row.Количество * row.Цена, 2, MidpointRounding.AwayFromZero),
            }).ToArray();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO dbo.[Расход]
                        ([UETR], [Date], [Type], [КлиентId], [НакладнаяId],
                         [НоменклатураId], [СкладId], [Quantity], [Price], [Sum])
                    VALUES
                        (@uetr, @дата, 1, @clientId, @invoiceId,
                         @НоменклатураId, @warehouseId, @Количество, @Цена, @Sum)
                    """,
                    lines.Select(line => new
                    {
                        uetr,
                        дата,
                        clientId,
                        invoiceId,
                        warehouseId,
                        line.НоменклатураId,
                        line.Количество,
                        line.Цена,
                        line.Sum,
                    }),
                    transaction,
                    cancellationToken: cancellationToken));

            var сумма = Math.Round(
                lines.Sum(line => line.Sum), 2, MidpointRounding.AwayFromZero);

            await transaction.CommitAsync(cancellationToken);

            return new СозданиеНакладнойРезультат(
                СозданиеНакладнойСтатус.Успех,
                new ОтпущенныйТоварDto
                {
                    Id = invoiceId,
                    Номер = номер,
                    Дата = дата,
                    Сумма = сумма,
                    СделкаId = dealId,
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
