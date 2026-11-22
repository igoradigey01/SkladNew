namespace SkladApi.Services;

/// <summary>
/// Настройки облачного провайдера DeepSeek Flash.
/// Модель <c>deepseek-flash</c> (display name DeepSeek-V4.1-Flash) умеет vision:
/// принимает текст и изображение и возвращает JSON.
/// </summary>
public sealed class DeepSeekSettings
{
    public const string SectionName = "DeepSeek";

    /// <summary>Базовый адрес OpenAI-совместимого API.</summary>
    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    /// <summary>Идентификатор модели из <c>GET /models</c>.</summary>
    public string Model { get; set; } = "deepseek-flash";

    /// <summary>Ключ API. Задаётся через user-secrets / переменные окружения.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Таймаут запроса генерации, секунды.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Максимум токенов ответа (JSON-документ может быть большим).</summary>
    public int MaxTokens { get; set; } = 8192;

    /// <summary>Включает облачный fallback (по умолчанию включён).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Провайдер можно использовать только при наличии ключа.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
