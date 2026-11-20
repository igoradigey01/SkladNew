using Microsoft.Extensions.Options;
using Whisper.net;

namespace SkladApi.Services;

/// <summary>Локальное распознавание речи (Whisper.net).</summary>
public interface IWhisperTranscriber
{
    /// <summary>Модель Whisper найдена на диске.</summary>
    bool IsAvailable { get; }

    /// <summary>Итоговый путь к модели — для диагностики.</summary>
    string ModelPath { get; }

    /// <summary>Расшифровывает WAV 16 кГц, моно, 16 бит в текст.</summary>
    Task<string> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken);
}

/// <summary>
/// Модель (десятки-сотни мегабайт) грузится один раз на процесс: раньше
/// <see cref="WhisperFactory"/> создавался на каждый запрос, то есть файл модели
/// читался целиком при каждом распознавании.
/// </summary>
public sealed class WhisperTranscriber : IWhisperTranscriber, IDisposable
{
    private readonly WhisperSettings _settings;
    private readonly ILogger<WhisperTranscriber> _logger;
    private readonly Lazy<WhisperFactory> _factory;

    public WhisperTranscriber(
        IOptions<WhisperSettings> options,
        ILogger<WhisperTranscriber> logger)
    {
        _settings = options.Value;
        _logger = logger;
        _factory = new Lazy<WhisperFactory>(
            () => WhisperFactory.FromPath(ModelPath),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public string ModelPath => ResolveModelPath();

    public bool IsAvailable => File.Exists(ModelPath);

    public async Task<string> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            throw new WhisperUnavailableException(UnavailableMessage());
        }

        var tempFile = Path.Combine(
            Path.GetTempPath(), $"sklad-whisper-{Guid.NewGuid():N}.wav");

        try
        {
            await using (var file = File.Create(tempFile))
            {
                await audioStream.CopyToAsync(file, cancellationToken);
            }

            await using var stream = File.OpenRead(tempFile);

            // Процессор создаётся на запрос: он хранит состояние потока, но модель уже в памяти.
            using var processor = _factory.Value.CreateBuilder()
                .WithLanguage("ru")
                .Build();

            var text = new System.Text.StringBuilder();

            await foreach (var segment in processor.ProcessAsync(stream))
            {
                cancellationToken.ThrowIfCancellationRequested();
                text.Append(segment.Text);
            }

            var result = text.ToString().Trim();
            _logger.LogInformation("Whisper: {Length} символов распознано", result.Length);

            return result;
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                try
                {
                    File.Delete(tempFile);
                }
                catch (IOException exception)
                {
                    _logger.LogWarning(exception, "Не удалось удалить временный файл {File}", tempFile);
                }
            }
        }
    }

    private string ResolveModelPath()
    {
        var path = _settings.ModelPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);
    }

    private string UnavailableMessage()
    {
        if (string.IsNullOrWhiteSpace(_settings.ModelPath))
        {
            return "Модель Whisper не настроена: задайте Whisper:ModelPath в appsettings.json.";
        }

        return
            $"Модель Whisper не найдена: {ModelPath}. " +
            "Скачайте ggml-base.bin и положите файл по этому пути " +
            "(например: curl -L -o Models/ggml-base.bin " +
            "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin).";
    }

    public void Dispose()
    {
        if (_factory.IsValueCreated)
        {
            _factory.Value.Dispose();
        }
    }
}
