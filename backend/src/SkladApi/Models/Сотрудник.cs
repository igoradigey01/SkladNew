namespace SkladApi.Models;

/// <summary>
/// Карточка сотрудника из существующей таблицы Sklad_v1_HR.dbo.Сотрудник.
/// Таблица создана SQL-скриптами и не управляется EF-миграциями.
/// </summary>
public class Сотрудник
{
    public int Id { get; set; }
    public Guid Guid { get; set; }
    public string Имя { get; set; } = null!;
    public string? Адрес { get; set; }
    public string? Телефон { get; set; }
    public int СпециальностьId { get; set; }

    public ICollection<ApplicationUser> ApplicationUsers { get; set; } = new List<ApplicationUser>();
}
