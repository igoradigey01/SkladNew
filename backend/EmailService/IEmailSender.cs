namespace EmailService;

/// <summary>
/// Отправка электронной почты. Регистрируется в DI и внедряется в сервисы.
/// </summary>
public interface IEmailSender
{
    Task SendEmailAsync(Message message, CancellationToken cancellationToken = default);
}
