namespace SkladApi.Services;

/// <summary>Настройки локального распознавания речи.</summary>
public sealed class WhisperSettings
{
    public const string SectionName = "Whisper";

    /// <summary>
    /// Путь к модели whisper.cpp. Относительный путь считается от каталога приложения,
    /// поэтому файл должен лежать рядом с бинарником (папка Models копируется при сборке).
    /// </summary>
    public string ModelPath { get; set; } = "Models/ggml-base.bin";

    /// <summary>
    /// true — приложение не стартует без модели. По умолчанию false: сервис поднимается,
    /// а /api/recognition/parse отвечает 503 с понятным текстом.
    /// </summary>
    public bool Required { get; set; }
}
