# Задача: Создать API для формы «Клиент» (Backend)

## ⚠️ ВАЖНЫЙ АРХИТЕКТУРНЫЙ КОНТЕКСТ
Проект использует подход Database First. 
- Проект `SkladDB` содержит только сгенерированные из БД сущности (например, `Клиент.cs`) и `DbContext`. 
- **ЗАПРЕЩЕНО** создавать новые файлы в `SkladDB` (кроме случаев, когда это требуется для EF Core). Этот проект может быть перегенерирован в любой момент, и любые ручные файлы будут удалены.
- **Весь ручной код** (DTO, Репозитории) должен находиться в проекте `SkladApi`.

## Контекст задачи
- Проект API: `SkladApi` (ASP.NET Core).
- База данных: MSSQL (Docker).
- Стек: Dapper, ASP.NET Core Web API.
- Задача: Создать REST API для получения данных формы «Клиент».

## Требования

### 1. DTO (Проект `SkladApi`)
Создай папку `Dtos` в `SkladApi` и помести туда классы:
- `КлиентDto`: Id, Псевдоним, Фио, Телефон, Город, ПределКредита.
- `МеткаРасчетDto`: Id, Номер, Долг.
- `ОтпущенныйТоварDto`: Id, Номер, Дата, Сумма, МеткаId.
- `ПринятыеДеньгиDto`: Id, Дата, Сумма, МеткаId.

### 2. Репозитории (Проект `SkladApi`)
Создай папку `Repositories` в `SkladApi`. 
**Важно:** Строка подключения берется из `IConfiguration` (ключ `ConnectionStrings:SkladDb`).
- `IКлиентRepository` -> `КлиентRepository`:
  - `GetAllAsync(string? phone, string? alias, string? city)` — фильтры.
  - `GetByIdAsync(int id)`.
- `IМеткаРасчетRepository` -> `МеткаРасчетRepository`:
  - `GetByClientIdAsync(int clientId)`.
- `IОтпущенныйТоварRepository` -> `ОтпущенныйТоварRepository`:
  - `GetByMarkIdAsync(int markId)`.
- `IПринятыеДеньгиRepository` -> `ПринятыеДеньгиRepository`:
  - `GetByMarkIdAsync(int markId)`.

**Примечание:** Используй `Microsoft.Data.SqlClient` и Dapper. Не используй сущности из `SkladDB`, возвращай сразу DTO (или маппи их вручную).

### 3. Контроллеры (Проект `SkladApi`)
Создай `КлиентController` в папке `Controllers`:
- `GET /api/clients` (с опциональными query-параметрами `?phone=...&alias=...&city=...`).
- `GET /api/clients/{id}`.
- `GET /api/clients/{id}/marks`.
- `GET /api/clients/marks/{markId}/products`.
- `GET /api/clients/marks/{markId}/payments`.

### 4. Настройка DI (Проект `SkladApi/Program.cs`)
Зарегистрируй все репозитории как `Scoped`.

## Ограничения
- ❌ **Не трогать папку `SkladDB`!** (Там только сгенерированный код).
- ❌ Не создавать миграции.
- ✅ Использовать Dapper.
- ✅ Имена таблиц в SQL должны точно совпадать с БД (например, `Клиент`, `МеткаРасчет`).
- ✅ Весь новый код (DTO, репозитории, контроллер) размещать только в `SkladApi`.

## Проверка
- `dotnet build` без ошибок.
- Swagger открывается, эндпоинты возвращают JSON.