using System.Text.Json.Serialization;

namespace SkladApi.Dtos;

/// <summary>Текст, распознанный с клавиатуры смартфона.</summary>
public class VoiceParseRequest
{
    public string RawText { get; set; } = string.Empty;
}

/// <summary>Позиция, которую вернула модель. Ключи — как в промте.</summary>
public class ExtractedItemDto
{
    [JsonPropertyName("raw_name")]
    public string RawName { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public double Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}

/// <summary>Позиция после сопоставления с номенклатурой.</summary>
public class ProcessedInvoiceRow
{
    public int? ProductId { get; set; }
    public string ProductSku { get; set; } = string.Empty;
    public string DbName { get; set; } = string.Empty;
    public string VoiceName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? Unit { get; set; }
    public int MatchConfidence { get; set; }
}
