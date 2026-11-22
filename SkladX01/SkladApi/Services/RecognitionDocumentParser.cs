using System.Text.Json;
using SkladApi.Dtos;

namespace SkladApi.Services;

/// <summary>Общий разбор JSON-ответа модели в <see cref="ParsedDocumentDto"/>.</summary>
internal static class RecognitionDocumentParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ParsedDocumentDto Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new JsonException("Модель вернула пустой ответ.");
        }

        var document = JsonSerializer.Deserialize<ParsedDocumentDto>(content, Options)
            ?? throw new JsonException("Модель вернула пустой документ.");

        document.Items ??= [];

        return document;
    }
}
