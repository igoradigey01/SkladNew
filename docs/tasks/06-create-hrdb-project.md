# Задача: Создать отдельный проект HRDB (Identity)

## 1. КОНТЕКСТ
- **Цель:** Вынести всю работу с базой `Sklad_v1_HR` (пользователи, роли) в отдельный проект `HRDB`.
- **Архитектура:** Монолит с четким разделением слоев.
- **Текущая структура:** `SkladApi` (Web), `SkladDB` (Business Data), `EmailService` (Utils).

## 2. ЦЕЛЕВАЯ СТРУКТУРА

Внутри `SkladNew/backend/` должна появиться новая папка `HRDB`:

```text
SkladNew/backend/
├── SkladApi.sln
├── SkladApi/           # Web API (Контроллеры)
├── SkladDB/            # Основная БД (Склад)
├── HRDB/               # 🎯 НОВЫЙ ПРОЕКТ (Identity / Сотрудники)
│   ├── HRDB.csproj
│   ├── Data/
│   │   └── HRContext.cs
│   └── Models/
│       └── ApplicationUser.cs
└── EmailService/
```

## 3. ЗАДАЧИ ПО РЕАЛИЗАЦИИ

### Шаг 1. Создать проект `HRDB`
Находясь в папке `SkladNew/backend/`, выполни:

```bash
dotnet new classlib -n HRDB -o HRDB
dotnet sln add HRDB/HRDB.csproj
```

### Шаг 2. Добавить пакеты в `HRDB`
```bash
cd HRDB
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

### Шаг 3. Создать модели и контекст внутри `HRDB`

**Файл:** `HRDB/Models/ApplicationUser.cs`
```csharp
using Microsoft.AspNetCore.Identity;

namespace HRDB.Models;

public class ApplicationUser : IdentityUser
{
    public string? Фио { get; set; }
    public DateTime ДатаСоздания { get; set; } = DateTime.UtcNow;
}
```

**Файл:** `HRDB/Data/HRContext.cs`
```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HRDB.Models;

namespace HRDB.Data;

public class HRContext : IdentityDbContext<ApplicationUser>
{
    public HRContext(DbContextOptions<HRContext> options) : base(options) { }
}
```

### Шаг 4. Настроить ссылки (Project References)
Проект `SkladApi` должен знать о `HRDB`, чтобы использовать `UserManager`.
Проект `SkladDB` (для аудита) тоже может ссылаться на `HRDB`, чтобы брать оттуда `ApplicationUser` (если нужно).

```bash
cd /mnt/developer/VS_Prodject/my_Sclad_v1/SkladNew/backend

# API ссылается на HRDB
dotnet add SkladApi/SkladApi.csproj reference HRDB/HRDB.csproj

# SkladDB (если нужно для аудита) ссылается на HRDB
# dotnet add SkladDB/SkladDB.csproj reference HRDB/HRDB.csproj
```

### Шаг 5. Обновить `Program.cs` в `SkladApi`
Теперь настройки Identity берутся из нового проекта:

```csharp
using HRDB.Data;
using HRDB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Контекст HR (Identity)
builder.Services.AddDbContext<HRContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HRDb")));

// 2. Контекст основной БД
// builder.Services.AddDbContext<SkladContext>(...);

// 3. Настройка Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<HRContext>()
    .AddDefaultTokenProviders();

// ... остальной код
```

### Шаг 6. Удалить старый `HRContext` из `SkladDB`
Если в `SkladDB` был создан `HRContext` или `ApplicationUser` (из предыдущих шагов), **удали** их, чтобы не было дублирования. Теперь всё, что связано с HR, живет только в `HRDB`.

## 4. ОГРАНИЧЕНИЯ
- ❌ Не смешивать бизнес-модели (`Клиент`, `Приход`) и модели Identity (`ApplicationUser`) в одном проекте.
- ✅ `HRDB` должен зависеть только от EF Core и Identity.
- ✅ `SkladDB` не должен зависеть от `HRDB` (если это не требуется для аудита).

## 5. ССЫЛКИ
- Схема HR: `SkladNew/db/sklad_v1_hr/`