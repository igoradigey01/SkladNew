using Microsoft.AspNetCore.Mvc;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/nomenclature")]
public class NomenclatureController : ControllerBase
{
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
