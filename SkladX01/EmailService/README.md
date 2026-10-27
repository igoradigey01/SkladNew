# EmailService

Библиотека отправки почты на **MailKit/MimeKit** для .NET 8. Не зависит от
ASP.NET Core: вложения передаются байтами, поэтому проект можно использовать
в API, консольных приложениях и фоновых задачах.

## Настройки

Секция `EmailSettings`:

| Ключ | Назначение | По умолчанию |
|------|------------|--------------|
| `From` | адрес отправителя | — (обязателен) |
| `FromName` | отображаемое имя | пусто |
| `SmtpServer` | SMTP-сервер | — (обязателен) |
| `Port` | порт | `587` |
| `UserName` | логин SMTP | пусто (без аутентификации) |
| `Password` | пароль SMTP | — |
| `SecureSocketOptions` | `Auto`, `SslOnConnect`, `StartTls`, `StartTlsWhenAvailable`, `None` | `Auto` |
| `TimeoutMilliseconds` | таймаут SMTP | `10000` |

Пароль **не** хранится в репозитории. В API используйте User Secrets:

```bash
cd SkladNew/SkladX01/SkladApi
dotnet user-secrets set "EmailSettings:From" "noreply@example.com"
dotnet user-secrets set "EmailSettings:SmtpServer" "smtp.example.com"
dotnet user-secrets set "EmailSettings:Port" "587"
dotnet user-secrets set "EmailSettings:UserName" "<логин>"
dotnet user-secrets set "EmailSettings:Password" "<пароль>"
dotnet user-secrets set "EmailSettings:SecureSocketOptions" "StartTls"
```

## Регистрация в DI

```csharp
using EmailService;

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.AddScoped<IEmailSender, EmailSender>();
```

## Использование

```csharp
public sealed class NotificationService
{
    private readonly IEmailSender _emailSender;

    public NotificationService(IEmailSender emailSender) => _emailSender = emailSender;

    public Task SendAsync(string to, CancellationToken cancellationToken)
    {
        var message = new Message(
            new[] { to },
            "Добро пожаловать",
            "<p>Учётная запись создана.</p>");

        // Реальная отправка — только когда SMTP настроен.
        return _emailSender.SendEmailAsync(message, cancellationToken);
    }
}
```

Вложения:

```csharp
var attachment = new EmailAttachment("акт.pdf", pdfBytes, "application/pdf");
var message = new Message(new[] { to }, "Акт", "<p>Во вложении.</p>",
    attachments: new[] { attachment });
```

## Проверка без отправки

`EmailSender.CreateEmailMessage` собирает `MimeMessage` и не открывает SMTP.
Это удобно для тестов и предпросмотра:

```csharp
var mime = sender.CreateEmailMessage(message);
Console.WriteLine(mime.Subject);
```
