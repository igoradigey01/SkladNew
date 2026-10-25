using Microsoft.AspNetCore.Identity;

namespace SkladApi.Models;

/// <summary>
/// Учётная запись ASP.NET Identity. Хранится в Sklad_v1_HR и обязательно
/// связана с карточкой сотрудника той же базы.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? Фио { get; set; }

    public int СотрудникId { get; set; }
    public Сотрудник Сотрудник { get; set; } = null!;
}
