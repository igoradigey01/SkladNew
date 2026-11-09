namespace SkladApi.Services;

public sealed class OllamaSettings
{
    public const string SectionName = "Ollama";

    /// <summary>Базовый адрес Ollama, например http://10.42.0.100:11434.</summary>
    public string BaseUrl { get; set; } = "http://10.42.0.100:11434";

    /// <summary>Модель для разбора накладной. gemma4:e4b — быстрая, qwen2.5:14b-instruct — запасная.</summary>
    public string Model { get; set; } = "gemma4:e4b";

    /// <summary>Модель эмбеддингов для семантического поиска.</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>Таймаут запроса к модели, секунды.</summary>
    public int TimeoutSeconds { get; set; } = 90;
}
