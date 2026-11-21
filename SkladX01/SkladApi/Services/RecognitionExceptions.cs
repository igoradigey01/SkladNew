namespace SkladApi.Services;

/// <summary>
/// Исключения распознавания. Отдельные типы нужны, чтобы контроллер отдавал
/// понятный код ответа вместо общего 500.
/// </summary>
public sealed class WhisperUnavailableException(string message) : Exception(message);

/// <summary>Аудио не в том формате, который понимает Whisper.</summary>
public sealed class UnsupportedAudioFormatException(string message) : Exception(message);

/// <summary>Файл не является изображением, которое можно прочитать.</summary>
public sealed class UnsupportedImageFormatException(string message) : Exception(message);
