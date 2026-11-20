using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/invoice")]
public class InvoiceController : ControllerBase
{
    private const int MatchThreshold = 75;
    private const int MaxTextLength = 4000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IНоменклатураRepository _номенклатура;
    private readonly OllamaSettings _ollama;
    private readonly ILogger<InvoiceController> _logger;

    public InvoiceController(
        IHttpClientFactory httpClientFactory,
        IНоменклатураRepository номенклатура,
        IOptions<OllamaSettings> ollama,
        ILogger<InvoiceController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _номенклатура = номенклатура;
        _ollama = ollama.Value;
        _logger = logger;
    }

    /// <summary>
    /// Разбирает продиктованный текст накладной в позиции и сопоставляет их с номенклатурой.
    /// </summary>
    [HttpPost("parse-voice")]
    public async Task<ActionResult<IReadOnlyList<ProcessedInvoiceRow>>> ParseVoice(
        [FromBody] VoiceParseRequest? request,
        CancellationToken cancellationToken)
    {
        var text = request?.RawText?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new { error = "Голосовой текст пуст." });
        }

        if (text.Length > MaxTextLength)
        {
            return BadRequest(new { error = $"Текст длиннее {MaxTextLength} символов." });
        }

        IReadOnlyList<НоменклатураDto> products;

        try
        {
            products = await _номенклатура.GetActiveAsync();
        }
        catch (SqlException exception)
        {
            _logger.LogError(exception, "Справочник номенклатуры недоступен");

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { error = "Справочник номенклатуры недоступен: проверьте SQL Server." });
        }

        if (products.Count == 0)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = "Справочник номенклатуры пуст или недоступен." });
        }

        List<ExtractedItemDto> items;

        try
        {
            items = await ParseWithModelAsync(text, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Ollama недоступна: {BaseUrl}", _ollama.BaseUrl);

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = "Сервис распознавания недоступен. Проверьте Ollama на ks34." });
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(
                StatusCodes.Status504GatewayTimeout,
                new { error = "Модель не ответила за отведённое время." });
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Модель вернула невалидный JSON");

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = "ИИ вернул невалидный формат JSON. Повторите запрос." });
        }

        var rows = items
            .Where(item => !string.IsNullOrWhiteSpace(item.RawName))
            .Select(item => Match(item, products))
            .ToList();

        return Ok(rows);
    }

    private async Task<List<ExtractedItemDto>> ParseWithModelAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("ollama");

        // gemma4 по умолчанию тратит время на блок рассуждений — для разбора он не нужен.
        var payload = new Dictionary<string, object?>
        {
            ["model"] = _ollama.Model,
            ["prompt"] = BuildPrompt(text),
            ["stream"] = false,
            ["format"] = InvoiceJsonParser.Schema,
            ["options"] = new { temperature = 0 },
        };

        if (_ollama.Model.StartsWith("gemma4", StringComparison.OrdinalIgnoreCase))
        {
            payload["think"] = false;
        }

        using var response = await client.PostAsJsonAsync(
            $"{_ollama.BaseUrl.TrimEnd('/')}/api/generate", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Ollama вернула {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var raw = result.TryGetProperty("response", out var responseText)
            ? responseText.GetString() ?? string.Empty
            : string.Empty;

        return InvoiceJsonParser.ParseItems(raw);
    }

    private static string BuildPrompt(string text)
    {
        return
            "Разбери текст накладной на позиции.\n" +
            "raw_name — название товара ВМЕСТЕ с размером и маркой (например «шайба цинк 10», " +
            "«гвозди 2,5х50», «цемент М-500»).\n" +
            "quantity — количество: число перед единицей измерения (штук, кг, мешков, м, л).\n" +
            "unit — единица измерения как в тексте, иначе пустая строка.\n" +
            "Слова «кг», «мешок», «штук», «метр» — это unit, в raw_name их не включай.\n" +
            "ВАЖНО: размеры и марки (10, 6, 3.5 на 25, М-500) — часть названия, а не количество.\n" +
            "Не придумывай товары, которых нет в тексте.\n\n" +
            "Пример 1.\nТекст: шайба цинк 10, 100 штук; гвозди 2,5х50 3 кг\n" +
            "Ответ: {\"items\":[{\"raw_name\":\"шайба цинк 10\",\"quantity\":100,\"unit\":\"штук\"}," +
            "{\"raw_name\":\"гвозди 2,5х50\",\"quantity\":3,\"unit\":\"кг\"}]}\n\n" +
            "Пример 2.\nТекст: саморезы 3.5 на 25, 200 штук\n" +
            "Ответ: {\"items\":[{\"raw_name\":\"саморезы 3.5 на 25\",\"quantity\":200,\"unit\":\"штук\"}]}\n\n" +
            "Текст: " + text;
    }

    private static ProcessedInvoiceRow Match(
        ExtractedItemDto item,
        IReadOnlyList<НоменклатураDto> products)
    {
        var row = new ProcessedInvoiceRow
        {
            VoiceName = item.RawName.Trim(),
            Quantity = item.Quantity > 0 ? item.Quantity : 1,
            Unit = string.IsNullOrWhiteSpace(item.Unit) ? null : item.Unit.Trim(),
            DbName = "НЕ НАЙДЕНО В БД",
        };

        var bestScore = 0;
        НоменклатураDto? bestProduct = null;

        foreach (var product in products)
        {
            if (FuzzyMatcher.NumbersConflict(item.RawName, product.Название))
            {
                continue;
            }

            var nameScore = FuzzyMatcher.Similarity(item.RawName, product.Название);
            var skuScore = FuzzyMatcher.Similarity(item.RawName, product.Артикул);
            var score = Math.Max(nameScore, skuScore);

            if (score > bestScore)
            {
                bestScore = score;
                bestProduct = product;
            }
        }

        if (bestProduct is null || bestScore < MatchThreshold)
        {
            return row;
        }

        row.ProductId = bestProduct.Id;
        row.ProductSku = bestProduct.Артикул ?? string.Empty;
        row.DbName = bestProduct.Название;
        row.MatchConfidence = bestScore;

        return row;
    }
}
