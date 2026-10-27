using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailService;

/// <summary>
/// SMTP-отправитель на MailKit. Настройки приходят через
/// <see cref="IOptions{TOptions}"/>, секреты — только из конфигурации.
/// </summary>
public sealed class EmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IOptions<EmailSettings> options, ILogger<EmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(Message message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureConfigured();

        using var client = new SmtpClient
        {
            Timeout = _settings.TimeoutMilliseconds
        };

        _logger.LogDebug(
            "Отправка письма через {Host}:{Port}",
            _settings.SmtpServer,
            _settings.Port);

        await client.ConnectAsync(
            _settings.SmtpServer,
            _settings.Port,
            _settings.SecureSocketOptions,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_settings.UserName))
        {
            client.AuthenticationMechanisms.Remove("XOAUTH2");
            await client.AuthenticateAsync(
                _settings.UserName,
                _settings.Password!,
                cancellationToken);
        }

        await client.SendAsync(CreateEmailMessage(message), cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Письмо отправлено: {Subject}", message.Subject);
    }

    /// <summary>
    /// Собирает MimeMessage. Отдельный метод нужен для тестов и предпросмотра
    /// без реальной отправки.
    /// </summary>
    public MimeMessage CreateEmailMessage(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        EnsureConfigured();

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_settings.FromName ?? string.Empty, _settings.From));
        email.To.AddRange(message.To.Select(address => new MailboxAddress(string.Empty, address)));
        email.Subject = message.Subject;

        var body = new BodyBuilder();
        if (message.IsHtml)
            body.HtmlBody = message.Content;
        else
            body.TextBody = message.Content;

        foreach (var attachment in message.Attachments)
        {
            body.Attachments.Add(
                attachment.FileName,
                attachment.Content,
                ContentType.Parse(attachment.ContentType));
        }

        email.Body = body.ToMessageBody();
        return email;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_settings.From))
            throw new InvalidOperationException("EmailSettings:From не задан.");

        if (string.IsNullOrWhiteSpace(_settings.SmtpServer))
            throw new InvalidOperationException("EmailSettings:SmtpServer не задан.");

        if (_settings.Port is <= 0 or > 65535)
            throw new InvalidOperationException("EmailSettings:Port должен быть в диапазоне 1..65535.");

        if (!string.IsNullOrWhiteSpace(_settings.UserName) &&
            string.IsNullOrEmpty(_settings.Password))
            throw new InvalidOperationException(
                "EmailSettings:Password не задан, хотя EmailSettings:UserName заполнен.");
    }
}
