using MailKit.Security;

namespace EmailService;

/// <summary>
/// Настройки SMTP. Задаются в секции <c>EmailSettings</c> и биндятся через
/// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>.
/// </summary>
public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>Адрес отправителя, например <c>noreply@example.com</c>.</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>Отображаемое имя отправителя.</summary>
    public string? FromName { get; set; }

    /// <summary>SMTP-сервер.</summary>
    public string SmtpServer { get; set; } = string.Empty;

    /// <summary>Порт SMTP. По умолчанию 587 (STARTTLS).</summary>
    public int Port { get; set; } = 587;

    /// <summary>Логин SMTP. Если пустой — аутентификация не выполняется.</summary>
    public string? UserName { get; set; }

    /// <summary>Пароль SMTP. Хранить только в User Secrets или переменных окружения.</summary>
    public string? Password { get; set; }

    /// <summary>
    /// Режим шифрования: <c>Auto</c>, <c>SslOnConnect</c>, <c>StartTls</c>,
    /// <c>StartTlsWhenAvailable</c>, <c>None</c>.
    /// </summary>
    public SecureSocketOptions SecureSocketOptions { get; set; } = SecureSocketOptions.Auto;

    /// <summary>Таймаут операций с SMTP в миллисекундах.</summary>
    public int TimeoutMilliseconds { get; set; } = 10_000;
}
