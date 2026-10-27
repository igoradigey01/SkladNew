# Схема БД `Sklad_v1_HR` — кадры

Кадровая часть вынесена из основной базы [`Sklad_v1`](../sklad_v1/database-schema.md).
Разные циклы работы и разный доступ: кадры нужны кадровику и бухгалтерии, склад —
кладовщику; права, резервирование и бэкапы разводятся независимо.

## Две таблицы

| Таблица | Колонки |
|---------|---------|
| `Специальность` | `Id` (PK), `Название` |
| `Сотрудник` | `Id` (PK), `Guid` (UNIQUE), `Имя`, `Адрес`, `Телефон`, `СпециальностьId` (FK) |

`Guid` — ключ связи между базами: основная база хранит его в `Приход`, `Расход`
и `Деньги` как `СотрудникGuid`. Поэтому Guid присваивается при создании
карточки (`DEFAULT NEWID()`) и **никогда не меняется**, тогда как `Id` живёт
только внутри этой базы. Один Guid — один человек навсегда; это то, что нужно
для исторических документов: переименовали сотрудника или перевели в другой
отдел — ссылки на старые операции остаются рабочими.

## Файлы

| Файл | Что делает |
|------|------------|
| `create-database.sql` | Создаёт `Sklad_v1_HR` |
| `create-tables.sql` | `Специальность` и `Сотрудник` |
| `create-indexes.sql` | Индексы по специальности и ФИО |
| `insert-reference-data.sql` | 6 специальностей и демо-сотрудник |
| `create-identity-tables.sql` | Таблицы ASP.NET Identity + связь `AspNetUsers.СотрудникId` (генерируется EF Core) |

```bash
sqlcmd -S localhost -E -f 65001 -b -i create-database.sql
sqlcmd -S localhost -E -f 65001 -b -i create-tables.sql
sqlcmd -S localhost -E -f 65001 -b -i create-indexes.sql
sqlcmd -S localhost -E -f 65001 -b -i insert-reference-data.sql
sqlcmd -S localhost -E -f 65001 -b -i create-identity-tables.sql
```

## ER-диаграмма

```mermaid
erDiagram
    Специальность ||--o{ Сотрудник : "СпециальностьId"
    Сотрудник {
        int Id, PK
        uniqueidentifier Guid, UK
        nvarchar Имя
        nvarchar Адрес
        nvarchar Телефон
        int СпециальностьId, FK
    }
    Специальность {
        int Id, PK
        nvarchar Название, UK
    }
```

## Таблицы и колонки

### `Сотрудник`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Guid` | `uniqueidentifier` | UNIQUE, NOT NULL, DEFAULT (newid()) |
| `Имя` | `nvarchar(100)` | NOT NULL |
| `Адрес` | `nvarchar(100)` | NULL |
| `Телефон` | `nvarchar(18)` | NULL |
| `СпециальностьId` | `int` | NOT NULL |

### `Специальность`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Название` | `nvarchar(50)` | UNIQUE, NOT NULL |

## ASP.NET Identity

Пользователи API хранятся в этой же базе. Схема создаётся EF-миграциями
`InitialIdentity` и `AddUserCreatedAt` и продублирована идемпотентным скриптом
`create-identity-tables.sql`, чтобы базу можно было поднять без .NET SDK.

Стандартные таблицы: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`,
`AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`,
`AspNetUserTokens`, `__EFMigrationsHistory`.

Отличие от стандартной схемы — обязательная связь учётки с сотрудником:

| Колонка `AspNetUsers` | Тип | Ограничения |
|-----------------------|-----|-------------|
| `СотрудникId` | `int` | NOT NULL, UNIQUE, FK → `Сотрудник.Id`, `ON DELETE NO ACTION` |
| `Фио` | `nvarchar(200)` | NULL — денормализованное ФИО для списков |
| `ДатаСоздания` | `datetime2` | NOT NULL, DEFAULT `SYSUTCDATETIME()` |

Правила:

- **Учётка без сотрудника невозможна** — `СотрудникId` обязателен.
- **Один сотрудник — одна учётка** — уникальный индекс
  `IX_AspNetUsers_СотрудникId`.
- **Сотрудника с учёткой нельзя удалить** — `ON DELETE NO ACTION`; сначала
  удаляется или переносится учётка.
- Таблица `Сотрудник` помечена `ExcludeFromMigrations()`: EF её не создаёт и
  не удаляет, потому что она уже есть в скриптах.

Применить схему:

```bash
# через EF (нужны User Secrets со строкой подключения HRDb)
cd SkladNew/backend
dotnet tool run dotnet-ef database update \
  --project SkladApi --startup-project SkladApi --context HRContext

# или чистым SQL
sqlcmd -S localhost -U sa -P '<пароль>' -C -f 65001 -b -i create-identity-tables.sql
```

## Данные

В эталоне `Sh_29-06-25` кадровых таблиц нет — это складской модуль, поэтому
значения заданы в `insert-reference-data.sql`:

| Таблица | Строк |
|---------|-------|
| `Специальность` | 6 (швея, обивщик, сборщик, упаковщик, мастер участка, кладовщик) |
| `Сотрудник` | 1 (демо, `Guid` = `C0FFEE00-0000-0000-0000-000000000001`) |

Демо-сотрудник нужен, чтобы в основной базе было что записать в `СотрудникGuid`
и проверить межбазовую связь.

## Запросы

**Сотрудники по специальности:**

```sql
SELECT с.Имя, сп.Название AS Специальность, с.[Guid]
FROM   dbo.Сотрудник AS с
JOIN   dbo.Специальность AS сп ON сп.Id = с.СпециальностьId
WHERE  сп.Название = N'Швея';
```

**Все операции сотрудника в основной базе** (JOIN через базы):

```sql
SELECT р.Date, р.НоменклатураId, р.Quantity, р.[Sum]
FROM   [Sklad_v1].dbo.Расход AS р
WHERE  р.СотрудникGuid = (SELECT [Guid] FROM dbo.Сотрудник WHERE Имя = N'Сидорова Мария Ивановна')
ORDER BY р.Date;
```

**Кто ещё не привязан** — сотрудники без операций (обычно новые):

```sql
SELECT с.Имя, с.[Guid]
FROM   dbo.Сотрудник AS с
WHERE  NOT EXISTS (SELECT 1 FROM [Sklad_v1].dbo.Расход WHERE СотрудникGuid = с.[Guid]);
```

## Проверено

- Полный прогон на SQL Server 2019 с нуля.
- `JOIN` между базами по `Guid` работает; `UNIQUE` на Guid отрабатывает.
- Основная база содержит `СотрудникGuid` в трёх таблицах и не содержит
  кадровых таблиц.
