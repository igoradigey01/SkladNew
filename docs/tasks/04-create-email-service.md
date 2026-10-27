# Задача: Создать EmailService на основе старого проекта

## 1. КОНТЕКСТ
- **Цель:** Создать проект `EmailService` в новом решении `SkladNew/backend/`.
- **Основа:** Использовать существующий код из `/home/ks33/Документы/VS_Ubuntu/SkladX01/EmailService`.
- **Окружение:** .NET 8, современные стандарты ASP.NET Core.

## 2. ЧТО НУЖНО СДЕЛАТЬ

### Шаг 1. Анализ старого проекта
1. Перейди в папку `/home/ks33/Документы/VS_Ubuntu/SkladX01/EmailService`.
2. Изучи структуру проекта, файлы `.cs`, зависимости (`.csproj`).
3. Пойми, как отправляется почта (SMTP, MailKit, SendGrid и т.д.) и какие настройки используются.

### Шаг 2. Создание нового проекта
В папке `SkladNew/backend/` создай новый проект библиотеки классов:
```bash
cd /mnt/developer/VS_Prodject/my_Sclad_v1/SkladNew/backend
dotnet new classlib -n EmailService -o EmailService
dotnet sln add EmailService/EmailService.csproj
```

### Шаг 3. Перенос и адаптация кода
1. Скопируй файлы `.cs` из старого проекта в новый `SkladNew/backend/EmailService/`.
2. **ОБЯЗАТЕЛЬНО** обнови код:
   - Замени устаревшие NuGet-пакеты (например, `System.Net.Mail` на `MailKit`, если это уместно, или обнови версии).
   - Убери прямые зависимости от конфигурации (вынеси строки подключения и настройки SMTP в интерфейс `IOptions<EmailSettings>`).
   - Сделай методы асинхронными (`async Task`), если они такими не были.
3. Добавь интерфейс `IEmailSender` (если его нет), чтобы его можно было внедрять через DI.

### Шаг 4. Настройка DI (Dependency Injection)
1. Создай класс `EmailSettings` для хранения настроек (Host, Port, User, Password).
2. В `SkladApi/Program.cs` добавь регистрацию сервиса:
   ```csharp
   builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
   builder.Services.AddScoped<IEmailSender, EmailSender>();
   ```
3. В `SkladApi/appsettings.json` добавь пустые заглушки для настроек (без реальных паролей).

### Шаг 5. Проверка
1. Убедись, что проект собирается (`dotnet build`).
2. Создай простой тестовый метод отправки письма (можно закомментировать реальную отправку, чтобы не спамить).

## 3. ОГРАНИЧЕНИЯ
- ❌ Не копировать код "как есть", если он написан для .NET Framework. Адаптировать под .NET 8.
- ✅ Использовать `IOptions` для настроек.
- ✅ Использовать `IEmailSender` для внедрения зависимостей.

## 4. ССЫЛКИ
- Старый проект: `/home/ks33/Документы/VS_Ubuntu/SkladX01/EmailService`
- Новый проект: `SkladNew/backend/EmailService`