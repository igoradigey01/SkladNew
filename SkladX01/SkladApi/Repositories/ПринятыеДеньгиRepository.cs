using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class ПринятыеДеньгиRepository : IПринятыеДеньгиRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public ПринятыеДеньгиRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<ПринятыеДеньгиDto>> GetByDealIdAsync(Guid dealId)
    {
        const string sql = """
            SELECT m.[Id] AS Id,
                   m.[Date] AS Дата,
                   m.[Sum] AS Сумма,
                   s.[КодСделки] AS [СделкаId],
                   m.[PaymentMethod] AS СпособОплаты,
                   m.[НомерДокумента] AS НомерДокумента,
                   m.[Comment] AS Комментарий,
                   CAST(CASE WHEN EXISTS
                   (
                       SELECT 1 FROM dbo.[AuditLog] AS a
                       WHERE a.[EntityType] = N'Payment'
                         AND a.[EntityId] = m.[Id]
                   ) THEN 1 ELSE 0 END AS bit) AS Изменялась
            FROM dbo.[Деньги] AS m
            INNER JOIN dbo.[Сделка] AS s
                ON s.[КодСделки] = m.[КодСделки]
            WHERE s.[КодСделки] = @dealId
            ORDER BY m.[Date]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var payments = await connection.QueryAsync<ПринятыеДеньгиDto>(sql, new { dealId });

        return payments.ToList();
    }

    public async Task<ПринятыеДеньгиDto?> GetByIdAsync(
        Guid dealId,
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT m.[Id] AS Id,
                   m.[Date] AS Дата,
                   m.[Sum] AS Сумма,
                   s.[КодСделки] AS [СделкаId],
                   m.[PaymentMethod] AS СпособОплаты,
                   m.[НомерДокумента] AS НомерДокумента,
                   m.[Comment] AS Комментарий
            FROM dbo.[Деньги] AS m
            INNER JOIN dbo.[Сделка] AS s
                ON s.[КодСделки] = m.[КодСделки]
            WHERE m.[Id] = @paymentId
              AND s.[КодСделки] = @dealId
            """;

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<ПринятыеДеньгиDto>(
            new CommandDefinition(
                sql, new { dealId, paymentId }, cancellationToken: cancellationToken));
    }

    public async Task<СозданиеПлатежаРезультат> CreateAsync(
        Guid dealId,
        СоздатьПлатежDto dto,
        CancellationToken cancellationToken = default)
    {
        var дата = dto.Дата ?? DateTime.Now;
        var сумма = Math.Round(dto.Сумма, 2, MidpointRounding.AwayFromZero);
        var способ = dto.СпособОплаты ?? 1;
        var номер = string.IsNullOrWhiteSpace(dto.НомерДокумента)
            ? null
            : dto.НомерДокумента.Trim();
        var комментарий = string.IsNullOrWhiteSpace(dto.Комментарий)
            ? null
            : dto.Комментарий.Trim();

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
                await transaction.RollbackAsync(cancellationToken);

                return new СозданиеПлатежаРезультат(
                    СозданиеПлатежаСтатус.СделкаНеНайдена);
            }

            // OUTPUT без INTO нельзя: на таблице висит триггер проверки контрагента.
            var paymentId = await connection.QuerySingleAsync<int>(
                new CommandDefinition(
                    """
                    DECLARE @Ids TABLE ([Id] int);

                    INSERT INTO dbo.[Деньги]
                        ([UETR], [Date], [OperationType], [CounterpartyId],
                         [CounterpartyType], [КодСделки], [Sum], [PaymentMethod],
                         [НомерДокумента], [Comment])
                    OUTPUT INSERTED.[Id] INTO @Ids
                    VALUES (NEWID(), @дата, 1, @clientId, 1, @dealId, @сумма,
                            @способ, @номер, @комментарий);

                    SELECT [Id] FROM @Ids;
                    """,
                    new { дата, clientId, dealId, сумма, способ, номер, комментарий },
                    transaction,
                    cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            return new СозданиеПлатежаРезультат(
                СозданиеПлатежаСтатус.Успех,
                new ПринятыеДеньгиDto
                {
                    Id = paymentId,
                    Дата = дата,
                    Сумма = сумма,
                    СделкаId = dealId,
                    СпособОплаты = способ,
                    НомерДокумента = номер,
                    Комментарий = комментарий,
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
