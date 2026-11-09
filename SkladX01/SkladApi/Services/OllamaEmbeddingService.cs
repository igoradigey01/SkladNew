using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SkladApi.Services;

/// <summary>Получение эмбеддингов из Ollama (модель nomic-embed-text).</summary>
public sealed class OllamaEmbeddingService
{
    private readonly HttpClient _http;
    private readonly OllamaSettings _settings;

    public OllamaEmbeddingService(HttpClient http, IOptions<OllamaSettings> settings)
    {
        _http = http;
        _settings = settings.Value;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await GetEmbeddingsAsync([text], cancellationToken);

        return embeddings[0];
    }

    public async Task<float[][]> GetEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = _settings.EmbeddingModel,
            input = texts,
        };

        using var response = await _http.PostAsJsonAsync(
            $"{_settings.BaseUrl.TrimEnd('/')}/api/embed", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Ollama embeddings {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        if (!result.TryGetProperty("embeddings", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Ollama не вернула embeddings.");
        }

        var vectors = new List<float[]>(texts.Count);

        foreach (var vector in array.EnumerateArray())
        {
            var values = vector.EnumerateArray().Select(value => value.GetSingle()).ToArray();
            vectors.Add(values);
        }

        return vectors.ToArray();
    }
}
