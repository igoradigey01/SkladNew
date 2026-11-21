using SkladApi.Dtos;

namespace SkladApi.Services;

/// <summary>Распознавание накладной по фото и текстовой подсказке приёмщика.</summary>
public interface IMultimodalRecognitionService
{
    /// <summary>
    /// Читает фото накладной (или только текст) и возвращает шапку документа и позиции.
    /// Текст приёмщика берётся как есть; если его нет, а аудио передано — расшифровывает Whisper.
    /// </summary>
    Task<ParsedDocumentDto> RecognizeInvoiceAsync(
        Stream? imageStream,
        Stream? audioStream,
        string? text,
        CancellationToken cancellationToken);
}
