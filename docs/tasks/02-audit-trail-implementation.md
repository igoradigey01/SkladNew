# Задача: Реализация Audit Trail (Аудит) в коде

## 1. КОНТЕКСТ
- **БД:** `Sklad_v4.1.1` (все 19 таблиц уже имеют колонки `СоздалId`, `ИзменилId`, `ДатаСоздания`, `ДатаИзменения`).
- **Бэкенд:** `SkladNew/backend/src/SkladApi`.
- **Цель:** Реализовать автоматическое заполнение этих колонок в C# только для **критичных** бизнес-таблиц, чтобы не нагружать базу и код.

## 2. РАЗДЕЛЕНИЕ ТАБЛИЦ (СТРАТЕГИЯ)

Аудит в коде (через интерфейс `IAuditable`) применяем **только** к таблицам, где важна история изменения сумм и остатков.

### Группа 1: Транзакционные (Аудит ОБЯЗАТЕЛЕН) 🔴
- `Приход`
- `Расход`
- `Деньги`
- `НакладнаяРасход`
- `Сделка` (или `КлиентМеткаРасчет`)
- `Расхождение`
- `Ревизия`

### Группа 2: Основные справочники (Аудит ЖЕЛАТЕЛЕН) 🟡
- `Номенклатура`
- `Клиент`
- `Поставщик`
- `Склад`

### Группа 3: Вспомогательные справочники (Аудит НЕ НУЖЕН) 🟢
- `ВидНоменклатуры`, `ГруппаНоменклатуры`, `ЕдИзмерения`, `КатегорияНоменклатуры`, `СписокЗначений`, `ТипЗначения`, `Характеристика`, `ЗначениеХарактеристики`.
*(Для них колонки в БД остаются `NULL`, код их не трогает).*

## 3. РЕАЛИЗАЦИЯ (C#)

### Шаг 1. Создать интерфейс `IAuditable`
Файл: `SkladApi/Models/IAuditable.cs`

```csharp
namespace SkladApi.Models;

public interface IAuditable
{
    string? СоздалId { get; set; }
    string? ИзменилId { get; set; }
    DateTime ДатаСоздания { get; set; }
    DateTime? ДатаИзменения { get; set; }
}
```

### Шаг 2. Обновить модели (Только для Групп 1 и 2)
Добавь `IAuditable` в классы, например, для `Клиент`:

```csharp
namespace SkladApi.Models;

public class Клиент : IAuditable
{
    public int Id { get; set; }
    public string Псевдоним { get; set; } = null!;
    public string Фио { get; set; } = null!;
    // ... остальные поля ...

    // Аудит
    public string? СоздалId { get; set; }
    public string? ИзменилId { get; set; }
    public DateTime ДатаСоздания { get; set; }
    public DateTime? ДатаИзменения { get; set; }
}
```
*(Сделай то же самое для `Приход`, `Расход`, `Деньги`, `Номенклатура`, `Поставщик` и т.д. из списка).*

### Шаг 3. Настроить `SkladContext` (EF Core)
Если используется EF Core, переопредели `SaveChangesAsync` в `SkladApi/Data/SkladContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkladApi.Models;

public class SkladContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<ApplicationUser> _userManager;

    public SkladContext(
        DbContextOptions<SkladContext> options,
        IHttpContextAccessor httpContextAccessor,
        UserManager<ApplicationUser> userManager) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userId = user != null ? _userManager.GetUserId(user) : null;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.СоздалId = userId;
                    entry.Entity.ДатаСоздания = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.ИзменилId = userId;
                    entry.Entity.ДатаИзменения = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### Шаг 4. Если используется Dapper (без EF Core)
В репозиториях передавай `userId` **только** для транзакционных таблиц.

Пример метода для `Приход`:

```csharp
public async Task<int> CreateAsync(Приход приход, string userId)
{
    using var connection = new SqlConnection(_connectionString);
    var sql = @"
        INSERT INTO Приход (НоменклатураId, Quantity, Price, Sum, СоздалId, ДатаСоздания)
        VALUES (@НоменклатураId, @Quantity, @Price, @Sum, @СоздалId, @ДатаСоздания);
        SELECT CAST(SCOPE_IDENTITY() as int);";

    return await connection.ExecuteScalarAsync<int>(sql, new
    {
        приход.НоменклатураId,
        приход.Quantity,
        приход.Price,
        приход.Sum,
        СоздалId = userId,
        ДатаСоздания = DateTime.UtcNow
    });
}
```
*(Для `ЕдИзмерения` или `Характеристика` этот параметр просто не передается).*

## 4. ОГРАНИЧЕНИЯ
- ❌ Не реализовывать `IAuditable` для Группы 3 (вспомогательные справочники).
- ✅ Использовать `DateTime.UtcNow` (UTC).
- ✅ В Dapper передавать `userId` только в критичные методы.

## 5. ССЫЛКИ
- Схема БД: `SkladNew/db/sklad_v4_1/`
- Скрипт аудита: `SkladNew/db/sklad_v4_1/add-audit.sql` (если сохранён)