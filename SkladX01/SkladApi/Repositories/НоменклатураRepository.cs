using Dapper;
using Microsoft.Data.SqlClient;
using SkladApi.Dtos;

namespace SkladApi.Repositories;

public class НоменклатураRepository : IНоменклатураRepository
{
    private const string ConnectionStringKey = "SkladDb";

    private readonly string _connectionString;

    public НоменклатураRepository(IConfiguration configuration)
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

    public async Task<IReadOnlyList<НоменклатураDto>> GetActiveAsync()
    {
        const string sql = """
            SELECT [Id] AS Id,
                   [Название] AS Название,
                   [Артикул] AS Артикул
            FROM dbo.[Номенклатура]
            WHERE [Активен] = 1
            ORDER BY [Название]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var products = await connection.QueryAsync<НоменклатураDto>(sql);

        return products.ToList();
    }

    public async Task EnsureVectorColumnAsync()
    {
        const string sql = """
            IF COL_LENGTH('dbo.Номенклатура', 'Вектор') IS NULL
                ALTER TABLE dbo.[Номенклатура] ADD [Вектор] varbinary(3072) NULL;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql);
    }

    public async Task<IReadOnlyList<НоменклатураВекторDto>> GetWithoutVectorAsync()
    {
        const string sql = """
            SELECT [Id] AS Id, [Название] AS Название
            FROM dbo.[Номенклатура]
            WHERE [Активен] = 1
              AND ([Вектор] IS NULL OR DATALENGTH([Вектор]) <> 3072)
            ORDER BY [Id]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<НоменклатураВекторDto>(sql);

        return rows.ToList();
    }

    public async Task<IReadOnlyList<НоменклатураВекторDto>> GetWithVectorAsync()
    {
        const string sql = """
            SELECT [Id] AS Id, [Название] AS Название,
                   [ЦенаПродажная] AS ЦенаПродажная, [Вектор] AS Вектор
            FROM dbo.[Номенклатура]
            WHERE [Активен] = 1
              AND [Вектор] IS NOT NULL
              AND DATALENGTH([Вектор]) = 3072
            ORDER BY [Id]
            """;

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<НоменклатураВекторDto>(sql);

        return rows.ToList();
    }

    public async Task UpdateVectorAsync(int id, byte[] vector)
    {
        const string sql = """
            UPDATE dbo.[Номенклатура] SET [Вектор] = @vector WHERE [Id] = @id
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { id, vector });
    }

    public async Task<IReadOnlyList<НоменклатураQuickDto>> QuickSearchAsync(
        string query, int limit)
    {
        const string sql = """
            SELECT TOP (@limit)
                   n.[Id]              AS Id,
                   n.[Код]             AS Код,
                   n.[Артикул]         AS Артикул,
                   n.[Название]        AS Название,
                   e.[Сокращение]      AS ЕдИзм,
                   n.[ЦенаЗакупочная]  AS ЦенаЗакупочная,
                   n.[ЦенаПродажная]   AS ЦенаПродажная
            FROM dbo.[Номенклатура] AS n
            JOIN dbo.[ЕдИзмерения] AS e ON e.[Id] = n.[БазоваяЕдИзмId]
            WHERE n.[Активен] = 1
              AND (n.[Код] LIKE @prefix ESCAPE '\'
                   OR n.[Артикул] LIKE @contains ESCAPE '\'
                   OR n.[Название] LIKE @contains ESCAPE '\')
            ORDER BY n.[Название]
            """;

        var escaped = EscapeLike(query.Trim());

        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<НоменклатураQuickDto>(sql, new
        {
            limit = Math.Clamp(limit, 1, 50),
            prefix = $"{escaped}%",
            contains = $"%{escaped}%",
        });

        return rows.ToList();
    }

    public async Task<string> GetNextCodeAsync()
    {
        const string sql = """
            SELECT MAX(TRY_CAST(SUBSTRING([Код], 4, 32) AS int))
            FROM dbo.[Номенклатура]
            WHERE [Код] LIKE 'НМ-%'
            """;

        await using var connection = new SqlConnection(_connectionString);
        var max = await connection.ExecuteScalarAsync<int?>(sql);

        return FormatNextCode(max);
    }

    /// <summary>НМ-00001, если кодов ещё нет; иначе максимальный номер + 1.</summary>
    public static string FormatNextCode(int? maxNumber)
    {
        return $"НМ-{(maxNumber ?? 0) + 1:D5}";
    }

    public async Task<НоменклатураReferencesDto> GetReferencesAsync()
    {
        const string groupsSql = """
            SELECT [Id] AS Id, [Название] AS Название
            FROM dbo.[ГруппаНоменклатуры]
            ORDER BY [Название]
            """;

        const string kindsSql = """
            SELECT [Id] AS Id, [Название] AS Название
            FROM dbo.[ВидНоменклатуры]
            WHERE [Активен] = 1
            ORDER BY [Название]
            """;

        const string categoriesSql = """
            SELECT [Id] AS Id, [Название] AS Название
            FROM dbo.[КатегорияНоменклатуры]
            ORDER BY [Название]
            """;

        const string unitsSql = """
            SELECT [Id] AS Id, [Название] AS Название
            FROM dbo.[ЕдИзмерения]
            ORDER BY [Название]
            """;

        await using var connection = new SqlConnection(_connectionString);

        return new НоменклатураReferencesDto
        {
            Группы = (await connection.QueryAsync<СправочникDto>(groupsSql)).ToList(),
            Виды = (await connection.QueryAsync<СправочникDto>(kindsSql)).ToList(),
            Категории = (await connection.QueryAsync<СправочникDto>(categoriesSql)).ToList(),
            Единицы = (await connection.QueryAsync<СправочникDto>(unitsSql)).ToList(),
        };
    }

    public async Task<IReadOnlyList<ХарактеристикаВидаDto>> GetCharacteristicsAsync(int видId)
    {
        const string characteristicsSql = """
            SELECT h.[Id]               AS Id,
                   h.[Название]         AS Название,
                   t.[Название]         AS ТипЗначения,
                   h.[Обязательный]     AS Обязательный,
                   h.[Минимум]          AS Минимум,
                   h.[Максимум]         AS Максимум,
                   h.[МаксимальнаяДлина] AS МаксимальнаяДлина
            FROM dbo.[Характеристика] AS h
            JOIN dbo.[ТипЗначения] AS t ON t.[Id] = h.[ТипЗначенияId]
            WHERE h.[ВидНоменклатурыId] = @видId
              AND h.[Активен] = 1
            ORDER BY h.[Порядок], h.[Название]
            """;

        const string valuesSql = """
            SELECT s.[ХарактеристикаId] AS ХарактеристикаId,
                   s.[Значение]         AS Значение
            FROM dbo.[СписокЗначений] AS s
            JOIN dbo.[Характеристика] AS h ON h.[Id] = s.[ХарактеристикаId]
            WHERE h.[ВидНоменклатурыId] = @видId
            ORDER BY s.[Порядок], s.[Значение]
            """;

        await using var connection = new SqlConnection(_connectionString);

        var rows = (await connection.QueryAsync<ХарактеристикаВидаDto>(
            characteristicsSql, new { видId })).ToList();

        var values = (await connection.QueryAsync<СписокЗначенийRow>(
            valuesSql, new { видId })).ToList();

        return rows
            .Select(row => new ХарактеристикаВидаDto
            {
                Id = row.Id,
                Название = row.Название,
                ТипЗначения = row.ТипЗначения,
                Обязательный = row.Обязательный,
                Минимум = row.Минимум,
                Максимум = row.Максимум,
                МаксимальнаяДлина = row.МаксимальнаяДлина,
                СписокЗначений = values
                    .Where(item => item.ХарактеристикаId == row.Id)
                    .Select(item => item.Значение)
                    .ToList(),
            })
            .ToList();
    }

    public async Task<int> CreateAsync(СоздатьНоменклатуруRequest request)
    {
        const string insertNomenclatureSql = """
            INSERT INTO dbo.[Номенклатура]
                ([Код], [Артикул], [Название], [КраткоеНазвание],
                 [ГруппаId], [КатегорияId], [ТипId], [ВидНоменклатурыId],
                 [БазоваяЕдИзмId], [ЦенаЗакупочная], [ЦенаПродажная], [Активен])
            VALUES
                (@Код, @Артикул, @Название, @КраткоеНазвание,
                 @ГруппаId, @КатегорияId, @ТипId, @ВидНоменклатурыId,
                 @БазоваяЕдИзмId, @ЦенаЗакупочная, @ЦенаПродажная, 1);

            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        const string insertCharacteristicSql = """
            INSERT INTO dbo.[ЗначениеХарактеристики]
                ([НоменклатураId], [ХарактеристикаId], [Значение])
            VALUES (@newId, @characteristicId, @value)
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var newId = await connection.ExecuteScalarAsync<int>(
                insertNomenclatureSql,
                new
                {
                    Код = request.Код.Trim(),
                    Артикул = string.IsNullOrWhiteSpace(request.Артикул)
                        ? null
                        : request.Артикул.Trim(),
                    Название = request.Название.Trim(),
                    КраткоеНазвание = string.IsNullOrWhiteSpace(request.КраткоеНазвание)
                        ? null
                        : request.КраткоеНазвание.Trim(),
                    request.ГруппаId,
                    request.КатегорияId,
                    request.ТипId,
                    request.ВидНоменклатурыId,
                    request.БазоваяЕдИзмId,
                    request.ЦенаЗакупочная,
                    request.ЦенаПродажная,
                },
                transaction);

            foreach (var characteristic in NormalizeCharacteristics(request.Характеристики))
            {
                await connection.ExecuteAsync(
                    insertCharacteristicSql,
                    new
                    {
                        newId,
                        characteristicId = characteristic.ХарактеристикаId,
                        value = characteristic.Значение,
                    },
                    transaction);
            }

            await transaction.CommitAsync();

            return newId;
        }
        catch
        {
            // Триггер проверки значений (THROW 51002) делает ROLLBACK сам,
            // после чего транзакция уже завершена и повторный откат бросает
            // InvalidOperationException — он не должен скрывать исходную ошибку.
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
                when (rollbackException is InvalidOperationException or SqlException)
            {
                // Транзакция уже откачена базой.
            }

            throw;
        }
    }

    /// <summary>
    /// Пустое значение характеристики означает «не задано»: строку не пишем,
    /// и тогда действует значение по умолчанию вида (иначе триггер
    /// TR_ЗначениеХарактеристики_Проверка отклонит пустую строку для List).
    /// Дубли по характеристике схлопываем: (НоменклатураId, ХарактеристикаId)
    /// уникальны.
    /// </summary>
    public static IReadOnlyList<ЗначениеХарактеристикиRequest> NormalizeCharacteristics(
        IEnumerable<ЗначениеХарактеристикиRequest> characteristics)
    {
        return characteristics
            .Where(item => !string.IsNullOrWhiteSpace(item.Значение))
            .GroupBy(item => item.ХарактеристикаId)
            .Select(group => new ЗначениеХарактеристикиRequest
            {
                ХарактеристикаId = group.Key,
                Значение = group.Last().Значение.Trim(),
            })
            .ToList();
    }

    private static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

    private sealed class СписокЗначенийRow
    {
        public int ХарактеристикаId { get; set; }
        public string Значение { get; set; } = null!;
    }
}
