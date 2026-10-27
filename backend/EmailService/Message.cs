namespace EmailService;

/// <summary>
/// Письмо: получатели, тема, тело и необязательные вложения.
/// </summary>
public sealed class Message
{
    public IReadOnlyList<string> To { get; }
    public string Subject { get; }
    public string Content { get; }
    public bool IsHtml { get; }
    public IReadOnlyList<EmailAttachment> Attachments { get; }

    public Message(
        IEnumerable<string> to,
        string subject,
        string content,
        bool isHtml = true,
        IEnumerable<EmailAttachment>? attachments = null)
    {
        ArgumentNullException.ThrowIfNull(to);

        var recipients = to
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .ToArray();

        if (recipients.Length == 0)
            throw new ArgumentException("Нужен хотя бы один получатель.", nameof(to));

        To = recipients;
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        Content = content ?? string.Empty;
        IsHtml = isHtml;
        Attachments = attachments?.ToArray() ?? Array.Empty<EmailAttachment>();
    }
}
