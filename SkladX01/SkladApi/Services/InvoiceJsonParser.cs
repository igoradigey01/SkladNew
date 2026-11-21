using System.Text.Json;
using SkladApi.Dtos;

namespace SkladApi.Services;

/// <summary>
/// Разбор ответа модели на позиции накладной.
/// Схема ответа задаётся через <c>format</c>, но модели всё равно возвращают то объект
/// <c>{"items":[…]}</c>, то массив, то одну позицию — поддержаны все варианты.
/// </summary>
public static class InvoiceJsonParser
{
    private static readonly string[] ItemsPropertyNames = ["items", "rows", "data", "result"];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>JSON-схема ответа модели: без неё модель отдаёт один объект вместо массива.</summary>
    public static object Schema { get; } = new
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

    /// <summary>
    /// Разбирает ответ модели. Бросает <see cref="JsonException"/>, если позиций в ответе нет.
    /// </summary>
    public static List<ExtractedItemDto> ParseItems(string raw)
    {
        var json = StripFences(raw);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<ExtractedItemDto>>(root.GetRawText(), Options)
                ?? [];
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("raw_name", out _))
        {
            var single = JsonSerializer.Deserialize<ExtractedItemDto>(root.GetRawText(), Options);

            return single is null ? [] : [single];
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in ItemsPropertyNames)
            {
                if (root.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<ExtractedItemDto>>(
                        array.GetRawText(), Options) ?? [];
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
}
