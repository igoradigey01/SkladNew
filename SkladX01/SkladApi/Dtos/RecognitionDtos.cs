using System.Text.Json.Serialization;

namespace SkladApi.Dtos;

/// <summary>
/// Результат разбора фото накладной с голосом приёмщика.
/// Схема и имена полей — из <c>_hubx01/prompts/ JSON Schema.md</c> (snake_case, как у модели).
/// </summary>
public sealed class ParsedDocumentDto
{
    [JsonPropertyName("document_number")]
    public string? DocumentNumber { get; set; }

    [JsonPropertyName("document_date")]
    public string? DocumentDate { get; set; }

    [JsonPropertyName("supplier_name")]
    public string? SupplierName { get; set; }

    [JsonPropertyName("general_comment")]
    public string? GeneralComment { get; set; }

    [JsonPropertyName("items")]
    public List<ParsedItemDto> Items { get; set; } = [];
}

/// <summary>Позиция накладной: количество по документу и фактически принятое.</summary>
public sealed class ParsedItemDto
{
    [JsonPropertyName("line_number")]
    public int? LineNumber { get; set; }

    [JsonPropertyName("article")]
    public string? Article { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("price")]
    public double? Price { get; set; }

    /// <summary>Количество по документу.</summary>
    [JsonPropertyName("qty_doc")]
    public double QtyDoc { get; set; }

    /// <summary>Фактически принятое количество (из голоса приёмщика).</summary>
    [JsonPropertyName("qty_fact")]
    public double QtyFact { get; set; }

    /// <summary>Товар, которого нет в накладной, но он назван в голосе.</summary>
    [JsonPropertyName("is_extra")]
    public bool IsExtra { get; set; }

    /// <summary>Расхождение или комментарий приёмщика по этой позиции.</summary>
    [JsonPropertyName("item_comment")]
    public string? ItemComment { get; set; }
}
