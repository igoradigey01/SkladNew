# Задача: Настройка ASP.NET Core Identity и Аудита

## 1. Контекст
- **Проект:** `SkladNew`
- **Бэкенд:** ASP.NET Core Web API (папка `backend/`)
- **База данных:** 
  - `Sklad_v4.1.1` (основная, для бизнес-данных)
  - `Sklad_v1_HR` (для пользователей Identity)
- **Цель:** Настроить аутентификацию и автоматически записывать, кто создал или изменил запись в основной БД.

## 2. Задачи по шагам

### Шаг 1. Подключение пакетов
В проекте `SkladApi` (внутри `backend/src/`) добавь необходимые NuGet-пакеты:
```bash
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Dapper
```

### Шаг 2. Настройка строк подключения
В `backend/src/SkladApi/appsettings.json` пропиши две строки подключения:
```json
{
  "ConnectionStrings": {
    "SkladDb": "Server=192.168.43.14,1433;Database=Sklad_v4.1.1;User Id=sa;Password=2a1sp-msX01;TrustServerCertificate=True;",
    "HRDb": "Server=192.168.43.14,1433;Database=Sklad_v1_HR;User Id=sa;Password=2a1sp-msX01;TrustServerCertificate=True;"
  }
}
```

### Шаг 3. Создание контекста для HR (Identity)
Создай файл `backend/src/SkladApi/Data/HRContext.cs`:
```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SkladApi.Data;

public class ApplicationUser : IdentityUser
{
    public string? Фио { get; set; }
}

public class HRContext : IdentityDbContext<ApplicationUser>
{
    public HRContext(DbContextOptions<HRContext> options) : base(options) { }
}
```

### Шаг 4. Создание контекста для Основной БД (Sklad)
Создай файл `backend/src/SkladApi/Data/SkladContext.cs`:
```csharp
using Microsoft.EntityFrameworkCore;

namespace SkladApi.Data;

public class SkladContext : DbContext
{
    public SkladContext(DbContextOptions<SkladContext> options) : base(options) { }
    
    // Пример DbSet (добавь свои таблицы)
    // public DbSet<Приход> Приходы { get; set; }
}
```

### Шаг 5. Настройка в Program.cs
Настрой Identity и DbContext в `backend/src/SkladApi/Program.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkladApi.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Настройка БД для HR (Identity)
builder.Services.AddDbContext<HRContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HRDb")));

// 2. Настройка БД для Sklad (Основная)
builder.Services.AddDbContext<SkladContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SkladDb")));

// 3. Настройка Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<HRContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication(); // Важно!
app.UseAuthorization();  // Важно!

app.MapControllers();
app.Run();
```

### Шаг 6. Проверка
1. Убедись, что проект собирается: `dotnet build`
2. Запусти проект: `dotnet run`
3. Если ошибок нет — Identity настроен.

## 3. Ограничения
- Не используй статические классы для получения текущего пользователя. Только DI (`UserManager`, `IHttpContextAccessor`).
- База `Sklad_v1_HR` должна существовать (она у нас есть).

## 4. Ссылки
- Скрипты БД: `SkladNew/db/sklad_v4_1/`
- Скрипты HR: `SkladNew/db/sklad_v1_hr/`