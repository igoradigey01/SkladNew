using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/nomenclature")]
public class NomenclatureController : ControllerBase
{
    // Ограничения dbo.Номенклатура: long values otherwise fail with SQL 2628.
    private const int МаксимальнаяДлинаКода = 20;
    private const int МаксимальнаяДлинаНазвания = 200;
    private const int МаксимальнаяДлинаАртикула = 50;
    private const int МаксимальнаяДлинаКраткогоНазвания = 50;

    private readonly IНоменклатураRepository _номенклатура;
    private readonly OllamaEmbeddingService _embeddings;
    private readonly ILogger<NomenclatureController> _logger;

    public NomenclatureController(
        IНоменклатураRepository номенклатура,
        OllamaEmbeddingService embeddings,
        ILogger<NomenclatureController> logger)
    {
        _номенклатура = номенклатура;
        _embeddings = embeddings;
        _logger = logger;
    }

    /// <summary>Активная номенклатура; поиск по названию или артикулу.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<НоменклатураDto>>> Get(
        [FromQuery] string? q,
        [FromQuery] int limit = 50)
    {
        var products = await _номенклатура.GetActiveAsync();

        if (!string.IsNullOrWhiteSpace(q))
        {
            products = products
                .Where(product =>
                    product.Название.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (product.Артикул?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        return Ok(products.Take(Math.Clamp(limit, 1, 200)).ToList());
    }

    /// <summary>Семантический поиск по названию: косинус между векторами.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Поисковый запрос пуст." });
        }

        float[] query;

        try
        {
            query = await _embeddings.GetEmbeddingAsync(q.Trim(), cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Ollama embeddings недоступна");

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = "Сервис эмбеддингов недоступен (Ollama на ks34)." });
        }

        var rows = await _номенклатура.GetWithVectorAsync();

        var scored = rows
            .Select(row => new
            {
                row.Id,
                name = row.Название,
                score = Math.Round(Cosine(query, ToFloats(row.Вектор)), 4),
            })
            .OrderByDescending(item => item.score)
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();

        return Ok(scored);
    }

    /// <summary>
    /// Кандидаты для ручного выбора позиции: гибрид косинуса и Левенштейна,
    /// кандидаты с несовпадающими размерами отбрасываются.
    /// </summary>
    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates(
        [FromQuery] string? q,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Поисковый запрос пуст." });
        }

        var text = q.Trim();
        var query = await _embeddings.GetEmbeddingAsync(text, cancellationToken);
        var rows = await _номенклатура.GetWithVectorAsync();

        var scored = rows
            .Where(row => !FuzzyMatcher.NumbersConflict(text, row.Название))
            .Select(row =>
            {
                var cosine = Cosine(query, ToFloats(row.Вектор));
                var lexical = FuzzyMatcher.Similarity(text, row.Название);

                return new
                {
                    row.Id,
                    name = row.Название,
                    price = row.ЦенаПродажная,
                    score = Math.Round(cosine * 50 + lexical * 0.5, 1),
                    cosine = Math.Round(cosine, 3),
                    lexical,
                };
            })
            .OrderByDescending(item => item.score)
            .Take(Math.Clamp(limit, 1, 20))
            .ToList();

        return Ok(scored);
    }

    /// <summary>
    /// Пересобирает векторы номенклатуры: добавляет колонку [Вектор], если её нет,
    /// и считает эмбеддинги для строк, где вектор пуст или неверной длины.
    /// </summary>
    [HttpPost("embeddings/rebuild")]
    public async Task<IActionResult> RebuildEmbeddings(
        [FromQuery] int batch = 16,
        CancellationToken cancellationToken = default)
    {
        await _номенклатура.EnsureVectorColumnAsync();

        var pending = await _номенклатура.GetWithoutVectorAsync();
        var updated = 0;
        var failed = 0;

        foreach (var chunk in pending.Chunk(Math.Clamp(batch, 1, 64)))
        {
            try
            {
                var vectors = await _embeddings.GetEmbeddingsAsync(
                    chunk.Select(item => item.Название).ToList(), cancellationToken);

                for (var index = 0; index < chunk.Length; index++)
                {
                    await _номенклатура.UpdateVectorAsync(chunk[index].Id, ToBytes(vectors[index]));
                    updated++;
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Не удалось посчитать эмбеддинги для пачки");
                failed += chunk.Length;
            }
        }

        return Ok(new { pending = pending.Count, updated, failed });
    }

    /// <summary>
    /// Быстрый поиск для ввода с клавиатуры: точный LIKE по Код/Артикул/Название,
    /// без эмбеддингов и без обращения к Ollama.
    /// </summary>
    [HttpGet("quick-search")]
    public async Task<ActionResult<IReadOnlyList<НоменклатураQuickDto>>> QuickSearch(
        [FromQuery] string? q,
        [FromQuery] int limit = 15)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<НоменклатураQuickDto>());
        }

        var rows = await _номенклатура.QuickSearchAsync(q, limit);

        return Ok(rows);
    }

    /// <summary>Следующий свободный код для быстрого создания позиции.</summary>
    [HttpGet("next-code")]
    public async Task<IActionResult> NextCode()
    {
        try
        {
            return Ok(new { код = await _номенклатура.GetNextCodeAsync() });
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Не удалось вычислить следующий код номенклатуры");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "Справочник номенклатуры недоступен: проверьте SQL Server." });
        }
    }

    /// <summary>Справочники для быстрого создания номенклатуры «на лету».</summary>
    [HttpGet("references")]
    public async Task<ActionResult<НоменклатураReferencesDto>> References()
    {
        return Ok(await _номенклатура.GetReferencesAsync());
    }

    /// <summary>Характеристики вида номенклатуры (для динамической секции формы).</summary>
    [HttpGet("kinds/{видId:int}/characteristics")]
    public async Task<ActionResult<IReadOnlyList<ХарактеристикаВидаDto>>> Characteristics(
        int видId)
    {
        return Ok(await _номенклатура.GetCharacteristicsAsync(видId));
    }

    /// <summary>
    /// Создание номенклатуры «на лету»: карточка + значения характеристик
    /// в одной транзакции. Код должен быть уникальным (иначе 409).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] СоздатьНоменклатуруRequest? request)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Тело запроса пусто." });
        }

        var ошибки = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Код))
        {
            ошибки.Add("Код обязателен.");
        }
        else if (request.Код.Trim().Length > МаксимальнаяДлинаКода)
        {
            ошибки.Add($"Код длиннее {МаксимальнаяДлинаКода} символов.");
        }

        if (string.IsNullOrWhiteSpace(request.Название))
        {
            ошибки.Add("Название обязательно.");
        }
        else if (request.Название.Trim().Length > МаксимальнаяДлинаНазвания)
        {
            ошибки.Add($"Название длиннее {МаксимальнаяДлинаНазвания} символов.");
        }

        if (request.Артикул?.Trim().Length > МаксимальнаяДлинаАртикула)
        {
            ошибки.Add($"Артикул длиннее {МаксимальнаяДлинаАртикула} символов.");
        }

        if (request.КраткоеНазвание?.Trim().Length > МаксимальнаяДлинаКраткогоНазвания)
        {
            ошибки.Add(
                $"Краткое название длиннее {МаксимальнаяДлинаКраткогоНазвания} символов.");
        }

        if (request.БазоваяЕдИзмId <= 0)
        {
            ошибки.Add("Базовая единица измерения обязательна.");
        }

        if (request.ТипId is < 1 or > 3)
        {
            ошибки.Add("ТипId должен быть 1 (товар), 2 (услуга) или 3 (материал).");
        }

        if (request.ЦенаЗакупочная is < 0 || request.ЦенаПродажная is < 0)
        {
            ошибки.Add("Цены не могут быть отрицательными.");
        }

        if (request.Характеристики.Count > 0 && request.ВидНоменклатурыId is null)
        {
            ошибки.Add("Характеристики допустимы только при выбранном виде номенклатуры.");
        }

        var дубли = request.Характеристики
            .GroupBy(item => item.ХарактеристикаId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (дубли.Count > 0)
        {
            ошибки.Add(
                $"Характеристики указаны дважды: {string.Join(", ", дубли)}.");
        }

        if (дубли.Count == 0
            && ошибки.Count == 0
            && request.ВидНоменклатурыId is int видId)
        {
            ошибки.AddRange(await ValidateCharacteristicsAsync(видId, request));
        }

        if (ошибки.Count > 0)
        {
            return BadRequest(new { error = "Проверьте поля.", details = ошибки });
        }

        try
        {
            var id = await _номенклатура.CreateAsync(request);

            return Created(
                $"/api/nomenclature/{id}",
                new
                {
                    id,
                    код = request.Код.Trim(),
                    название = request.Название.Trim(),
                });
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                error = "Позиция с таким кодом уже существует.",
                код = request.Код.Trim(),
            });
        }
        catch (SqlException exception) when (exception.Number is 547)
        {
            _logger.LogWarning(exception, "Создание номенклатуры: нарушена ссылка");

            return BadRequest(new
            {
                error = "Связанный справочник не найден.",
                details = new[]
                {
                    "Проверьте единицу измерения, группу, категорию и вид номенклатуры.",
                },
            });
        }
        catch (SqlException exception) when (exception.Number is 2628 or 8152)
        {
            _logger.LogWarning(exception, "Создание номенклатуры: значение слишком длинное");

            return BadRequest(new
            {
                error = "Значение не помещается в поле базы данных.",
                details = new[] { "Сократите название, код или артикул." },
            });
        }
        catch (SqlException exception) when (exception.Number >= 50000)
        {
            _logger.LogWarning(
                exception, "Создание номенклатуры отклонено базой (код {Number})",
                exception.Number);

            return BadRequest(new
            {
                error = "База данных отклонила значения.",
                details = new[] { exception.Message },
            });
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Создание номенклатуры: ошибка SQL {Number}",
                exception.Number);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Ошибка базы данных при создании позиции." });
        }
    }

    private async Task<IEnumerable<string>> ValidateCharacteristicsAsync(
        int видId,
        СоздатьНоменклатуруRequest request)
    {
        var ошибки = new List<string>();
        var allowed = await _номенклатура.GetCharacteristicsAsync(видId);
        var provided = request.Характеристики
            .GroupBy(item => item.ХарактеристикаId)
            .ToDictionary(group => group.Key, group => group.First().Значение.Trim());

        foreach (var unknown in provided.Keys.Where(id => allowed.All(item => item.Id != id)))
        {
            ошибки.Add($"Характеристика {unknown} не принадлежит выбранному виду.");
        }

        foreach (var characteristic in allowed.Where(item => item.Обязательный))
        {
            if (!provided.TryGetValue(characteristic.Id, out var value)
                || string.IsNullOrWhiteSpace(value))
            {
                ошибки.Add($"Характеристика «{characteristic.Название}» обязательна.");
                continue;
            }

            var details = ValidateValue(characteristic, value);

            if (details is not null)
            {
                ошибки.Add(details);
            }
        }

        foreach (var (characteristicId, value) in provided)
        {
            var characteristic = allowed.FirstOrDefault(item => item.Id == characteristicId);

            if (characteristic is null || characteristic.Обязательный
                || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var details = ValidateValue(characteristic, value);

            if (details is not null)
            {
                ошибки.Add(details);
            }
        }

        return ошибки;
    }

    private static string? ValidateValue(ХарактеристикаВидаDto characteristic, string value)
    {
        switch (characteristic.ТипЗначения)
        {
            case "List":
                if (characteristic.СписокЗначений.Count > 0
                    && !characteristic.СписокЗначений.Contains(value))
                {
                    return $"«{characteristic.Название}»: значение «{value}» не из списка.";
                }

                return null;

            case "Number":
                if (!decimal.TryParse(
                        value,
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var number))
                {
                    return $"«{characteristic.Название}»: ожидается число.";
                }

                if ((characteristic.Минимум is decimal min && number < min)
                    || (characteristic.Максимум is decimal max && number > max))
                {
                    return $"«{characteristic.Название}»: значение вне диапазона.";
                }

                return null;

            case "Text":
                if (characteristic.МаксимальнаяДлина is int length && value.Length > length)
                {
                    return $"«{characteristic.Название}»: длиннее {length} символов.";
                }

                return null;

            default:
                return null;
        }
    }

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);

        return bytes;
    }

    private static float[] ToFloats(byte[]? vector)
    {
        if (vector is null)
        {
            return [];
        }

        var values = new float[vector.Length / sizeof(float)];
        Buffer.BlockCopy(vector, 0, values, 0, vector.Length);

        return values;
    }

    private static double Cosine(float[] left, float[] right)
    {
        if (left.Length == 0 || right.Length == 0 || left.Length != right.Length)
        {
            return 0;
        }

        double dot = 0;
        double normLeft = 0;
        double normRight = 0;

        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
            normLeft += left[index] * left[index];
            normRight += right[index] * right[index];
        }

        return normLeft == 0 || normRight == 0 ? 0 : dot / (Math.Sqrt(normLeft) * Math.Sqrt(normRight));
    }
}
