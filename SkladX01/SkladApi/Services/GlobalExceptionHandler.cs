using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SkladApi.Services;

/// <summary>
/// Глобальный обработчик необработанных исключений API. Возвращает клиенту
/// RFC 7807 ProblemDetails, а полный стек пишет в лог.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Необработанное исключение при обработке {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var (status, title) = exception switch
        {
            ArgumentException or FormatException =>
                (HttpStatusCode.BadRequest, "Некорректные данные запроса"),
            UnauthorizedAccessException =>
                (HttpStatusCode.Forbidden, "Доступ запрещён"),
            KeyNotFoundException =>
                (HttpStatusCode.NotFound, "Ресурс не найден"),
            _ =>
                (HttpStatusCode.InternalServerError, "Внутренняя ошибка сервера")
        };

        httpContext.Response.StatusCode = (int)status;

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = _environment.IsDevelopment()
                ? exception.ToString()
                : "Обратитесь к администратору.",
            Instance = httpContext.Request.Path
        };

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
