# 2026-10-27 — разделение backend на проекты

Задачи: [`03-backend-structure.md`](../tasks/03-backend-structure.md),
[`04-create-email-service.md`](../tasks/04-create-email-service.md),
[`06-create-hrdb-project.md`](../tasks/06-create-hrdb-project.md).
Задача 05 (`05-add-hr-context.md`) отсутствовала в репозитории; существующий
HR-контекст проверен и считается выполненным.

## Структура

```
SkladX01/
├── SkladX01.sln
├── SkladApi/       Web API, DI, Swagger, HttpCurrentUserService
├── SkladDB/        SkladContext, 11 моделей, IAuditable, ICurrentUserService
├── HRDB/           Identity, ApplicationUser, Сотрудник, HRContext, миграции
└── EmailService/   SMTP на MailKit, EmailSettings, IEmailSender
```

- `SkladDB` больше не зависит от ASP.NET Core Identity: аудит получает
  пользователя через `ICurrentUserService`, реализация — в `SkladApi`.
- `HRDB` зависит только от EF Core и Identity.
- `EmailService` не зависит от ASP.NET Core: вложения передаются байтами.

## EmailService

- `EmailSettings` + `IOptions<EmailSettings>`.
- `IEmailSender` — только async, с `CancellationToken`.
- `EmailSender` на MailKit 4.18.2: `SecureSocketOptions`, `ILogger`,
  корректное `DisconnectAsync`, вложения `EmailAttachment`.
- Регистрация в `Program.cs`; `appsettings.example.json` с заглушками,
  реальные значения — в User Secrets.
- `EmailSender.CreateEmailMessage` собирает `MimeMessage` без отправки — для
  тестов и предпросмотра.

## HRDB

- `ApplicationUser`, `Сотрудник`, `HRContext` и миграции перенесены из
  `SkladApi` в `HRDB` (namespace `HRDB.*`).
- Сохранена обязательная связь: `СотрудникId int NOT NULL`, unique index,
  FK `ON DELETE NO ACTION`, `Сотрудник` — `ExcludeFromMigrations()`.
- Добавлено `ApplicationUser.ДатаСоздания` (`datetime2 NOT NULL`, default
  `SYSUTCDATETIME()`) миграцией `AddUserCreatedAt`.
- Идемпотентный SQL перегенерирован:
  `db/sklad_v1_hr/create-identity-tables.sql`.

## Проверки

- `dotnet build` — 0 ошибок, 0 предупреждений.
- Swagger — `200`.
- Smoke аудита: все 11 DbSet читаются, `СоздалId`/`ИзменилId`/даты
  заполняются через `ICurrentUserService`.
- Smoke EmailService: MIME-сообщение и вложение собираются, реальная
  отправка не выполнялась.
- Smoke HRDB: пользователь создаётся с `СотрудникId`, `ДатаСоздания`
  заполняется; вторая учётка для того же сотрудника блокируется unique index.
- Миграция `AddUserCreatedAt` применена к `Sklad_v1_HR`; FK и unique index
  сохранены.

## Переименование

- Папка `backend` переименована в `SkladX01`, решение `SkladApi.sln` —
  в `SkladX01.sln` (задача 07). Переименование выполнено через `git mv`,
  история файлов сохранена; названия проектов не менялись.
- Ссылки на старый путь обновлены в `README.md`,
  `db/sklad_v1_hr/database-schema.md`, `EmailService/README.md` и журналах.

## Дальше

- Бизнес-эндпоинты и сидирование администратора.
