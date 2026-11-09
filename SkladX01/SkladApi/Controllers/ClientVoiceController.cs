using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkladApi.Dtos;
using SkladApi.Repositories;
using SkladApi.Services;

namespace SkladApi.Controllers;

[ApiController]
[Route("api/clients/voice-search")]
public class ClientVoiceController : ControllerBase
{
    private const int MinScore = 50;
    private const int MaxTextLength = 500;

    private static readonly JsonSerializerOptions ExtractOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly object ExtractSchema = new
    {
        type = "object",
        properties = new
        {
            name = new { type = "string" },
            city = new { type = "string" },
            phone = new { type = "string" },
        },
        required = new[] { "name", "city", "phone" },
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IКлиентRepository _clients;
    private readonly OllamaSettings _ollama;
    private readonly ILogger<ClientVoiceController> _logger;

    public ClientVoiceController(
        IHttpClientFactory httpClientFactory,
        IКлиентRepository clients,
        IOptions<OllamaSettings> ollama,
        ILogger<ClientVoiceController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _clients = clients;
        _ollama = ollama.Value;
        _logger = logger;
    }

    /// <summary>Поиск клиента по продиктованному тексту.</summary>
    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<ClientCandidate>>> Search(
        [FromBody] ClientVoiceQuery? query,
        CancellationToken cancellationToken)
    {
        var text = query?.RawText?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new { error = "Голосовой текст пуст." });
        }

        if (text.Length > MaxTextLength)
        {
            return BadRequest(new { error = $"Текст длиннее {MaxTextLength} символов." });
        }

        ExtractedClientQuery extracted;

        try
        {
            extracted = await ExtractAsync(text, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Ollama может быть недоступна — поиск по самому тексту всё равно работает.
            _logger.LogWarning(exception, "Модель недоступна, ищем по исходному тексту");
            extracted = new ExtractedClientQuery { Name = text };
        }

        // Если модель ничего не выделила — ищем по всему тексту.
        if (string.IsNullOrWhiteSpace(extracted.Name) &&
            string.IsNullOrWhiteSpace(extracted.City) &&
            string.IsNullOrWhiteSpace(extracted.Phone))
        {
            extracted.Name = text;
        }

        if (Digits(extracted.Phone).Length < 6)
        {
            var digits = Digits(text);
            extracted.Phone = digits.Length >= 6 ? digits : DigitsFromWords(text) ?? extracted.Phone;
        }

        var clients = await _clients.GetAllAsync(
            null, null, null, null, "alias", false, activeOnly: true);

        var candidates = clients
            .Select(client => Score(client, extracted))
            .Where(candidate => candidate.Score >= MinScore)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Alias)
            .Take(10)
            .ToList();

        return Ok(candidates);
    }

    private async Task<ExtractedClientQuery> ExtractAsync(string text, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("ollama");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _ollama.Model,
            ["prompt"] =
                "Из текста выдели имя клиента, город и телефон. " +
                "Город — любой населённый пункт, даже в падеже: «из Родников» → «Родники». " +
                "Телефон — только цифры: «восемь девять один…» → «891…». " +
                "Если чего-то нет — верни пустую строку. Не придумывай данные.\n\n" +
                "Текст: " + text,
            ["stream"] = false,
            ["format"] = ExtractSchema,
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
        var raw = result.TryGetProperty("response", out var value)
            ? value.GetString() ?? "{}"
            : "{}";

        try
        {
            return JsonSerializer.Deserialize<ExtractedClientQuery>(raw, ExtractOptions)
                ?? new ExtractedClientQuery();
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Модель вернула невалидный JSON для поиска клиента");
            throw;
        }
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "найди", "найти", "покажи", "клиент", "клиента", "клиенту", "мне", "нужен",
        "нужно", "для", "это", "город", "городе", "из", "по", "про", "его", "она",
    };

    private static ClientCandidate Score(КлиентDto client, ExtractedClientQuery query)
    {
        var candidate = new ClientCandidate
        {
            Id = client.Id,
            Alias = client.Псевдоним,
            FullName = client.Фио,
            Phone = client.Телефон,
            City = client.Город,
        };

        double best = 0;
        var field = string.Empty;

        void Consider(double score, string name)
        {
            if (score > best)
            {
                best = score;
                field = name;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            Consider(FuzzyMatcher.Similarity(query.Name, client.Псевдоним), "alias");
            Consider(FuzzyMatcher.Similarity(query.Name, client.Фио) * 0.95, "name");

            foreach (var token in Tokens(query.Name))
            {
                Consider(BestTokenScore(token, client.Псевдоним), "alias");
                Consider(BestTokenScore(token, client.Фио) * 0.95, "name");
                Consider(BestTokenScore(token, client.Город) * 0.75, "city");
            }
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            Consider(
                SameStem(query.City, client.Город)
                    ? 95
                    : FuzzyMatcher.Similarity(query.City, client.Город) * 0.9,
                "city");
        }

        var phone = Digits(query.Phone);

        if (phone.Length < 6)
        {
            // «восемь девять один…» — модель часто не переводит слова в цифры.
            phone = DigitsFromWords(query.Phone) ?? phone;
        }

        if (phone.Length >= 4 && Digits(client.Телефон).Contains(phone, StringComparison.Ordinal))
        {
            Consider(100, "phone");
        }

        candidate.Score = (int)Math.Round(best);
        candidate.MatchedField = field;

        return candidate;
    }

    private static IEnumerable<string> Tokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var token in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = new string(token.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

            if (clean.Length >= 4 && !StopWords.Contains(clean))
            {
                yield return clean;
            }
        }
    }

    private static int BestTokenScore(string token, string? value)
    {
        var best = 0;

        foreach (var candidate in Tokens(value))
        {
            var score = FuzzyMatcher.Similarity(token, candidate);

            if (score > best)
            {
                best = score;
            }
        }

        return best;
    }

    private static readonly Dictionary<string, char> DigitWords = new(StringComparer.Ordinal)
    {
        ["ноль"] = '0', ["нуль"] = '0', ["один"] = '1', ["одна"] = '1', ["два"] = '2', ["две"] = '2',
        ["три"] = '3', ["четыре"] = '4', ["пять"] = '5', ["шесть"] = '6', ["семь"] = '7',
        ["восемь"] = '8', ["девять"] = '9',
    };

    private static string? DigitsFromWords(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new System.Text.StringBuilder();

        foreach (var token in value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = new string(token.Where(char.IsLetterOrDigit).ToArray());

            if (DigitWords.TryGetValue(clean, out var digit))
            {
                digits.Append(digit);
            }
            else if (clean.Length > 0 && clean.All(char.IsDigit))
            {
                digits.Append(clean);
            }
        }

        return digits.Length >= 6 ? digits.ToString() : null;
    }

    private static bool SameStem(string? left, string? right)
    {
        var a = new string((left ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        var b = new string((right ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        if (a.Length < 5 || b.Length < 5)
        {
            return false;
        }

        return a.AsSpan(0, 5).SequenceEqual(b.AsSpan(0, 5));
    }

    private static string Digits(string? value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
