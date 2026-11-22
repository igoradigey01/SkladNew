using SkladApi.Dtos;

namespace SkladApi.Services;

/// <summary>
/// Провайдер разбора накладной: фото (vision) и/или текст приёмщика → документ.
/// Реализации: локальный Ollama и облачный DeepSeek Flash.
/// </summary>
public interface IMultimodalAiProvider
{
    /// <summary>Готов ли провайдер принимать запросы (например, задан ли API-ключ).</summary>
    bool IsConfigured { get; }

    /// <summary>Разбирает фото и/или текст, возвращает шапку документа и позиции.</summary>
    Task<ParsedDocumentDto> ParseAsync(
        byte[]? imageBytes,
        string text,
        CancellationToken cancellationToken);
}

/// <summary>Маркер локального провайдера — удобно различать регистрации в DI.</summary>
public interface ILocalAiProvider : IMultimodalAiProvider;

/// <summary>Маркер облачного провайдера.</summary>
public interface ICloudAiProvider : IMultimodalAiProvider;
