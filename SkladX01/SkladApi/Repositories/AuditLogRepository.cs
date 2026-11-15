using Dapper;
using Microsoft.Data.SqlClient;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

/// <summary>Чтение журнала аудита — по правилу CQRS-lite только Dapper.</summary>
public class AuditLogRepository : IAuditLogRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public AuditLogRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<AuditLogDto>> GetAsync(
        string entityType,
        long entityId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (@limit)
                   a.[Id] AS Id,
                   a.[EntityType] AS EntityType,
                   a.[EntityId] AS EntityId,
                   a.[Action] AS Action,
                   a.[ChangedByEmployeeId] AS ChangedByEmployeeId,
                   a.[ChangedByEmployeeName] AS ChangedByEmployeeName,
                   a.[Timestamp] AS Timestamp,
                   a.[OldData] AS OldData,
                   a.[NewData] AS NewData
            FROM dbo.[AuditLog] AS a
            WHERE a.[EntityType] = @entityType
              AND a.[EntityId] = @entityId
            ORDER BY a.[Timestamp] DESC, a.[Id] DESC
            """;

        await using var connection = new SqlConnection(_connectionString);
        var items = (await connection.QueryAsync<AuditLogDto>(
            new CommandDefinition(
                sql,
                new { entityType, entityId, limit },
                cancellationToken: cancellationToken))).ToList();

        // Dapper отдаёт datetime2 с Kind = Unspecified; без этого JSON уйдёт
        // без «Z», и браузер сдвинет время на часовой пояс.
        foreach (var item in items)
        {
            item.Timestamp = DateTime.SpecifyKind(item.Timestamp, DateTimeKind.Utc);
        }

        return items;
    }
}
