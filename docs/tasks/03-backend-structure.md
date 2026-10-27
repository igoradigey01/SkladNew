# Задача: Создать правильную структуру Backend (Solution)

## 1. КОНТЕКСТ
- **Проект:** Склад (SkladNew).
- **Текущая папка:** `SkladNew/backend/`.
- **Цель:** Создать чистое решение (Solution) и разделить код на логические проекты (API, БД, Сервисы), как показано в эталоне.

## 2. ЦЕЛЕВАЯ СТРУКТУРА

Внутри папки `SkladNew/backend/` должна быть следующая структура:

```text
SkladNew/backend/
├── SkladApi.sln              # Общее решение
├── SkladApi/                 # 🎯 Главный Web API (Контроллеры, Program.cs)
│   ├── SkladApi.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   └── Controllers/
├── SkladDB/                  # 🗄️ Слой работы с БД (EF Core / Dapper)
│   ├── SkladDB.csproj
│   ├── Models/               # Классы-сущности (Клиент, Приход)
│   ├── Data/                 # DbContext, Репозитории
│   └── Migrations/           # Миграции EF Core
└── EmailService/             # 📧 Сервис отправки почты (если нужно)
    ├── EmailService.csproj
    └── EmailSender.cs
```

## 3. ЗАДАЧИ ПО СОЗДАНИЮ

Выполни эти шаги в терминале, находясь в папке `SkladNew/backend/`:

### Шаг 1. Очистить старое (если есть)
Если там уже есть папка `src` или другие файлы от предыдущих тестов — аккуратно удали их, чтобы не было путаницы.

### Шаг 2. Создать Solution
```bash
dotnet new sln -n SkladApi
```

### Шаг 3. Создать проект Web API (Главный)
```bash
dotnet new webapi -n SkladApi -o SkladApi
```

### Шаг 4. Создать проект для БД (Class Library)
```bash
dotnet new classlib -n SkladDB -o SkladDB
```

### Шаг 5. Создать проект для Email (Class Library)
```bash
dotnet new classlib -n EmailService -o EmailService
```

### Шаг 6. Добавить проекты в Solution
```bash
dotnet sln add SkladApi/SkladApi.csproj
dotnet sln add SkladDB/SkladDB.csproj
dotnet sln add EmailService/EmailService.csproj
```

### Шаг 7. Настроить зависимости (Ссылки)
Проект API должен ссылаться на БД и Email, чтобы использовать их.

```bash
dotnet add SkladApi/SkladApi.csproj reference SkladDB/SkladDB.csproj
dotnet add SkladApi/SkladApi.csproj reference EmailService/EmailService.csproj
```

### Шаг 8. Добавить пакеты в `SkladDB`
```bash
cd SkladDB
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Dapper
cd ..
```

### Шаг 9. Проверить сборку
```bash
dotnet build
```

## 4. ПЕРЕНОС КОДА (ИЗ СТАРОЙ ПАПКИ)
Если у тебя уже есть готовые модели (например, `Клиент` или `Приход`), перенеси их в `SkladDB/Models/`.
- Контекст БД (`SkladContext`) перенеси в `SkladDB/Data/`.
- Контроллеры оставь в `SkladApi/Controllers/`.

## 5. ОГРАНИЧЕНИЯ
- ❌ Не использовать папку `src/` (как было в старом варианте). Проекты должны лежать прямо в `backend/`.
- ✅ Использовать названия `SkladApi`, `SkladDB`, `EmailService`.

## 6. ССЫЛКИ
- Старая структура (для справки): `/home/ks33/Документы/VS_Ubuntu/SkladX01/SkladX01.sln`