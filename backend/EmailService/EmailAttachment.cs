namespace EmailService;

/// <summary>
/// Вложение письма. Содержимое передаётся байтами, чтобы библиотека не
/// зависела от ASP.NET Core и могла использоваться в любом .NET-проекте.
/// </summary>
public sealed record EmailAttachment(
    string FileName,
    byte[] Content,
    string ContentType = "application/octet-stream");
