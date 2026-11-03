# Шпаргалка: Сквозной тест архитектуры (API + Angular)

## Цель
Проверить, что бэкенд (`SkladX01`) и фронтенд (`skladx01` в NX) успешно общаются.

## Шаги

### 1. Бэкенд (C#)
- В `SkladApi/Controllers/КлиентController.cs` создать эндпоинт `GET /api/clients`.
- Использовать `IDbConnection` (Dapper).
- Строка подключения: `appsettings.json` (ключ `SkladDb`).
- **Важно:** Указать маршрут на латинице: `[Route("api/clients")]`.

### 2. Настройка CORS (Program.cs)
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // порт Angular
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
app.UseCors("AllowAngular");
```

### 3. Фронтенд (Angular NX)
- Проект: `apps/skladx01`.
- Создать сервис `ClientService` (HttpClient).
- URL: `http://localhost:5000/api/clients`.
- Создать компонент `ClientListComponent` (таблица: ID, ФИО, Телефон, Город).

## ⚠️ Важный нюанс про URL
Если в контроллере использовать `[Route("api/[controller]")]` и назвать контроллер `КлиентController`, URL будет `http://localhost:5000/api/Клиент`. Браузеры это переварят, но лучше явно писать `api/clients` (латиницей), чтобы избежать проблем с URL-encoding.

## 🎯 Проверка
1. Запустить бэкенд: `dotnet run` (в папке `SkladApi`).
2. Запустить фронтенд: `npx nx serve skladx01`.
3. Открыть браузер: `http://localhost:4200/clients`.
4. Убедиться, что данные загрузились.