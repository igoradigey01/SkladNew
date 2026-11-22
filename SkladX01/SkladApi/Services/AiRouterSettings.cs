namespace SkladApi.Services;

/// <summary>
/// Настройки умного роутера «локально → облако».
/// Интерактивный режим (Lazy Check): зонд только перед запросом, если статус несвежий.
/// </summary>
public sealed class AiRouterSettings
{
    public const string SectionName = "AiRouter";

    /// <summary>Число одновременных задач на локальном GPU (Whisper + Ollama).</summary>
    public int LocalGpuSlots { get; set; } = 1;

    /// <summary>Сколько секунд считать успешный зонд свежим (TTL).</summary>
    public int ProbeFreshnessSeconds { get; set; } = 15;
}
