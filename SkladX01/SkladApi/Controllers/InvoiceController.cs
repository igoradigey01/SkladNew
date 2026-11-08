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
    private const int MatchThreshold = 50;
    private const int MaxTextLength = 4000;

    private static readonly string[] ItemsPropertyNames = ["items", "rows", "data", "result"];

    private static readonly JsonSerializerOptions ItemJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// JSON-схема ответа модели: без неё qwen2.5 отдавала один объект вместо массива.
    /// </summary>
    private static readonly object InvoiceSchema = new
    {
        type = "object",
        properties = new
        {
            items = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        raw_name = new { type = "string" },
                        quantity = new { type = "number" },
                        unit = new { type = "string" },
                    },
                    required = new[] { "raw_name", "quantity", "unit" },
                },
            },
        },
        required = new[] { "items" },
    };

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
        var payload = new
        {
            model = _ollama.Model,
            prompt = BuildPrompt(text),
            stream = false,
            format = InvoiceSchema,
            options = new { temperature = 0 },
        };

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

        return ParseItems(raw);
    }

    private static string BuildPrompt(string text)
    {
        return
            "Разбери текст накладной на все позиции. " +
            "raw_name — название товара как в тексте; quantity — количество " +
            "(если не указано, 1); unit — единица измерения как в тексте " +
            "(или пустая строка). Не придумывай товары, которых нет в тексте.\n\n" +
            "Текст: " + text;
    }

    private static List<ExtractedItemDto> ParseItems(string raw)
    {
        var json = StripFences(raw);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<ExtractedItemDto>>(root.GetRawText(), ItemJsonOptions)
                ?? [];
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("raw_name", out _))
        {
            var single = JsonSerializer.Deserialize<ExtractedItemDto>(
                root.GetRawText(), ItemJsonOptions);

            return single is null ? [] : [single];
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in ItemsPropertyNames)
            {
                if (root.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<ExtractedItemDto>>(
                        array.GetRawText(), ItemJsonOptions) ?? [];
                }
            }
        }

        throw new JsonException("Ожидался массив позиций.");
    }

    private static string StripFences(string raw)
    {
        var text = raw.Trim();

        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);

        return firstLineEnd > 0 && lastFence > firstLineEnd
            ? text[(firstLineEnd + 1)..lastFence].Trim()
            : text;
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
