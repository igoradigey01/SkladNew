using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class ОтданныеДеньгиRepository : IОтданныеДеньгиRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public ОтданныеДеньгиRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<ОтданныеДеньгиDto>> GetByDealIdAsync(Guid dealId)
    {
        const string sql = """
            SELECT m.[Id] AS Id,
                   m.[Date] AS Дата,
                   m.[Sum] AS Сумма,
                   s.[КодСделки] AS [СделкаId],
                   CAST(CASE WHEN EXISTS
                   (
                       SELECT 1 FROM dbo.[AuditLog] AS a
                       WHERE a.[EntityType] = N'SupplierPayment'
                         AND a.[EntityId] = m.[Id]
                   ) THEN 1 ELSE 0 END AS bit) AS Изменялась
            FROM dbo.[Деньги] AS m
            INNER JOIN dbo.[СделкаПоставщика] AS s
                ON s.[КодСделки] = m.[КодСделкиПоставщика]
            WHERE s.[КодСделки] = @dealId
            ORDER BY m.[Date], m.[Id]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var payments = await connection.QueryAsync<ОтданныеДеньгиDto>(
            sql, new { dealId });

        return payments.ToList();
    }

    public async Task<ОтданныеДеньгиDto?> GetByIdAsync(
        Guid dealId,
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT m.[Id] AS Id,
                   m.[Date] AS Дата,
                   m.[Sum] AS Сумма,
                   s.[КодСделки] AS [СделкаId]
            FROM dbo.[Деньги] AS m
            INNER JOIN dbo.[СделкаПоставщика] AS s
                ON s.[КодСделки] = m.[КодСделкиПоставщика]
            WHERE s.[КодСделки] = @dealId
              AND m.[Id] = @paymentId
            """;

        await using var connection = new SqlConnection(_connectionString);
        var payment = await connection.QueryFirstOrDefaultAsync<ОтданныеДеньгиDto>(
            new CommandDefinition(
                sql, new { dealId, paymentId }, cancellationToken: cancellationToken));

        return payment;
    }
}
