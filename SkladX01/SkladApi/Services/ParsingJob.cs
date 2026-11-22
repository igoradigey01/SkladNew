namespace SkladApi.Services;

/// <summary>Задача асинхронного разбора накладной, поставленная в очередь.</summary>
/// <param name="Id">Идентификатор задачи (возвращается клиенту).</param>
/// <param name="Image">Фото накладной (или null, если разбор только по тексту).</param>
/// <param name="Text">Текст приёмщика.</param>
/// <param name="ConnectionId">SignalR-соединение для пуша результата (может быть null).</param>
public sealed record ParsingJob(
    Guid Id,
    byte[]? Image,
    string Text,
    string? ConnectionId);
