namespace SkladApi.Services;

public sealed class OllamaSettings
{
    public const string SectionName = "Ollama";

    /// <summary>Базовый адрес Ollama, например http://10.42.0.100:11434.</summary>
    public string BaseUrl { get; set; } = "http://10.42.0.100:11434";

    /// <summary>Модель для разбора накладной. На ks34 установлена qwen2.5:14b-instruct.</summary>
    public string Model { get; set; } = "qwen2.5:14b-instruct";

    /// <summary>Таймаут запроса к модели, секунды.</summary>
    public int TimeoutSeconds { get; set; } = 90;
}
