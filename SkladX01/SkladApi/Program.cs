using EmailService;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
using HRDB.Data;
using HRDB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Polly;
using SkladApi.Hubs;
using SkladApi.Repositories;
using SkladApi.Services;
using SkladDB.Data;
using SkladDB.Services;

var builder = WebApplication.CreateBuilder(args);

var skladConnection = RequireConnection(builder.Configuration, "SkladDb");
var hrConnection = RequireConnection(builder.Configuration, "HRDb");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.Configure<WhisperSettings>(
    builder.Configuration.GetSection(WhisperSettings.SectionName));
builder.Services.AddSingleton<IWhisperTranscriber, WhisperTranscriber>();
builder.Services.AddScoped<IEmailSender, EmailSender>();

builder.Services.AddScoped<IКлиентRepository, КлиентRepository>();
builder.Services.AddScoped<IMеткаРасчетRepository, МеткаРасчетRepository>();
builder.Services.AddScoped<IОтпущенныйТоварRepository, ОтпущенныйТоварRepository>();
builder.Services.AddScoped<IПринятыеДеньгиRepository, ПринятыеДеньгиRepository>();
builder.Services.AddScoped<IНоменклатураRepository, НоменклатураRepository>();
builder.Services.AddScoped<IПоставщикRepository, ПоставщикRepository>();
builder.Services.AddScoped<IСделкаПоставщикаRepository, СделкаПоставщикаRepository>();
builder.Services.AddScoped<IПолученныйТоварRepository, ПолученныйТоварRepository>();
builder.Services.AddScoped<IОтданныеДеньгиRepository, ОтданныеДеньгиRepository>();
builder.Services.AddScoped<IСкладRepository, СкладRepository>();
builder.Services.AddScoped<IClientVoiceSearchService, ClientVoiceSearchService>();
builder.Services.Configure<DeepSeekSettings>(
    builder.Configuration.GetSection(DeepSeekSettings.SectionName));
builder.Services.Configure<AiRouterSettings>(
    builder.Configuration.GetSection(AiRouterSettings.SectionName));

builder.Services.AddSingleton<LocalNodeStateTracker>();
builder.Services.AddSingleton<GpuSlotLimiter>();

// Локальный провайдер Ollama: Circuit Breaker + таймаут генерации.
builder.Services.AddHttpClient<OllamaMultimodalProvider>((services, client) =>
{
    var ollama = services.GetRequiredService<IOptions<OllamaSettings>>().Value;
    client.BaseAddress = new Uri(ollama.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(ollama.TimeoutSeconds);
})
.AddResilienceHandler("local-ai", pipeline =>
{
    pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        SamplingDuration = TimeSpan.FromSeconds(60),
        FailureRatio = 0.5,
        MinimumThroughput = 2,
        BreakDuration = TimeSpan.FromSeconds(30),
    });
    pipeline.AddTimeout(TimeSpan.FromSeconds(20));
});

builder.Services.AddTransient<ILocalAiProvider>(
    services => services.GetRequiredService<OllamaMultimodalProvider>());

// Быстрый зонд живости Ollama (600 мс).
builder.Services.AddHttpClient<OllamaHealthProbe>((services, client) =>
{
    var ollama = services.GetRequiredService<IOptions<OllamaSettings>>().Value;
    client.BaseAddress = new Uri(ollama.BaseUrl);
    client.Timeout = TimeSpan.FromMilliseconds(600);
});

builder.Services.AddTransient<ILocalNodeHealthProbe>(
    services => services.GetRequiredService<OllamaHealthProbe>());

// Облачный провайдер DeepSeek Flash (vision + JSON Output).
builder.Services.AddHttpClient<DeepSeekMultimodalProvider>((services, client) =>
{
    var deepSeek = services.GetRequiredService<IOptions<DeepSeekSettings>>().Value;
    client.BaseAddress = new Uri(deepSeek.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(deepSeek.TimeoutSeconds);
})
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 1;
    options.Retry.DisableForUnsafeHttpMethods();
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(25);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
});

builder.Services.AddTransient<ICloudAiProvider>(
    services => services.GetRequiredService<DeepSeekMultimodalProvider>());

builder.Services.AddScoped<SmartAiRouter>();
builder.Services.AddScoped<IMultimodalRecognitionService, MultimodalRecognitionService>();

// Асинхронная очередь тяжёлых файлов + SignalR-пуш результата.
builder.Services.AddSignalR();
builder.Services.AddSingleton<ParsingJobQueue>();
builder.Services.AddHostedService<ParsingQueueWorker>();

builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IEmployeeContext, EmployeeContext>();
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.Configure<OllamaSettings>(
    builder.Configuration.GetSection(OllamaSettings.SectionName));

builder.Services.AddHttpClient<OllamaEmbeddingService>();

builder.Services.AddHttpClient("ollama", (services, client) =>
{
    var ollama = services.GetRequiredService<IOptions<OllamaSettings>>().Value;
    client.Timeout = TimeSpan.FromSeconds(ollama.TimeoutSeconds);
});

builder.Services.AddHttpClient("deepseek", (services, client) =>
{
    var deepSeek = services.GetRequiredService<IOptions<DeepSeekSettings>>().Value;
    client.BaseAddress = new Uri(deepSeek.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(deepSeek.TimeoutSeconds);
});

builder.Services.AddDbContext<HRContext>(options =>
    options.UseSqlServer(hrConnection));

builder.Services.AddDbContext<SkladContext>(options =>
    options.UseSqlServer(skladConnection));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<HRContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

WarnIfWhisperMissing(app);

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ParsingHub>("/hubs/parsing");

app.Run();

static string RequireConnection(IConfiguration configuration, string name)
{
    var value = configuration.GetConnectionString(name);

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"Строка подключения '{name}' не задана. " +
            "Задайте её через dotnet user-secrets или переменные окружения.");
    }

    return value;
}

static void WarnIfWhisperMissing(WebApplication app)
{
    var transcriber = app.Services.GetRequiredService<IWhisperTranscriber>();

    if (transcriber.IsAvailable)
    {
        return;
    }

    var message =
        $"Модель Whisper не найдена: {transcriber.ModelPath}. " +
        "/api/recognition/parse будет отвечать 503.";

    if (app.Configuration.GetValue<bool>("Whisper:Required"))
    {
        throw new InvalidOperationException(message);
    }

    app.Logger.LogWarning("{Message}", message);
}
