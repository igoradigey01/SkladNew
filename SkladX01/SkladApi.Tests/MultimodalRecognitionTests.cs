using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkladApi.Controllers;
using SkladApi.Dtos;
using SkladApi.Services;
using Xunit;

namespace SkladApi.Tests;

public class MultimodalRecognitionTests
{
    private const string Voice = "шайба цинк 10 — сто штук, цемента два мешка, ещё привезли пленку 5 рулонов";

    // ---------- WavFormat ----------

    [Fact]
    public void Accepts_16k_mono_pcm()
    {
        var wav = WavFormat.BuildSilence(16000);

        Assert.True(WavFormat.TryValidate(wav, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Rejects_webm_from_browser_recorder()
    {
        // MediaRecorder отдаёт WebM: 0x1A 0x45 0xDF 0xA3
        byte[] webm = [0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

        Assert.False(WavFormat.TryValidate(webm, out var error));
        Assert.Contains("WAV", error);
    }

    [Fact]
    public void Rejects_stereo_audio()
    {
        var wav = BuildWav(channels: 2, sampleRate: 16000, bitsPerSample: 16);

        Assert.False(WavFormat.TryValidate(wav, out var error));
        Assert.Contains("моно", error);
    }

    [Fact]
    public void Rejects_44khz_audio()
    {
        var wav = BuildWav(channels: 1, sampleRate: 44100, bitsPerSample: 16);

        Assert.False(WavFormat.TryValidate(wav, out var error));
        Assert.Contains("16000", error);
    }

    // ---------- Сервис: разбор ответа модели ----------

    [Fact]
    public async Task Parses_document_header_and_items()
    {
        var content = """
            {
              "document_number": "1042",
              "document_date": "19.11.2026",
              "supplier_name": "ООО \"СтройБаза\"",
              "general_comment": null,
              "items": [
                { "line_number": 1, "name": "Шайба цинк 10", "unit": "шт",
                  "price": 12.5, "qty_doc": 10, "qty_fact": 100, "is_extra": false,
                  "item_comment": "приняли сто штук" }
              ]
            }
            """;

        var service = CreateService(ChatResponse(content), out _, out _);

        var document = await service.RecognizeInvoiceAsync(Image(), Audio(), null, CancellationToken.None);

        Assert.Equal("1042", document.DocumentNumber);
        Assert.Equal("19.11.2026", document.DocumentDate);
        Assert.Equal("ООО \"СтройБаза\"", document.SupplierName);

        var item = Assert.Single(document.Items);
        Assert.Equal("Шайба цинк 10", item.Name);
        Assert.Equal(10, item.QtyDoc);
        Assert.Equal(100, item.QtyFact);
        Assert.False(item.IsExtra);
        Assert.Equal(12.5, item.Price);
        Assert.Equal(1, item.LineNumber);
        Assert.Equal("приняли сто штук", item.ItemComment);
    }

    [Fact]
    public async Task Parses_extra_item_from_voice()
    {
        var content = """
            {
              "document_number": null, "document_date": null, "supplier_name": null,
              "items": [
                { "name": "пленка", "unit": "рулон", "qty_doc": 0, "qty_fact": 5, "is_extra": true }
              ]
            }
            """;

        var service = CreateService(ChatResponse(content), out _, out _);

        var document = await service.RecognizeInvoiceAsync(Image(), Audio(), null, CancellationToken.None);

        var item = Assert.Single(document.Items);
        Assert.True(item.IsExtra);
        Assert.Equal(0, item.QtyDoc);
        Assert.Equal(5, item.QtyFact);
        Assert.Null(document.DocumentNumber);
    }

    [Fact]
    public async Task Sends_schema_system_prompt_and_voice_context()
    {
        var service = CreateService(ChatResponse(EmptyDocument()), out var request, out var transcriber);

        await service.RecognizeInvoiceAsync(Image(), Audio(), null, CancellationToken.None);

        Assert.Equal(1, transcriber.Calls);

        using var body = JsonDocument.Parse(request.Body!);
        var root = body.RootElement;

        Assert.Equal("gemma4:e4b", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("think").GetBoolean());

        var format = root.GetProperty("format");
        Assert.Equal("object", format.GetProperty("type").GetString());
        Assert.Contains(
            "document_number",
            format.GetProperty("required").EnumerateArray().Select(item => item.GetString()!));

        var messages = root.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("точный парсер", messages[0].GetProperty("content").GetString());

        var user = messages[1];
        Assert.Contains(Voice, user.GetProperty("content").GetString());
        Assert.Single(user.GetProperty("images").EnumerateArray());
    }

    [Fact]
    public async Task Sends_no_comments_marker_when_transcription_is_empty()
    {
        var service = CreateService(
            ChatResponse(EmptyDocument()), out var request, out _, transcription: string.Empty);

        await service.RecognizeInvoiceAsync(Image(), Audio(), null, CancellationToken.None);

        using var body = JsonDocument.Parse(request.Body!);
        var prompt = body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();

        Assert.Contains("без расхождений", prompt);
    }

    [Fact]
    public async Task Uses_text_instead_of_whisper()
    {
        var service = CreateService(
            ChatResponse(EmptyDocument()), out var request, out var transcriber);

        await service.RecognizeInvoiceAsync(
            Image(), Audio(), "  шайба — сто штук, пленки пять  ", CancellationToken.None);

        Assert.Equal(0, transcriber.Calls);

        using var body = JsonDocument.Parse(request.Body!);
        var prompt = body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();

        Assert.Contains("шайба — сто штук, пленки пять", prompt);
        Assert.DoesNotContain(Voice, prompt);
    }

    [Fact]
    public async Task Parses_photo_without_text_or_audio()
    {
        var service = CreateService(
            ChatResponse(EmptyDocument()), out var request, out var transcriber);

        await service.RecognizeInvoiceAsync(Image(), null, null, CancellationToken.None);

        Assert.Equal(0, transcriber.Calls);

        using var body = JsonDocument.Parse(request.Body!);
        var prompt = body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();

        Assert.Contains("без расхождений", prompt);
    }

    [Fact]
    public async Task Throws_on_invalid_json_from_model()
    {
        var service = CreateService(ChatResponse("не JSON"), out _, out _);

        await Assert.ThrowsAnyAsync<JsonException>(
            () => service.RecognizeInvoiceAsync(Image(), Audio(), null, CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_model_returns_empty_content()
    {
        var service = CreateService(ChatResponse(string.Empty), out _, out _);

        await Assert.ThrowsAnyAsync<JsonException>(
            () => service.RecognizeInvoiceAsync(Image(), Audio(), null, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_non_wav_audio()
    {
        var service = CreateService(ChatResponse(EmptyDocument()), out _, out var transcriber);

        var audio = new MemoryStream([0x1A, 0x45, 0xDF, 0xA3]);

        await Assert.ThrowsAsync<UnsupportedAudioFormatException>(
            () => service.RecognizeInvoiceAsync(Image(), audio, null, CancellationToken.None));
        Assert.Equal(0, transcriber.Calls);
    }

    [Fact]
    public async Task Rejects_not_an_image()
    {
        var service = CreateService(ChatResponse(EmptyDocument()), out _, out _);

        var fake = new MemoryStream(Encoding.UTF8.GetBytes("это не картинка"));

        await Assert.ThrowsAsync<UnsupportedImageFormatException>(
            () => service.RecognizeInvoiceAsync(fake, Audio(), null, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_heic_image_with_hint()
    {
        var service = CreateService(ChatResponse(EmptyDocument()), out _, out _);

        // ftyp + brand heic — iOS отдаёт такое фото, модель его не читает.
        byte[] heic =
        [
            0x00, 0x00, 0x00, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p',
            (byte)'h', (byte)'e', (byte)'i', (byte)'c', 0x00, 0x00, 0x00, 0x00,
        ];

        var exception = await Assert.ThrowsAsync<UnsupportedImageFormatException>(
            () => service.RecognizeInvoiceAsync(new MemoryStream(heic), Audio(), null, CancellationToken.None));

        Assert.Contains("HEIC", exception.Message);
    }

    // ---------- Контроллер ----------

    [Fact]
    public async Task Controller_requires_image()
    {
        var controller = CreateController(StubService(new ParsedDocumentDto()));

        var result = await controller.ParseInvoice(null, File("audio", "a.wav", "audio/wav"), null, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_allows_photo_without_audio_or_text()
    {
        var controller = CreateController(StubService(new ParsedDocumentDto()));

        var result = await controller.ParseInvoice(
            File("image", "i.png", "image/png"), null, null, default);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_passes_text_without_audio()
    {
        var service = new CapturingService();
        var controller = CreateController(service);

        var result = await controller.ParseInvoice(
            File("image", "i.png", "image/png"), null, "  шайба сто штук  ", default);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("шайба сто штук", service.Text);
        Assert.Null(service.Audio);
    }

    [Fact]
    public async Task Controller_rejects_too_long_text()
    {
        var controller = CreateController(StubService(new ParsedDocumentDto()));

        var result = await controller.ParseInvoice(
            File("image", "i.png", "image/png"), null, new string('я', 4001), default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_rejects_wrong_content_type()
    {
        var controller = CreateController(StubService(new ParsedDocumentDto()));

        var result = await controller.ParseInvoice(
            File("image", "i.pdf", "application/pdf"),
            File("audio", "a.wav", "audio/wav"),
            null,
            default);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, objectResult.StatusCode);
    }

    [Fact]
    public async Task Controller_returns_413_for_large_image()
    {
        var controller = CreateController(StubService(new ParsedDocumentDto()));
        var big = File("image", "i.png", "image/png", size: 16 * 1024 * 1024);

        var result = await controller.ParseInvoice(big, File("audio", "a.wav", "audio/wav"), null, default);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);
    }

    [Fact]
    public async Task Controller_returns_415_for_unsupported_audio()
    {
        var controller = CreateController(
            new ThrowingService(new UnsupportedAudioFormatException("нужен WAV")));

        var result = await controller.ParseInvoice(
            File("image", "i.png", "image/png"),
            File("audio", "a.webm", "audio/webm"),
            null,
            default);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, objectResult.StatusCode);
    }

    [Fact]
    public async Task Controller_returns_503_when_whisper_missing()
    {
        var controller = CreateController(
            new ThrowingService(new WhisperUnavailableException("модели нет")));

        var result = await controller.ParseInvoice(
            File("image", "i.png", "image/png"),
            File("audio", "a.wav", "audio/wav"),
            null,
            default);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public async Task Controller_returns_502_when_ollama_fails()
    {
        var controller = CreateController(new ThrowingService(new HttpRequestException("нет связи")));

        var result = await controller.ParseInvoice(
            File("image", "i.png", "image/png"),
            File("audio", "a.wav", "audio/wav"),
            null,
            default);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
    }

    [Fact]
    public async Task Controller_returns_document()
    {
        var document = new ParsedDocumentDto
        {
            DocumentNumber = "1042",
            SupplierName = "ООО \"СтройБаза\"",
            Items =
            [
                new ParsedItemDto
                {
                    Name = "Шайба цинк 10",
                    Unit = "шт",
                    QtyDoc = 10,
                    QtyFact = 8,
                    IsExtra = false,
                    ItemComment = "две штуки брак",
                },
            ],
        };

        var controller = CreateController(StubService(document));

        var result = await controller.ParseInvoice(
            File("image", "i.png", "image/png"),
            File("audio", "a.wav", "audio/wav"),
            null,
            default);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var parsed = Assert.IsType<ParsedDocumentDto>(ok.Value);
        Assert.Equal("1042", parsed.DocumentNumber);

        var item = Assert.Single(parsed.Items);
        Assert.Equal(8, item.QtyFact);
        Assert.Equal("две штуки брак", item.ItemComment);
    }

    // ---------- Инфраструктура тестов ----------

    private static MultimodalRecognitionService CreateService(
        string responseBody,
        out CapturingHandler request,
        out FakeTranscriber transcriber,
        string transcription = Voice)
    {
        request = new CapturingHandler(responseBody);
        transcriber = new FakeTranscriber(transcription);

        var ollamaClient = new HttpClient(request)
        {
            BaseAddress = new Uri("http://10.42.0.100:11434"),
        };

        var local = new OllamaMultimodalProvider(
            Options.Create(new OllamaSettings { Model = "gemma4:e4b" }),
            ollamaClient,
            NullLogger<OllamaMultimodalProvider>.Instance);

        // Облако не настроено (нет ключа) — тесты проверяют локальную ветку.
        var cloud = new DeepSeekMultimodalProvider(
            Options.Create(new DeepSeekSettings { ApiKey = string.Empty }),
            new HttpClient(new CapturingHandler(EmptyDocument()))
            {
                BaseAddress = new Uri("https://api.deepseek.com"),
            },
            NullLogger<DeepSeekMultimodalProvider>.Instance);

        var routerSettings = Options.Create(new AiRouterSettings());

        var router = new SmartAiRouter(
            local,
            cloud,
            new LocalNodeStateTracker(),
            new AlwaysAliveProbe(),
            new GpuSlotLimiter(routerSettings),
            routerSettings,
            NullLogger<SmartAiRouter>.Instance);

        return new MultimodalRecognitionService(
            transcriber,
            router,
            NullLogger<MultimodalRecognitionService>.Instance);
    }

    private static RecognitionController CreateController(IMultimodalRecognitionService service)
    {
        return new RecognitionController(
            service,
            NullLogger<RecognitionController>.Instance);
    }

    private static IMultimodalRecognitionService StubService(ParsedDocumentDto document)
    {
        return new StubRecognitionService(document);
    }

    // Минимальный PNG 1×1 — сигнатуры достаточно, декодирование сервису не нужно.
    private static MemoryStream Image()
    {
        return new MemoryStream(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg=="));
    }

    private static MemoryStream Audio()
    {
        return new MemoryStream(WavFormat.BuildSilence(16000));
    }

    private static FormFile File(string name, string fileName, string contentType, long size = -1)
    {
        var stream = new MemoryStream(size > 0 ? new byte[size] : [1, 2, 3, 4]);

        return new FormFile(stream, 0, stream.Length, name, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static string EmptyDocument()
    {
        return """
            { "document_number": null, "document_date": null, "supplier_name": null, "items": [] }
            """;
    }

    private static string ChatResponse(string content)
    {
        return JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content },
            done = true,
        });
    }

    private static byte[] BuildWav(short channels, int sampleRate, short bitsPerSample)
    {
        const int samples = 100;
        var dataSize = samples * channels * (bitsPerSample / 8);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * (bitsPerSample / 8));
        writer.Write((short)(channels * (bitsPerSample / 8)));
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]);
        writer.Flush();

        return stream.ToArray();
    }

    private sealed class CapturingHandler(string responseBody) : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class FakeTranscriber(string text) : IWhisperTranscriber
    {
        public int Calls { get; private set; }

        public bool IsAvailable => true;

        public string ModelPath => "test";

        public Task<string> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(text);
        }
    }

    private sealed class AlwaysAliveProbe : ILocalNodeHealthProbe
    {
        public Task<bool> IsAliveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class StubRecognitionService(ParsedDocumentDto document)
        : IMultimodalRecognitionService
    {
        public Task<ParsedDocumentDto> RecognizeInvoiceAsync(
            Stream? imageStream,
            Stream? audioStream,
            string? text,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(document);
        }
    }

    private sealed class ThrowingService(Exception exception) : IMultimodalRecognitionService
    {
        public Task<ParsedDocumentDto> RecognizeInvoiceAsync(
            Stream? imageStream,
            Stream? audioStream,
            string? text,
            CancellationToken cancellationToken)
        {
            return Task.FromException<ParsedDocumentDto>(exception);
        }
    }

    private sealed class CapturingService : IMultimodalRecognitionService
    {
        public string? Text { get; private set; }

        public Stream? Audio { get; private set; }

        public Task<ParsedDocumentDto> RecognizeInvoiceAsync(
            Stream? imageStream,
            Stream? audioStream,
            string? text,
            CancellationToken cancellationToken)
        {
            Audio = audioStream;
            Text = text;

            return Task.FromResult(new ParsedDocumentDto());
        }
    }
}
