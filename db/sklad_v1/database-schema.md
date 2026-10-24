# Схема БД `Sklad_v1` — основная (склад)

Складская часть системы. Движения и деньги — собственное ядро на сквозном
идентификаторе `UETR`; справочники — из эталона `Sh_29-06-25`.

**Кадров в этой базе нет:** `Сотрудник` и `Специальность` вынесены в отдельную
базу [`Sklad_v1_HR`](../../sklad_v1_hr/database-schema.md). Связь — через
`СотрудникGuid` (uniqueidentifier) в `Приход`, `Расход` и `Деньги`.

```
┌──────────────────────────────────────────────┐
│              Sklad_v1 (основная)             │
├──────────────────────────────────────────────┤
│ Приход                                       │
│   ├── Id (int)                               │
│   ├── UETR (uniqueidentifier)                │
│   ├── СотрудникGuid (uniqueidentifier) ──────┼──┐
│ Расход                                       │  │
│   ├── СотрудникGuid (uniqueidentifier) ──────┼──┤
│ Деньги                                       │  │
│   └── СотрудникGuid (uniqueidentifier) ──────┼──┤
│ Клиент, Поставщик, Номенклатура, Материал,   │  │
│ ЕдИзмерения, Склад,                          │  │
│ АтрибутМатериала, ХарактеристикаНоменклатуры,│  │
│ Ревизия, Расхождение                         │  │
└──────────────────────────────────────────────┘  │
                    Связь через Guid              ↓
┌──────────────────────────────────────────────┐
│            Sklad_v1_HR (кадры)               │
├──────────────────────────────────────────────┤
│ Сотрудник: Id (PK), Guid (UNIQUE), … ─────────┘
│ Специальность: Id (PK), Название            │
└──────────────────────────────────────────────┘
```

## Файлы

| Файл | Что делает |
|------|------------|
| `create-database.sql` | Создаёт `Sklad_v1` |
| `create-tables.sql` | 13 таблиц: справочники, ядро, ревизии |
| `create-indexes.sql` | 19 индексов (UETR, даты, FK, `СотрудникGuid`) |
| `insert-reference-data.sql` | Справочники данными эталона (Id сохраняются) |
| `migrate-movements-from-Sh_29-06-25.sql` | Движения, деньги, ревизии с построением `UETR` |

```bash
sqlcmd -S localhost -E -f 65001 -b -i create-database.sql
sqlcmd -S localhost -E -f 65001 -b -i create-tables.sql
sqlcmd -S localhost -E -f 65001 -b -i create-indexes.sql
sqlcmd -S localhost -E -f 65001 -b -i insert-reference-data.sql
sqlcmd -S localhost -E -f 65001 -b -i migrate-movements-from-Sh_29-06-25.sql
```

## Почему без упаковки

Механизм второй единицы измерения из эталона не переносится. Проверено на данных:
`колAliasЕдИм` равна нулю во **всех 6773** строках движений, таблица
коэффициентов перевода `СвязиAlisЕдИмНоменклатура` пуста, а `ценаAliasЕдИм`
дублировала цену в основной единице. То есть механизм был заведён, но не работал.

Убраны: таблицы `Упаковка` и `СвязиУпаковки`, поля `УпаковкаId`, `Коэффициент`,
`QuantityPack`, `PricePack`, `ТараРасходаId` в `Приход` и `Расход`, три индекса и
проверка `CK_*_Упаковка`. Осталась одна единица измерения на позицию —
`ЕдИзмерения`, отпуск ведётся в ней же.

Если упаковка понадобится (продажа рулонами при учёте в метрах), это отдельная
задача: справочник упаковок + коэффициенты перевода + правила округления. Пустой
справочник без коэффициентов только мешает.

## Связь с кадровой базой

`FOREIGN KEY` между базами невозможен, поэтому связь держится на трёх опорах:
`UQ_Сотрудник_Guid` в `Sklad_v1_HR`, валидация в приложении и периодическая
сверка.

**Отчёт «кто что отгрузил»:**

```sql
SELECT с.Имя,
       сп.Название AS Специальность,
       COUNT(*)     AS СтрокОтгрузки,
       SUM(р.[Sum]) AS Сумма
FROM   dbo.Расход AS р
JOIN   [Sklad_v1_HR].dbo.Сотрудник] AS с ON с.[Guid] = р.СотрудникGuid
LEFT JOIN [Sklad_v1_HR].dbo.Специальность] AS сп ON сп.Id = с.СпециальностьId
WHERE  р.[Date] >= @from AND р.[Date] < @to
GROUP BY с.Имя, сп.Название;
```

**Сверка битых ссылок** — по расписанию, результат должен быть пустым:

```sql
SELECT т.Таблица, т.Id, т.СотрудникGuid
FROM (
    SELECT N'Приход' AS Таблица, Id, СотрудникGuid FROM dbo.Приход WHERE СотрудникGuid IS NOT NULL
    UNION ALL SELECT N'Расход',  Id, СотрудникGuid FROM dbo.Расход  WHERE СотрудникGuid IS NOT NULL
    UNION ALL SELECT N'Деньги',  Id, СотрудникGuid FROM dbo.Деньги  WHERE СотрудникGuid IS NOT NULL) AS т
WHERE NOT EXISTS (SELECT 1 FROM [Sklad_v1_HR].dbo.Сотрудник] AS с WHERE с.[Guid] = т.СотрудникGuid);
```

**Зарплата сотруднику**: `OperationType = 3`, `CounterpartyType = 3`,
`СотрудникGuid` заполнен, `CounterpartyId` пуст — идентификатор сотрудника
принадлежит другой базе. Это закреплено проверкой:

```sql
CONSTRAINT CK_Деньги_Контрагент CHECK (
    (CounterpartyType = 0 AND CounterpartyId IS NULL)
 OR (CounterpartyType IN (1, 2) AND CounterpartyId IS NOT NULL)
 OR (CounterpartyType = 3 AND СотрудникGuid IS NOT NULL))
```

## Про UETR

`UETR` — идентификатор **документа**, а не строки: в накладной несколько строк,
поэтому индекс по `UETR` намеренно **не уникальный**. Один `UETR` связывает
`Приход` + `Расход` + `Деньги`. При переносе данных `UETR` детерминирован:
`A…` — накладная прихода (189 документов), `B…` — накладная расхода (2449),
`C…` — движение денег.

## Состав

**Справочники (7):** `Склад`, `ЕдИзмерения`, `Материал`, `АтрибутМатериала`,
`Номенклатура`, `ХарактеристикаНоменклатуры`, `Клиент`, `Поставщик`.

**Ядро (3):** `Приход`, `Расход`, `Деньги`.

**Ревизии (2):** `Ревизия`, `Расхождение` (`Delta` — вычисляемая колонка,
в базу не пишется).

## ER-диаграмма

Сгенерирована из развёрнутой БД: 13 таблиц, 11 связей.

```mermaid
erDiagram
    ЕдИзмерения ||--o{ Номенклатура : "ЕдИзмеренияId"
    Клиент ||--o{ Расход : "КлиентId"
    Материал ||--o{ Номенклатура : "МатериалId"
    Номенклатура ||--o{ Приход : "НоменклатураId"
    Номенклатура ||--o{ Расход : "НоменклатураId"
    Номенклатура ||--o{ Расхождение : "НоменклатураId"
    Номенклатура ||--o{ ХарактеристикаНоменклатуры : "НоменклатураId"
    Поставщик ||--o{ Приход : "ПоставщикId"
    Ревизия ||--o{ Расхождение : "КодРевизии"
    Склад ||--o{ Приход : "СкладId"
    Склад ||--o{ Расход : "СкладId"
    АтрибутМатериала {
        int Id, PK
        nvarchar Название, UK
    }
    Деньги {
        int Id, PK
        uniqueidentifier UETR
        datetime Date
        int OperationType
        int CounterpartyId
        int CounterpartyType
        decimal Sum
        int PaymentMethod
        uniqueidentifier СотрудникGuid
        nvarchar Comment
    }
    ЕдИзмерения {
        int Id, PK
        nvarchar Название, UK
    }
    Клиент {
        int Id, PK
        nvarchar Псевдоним, UK
        nvarchar Фио
        nvarchar Телефон
        nvarchar Город
        decimal ПределКредита
    }
    Материал {
        int Id, PK
        nvarchar Название, UK
    }
    Номенклатура {
        int Id, PK
        nvarchar Название
        int МатериалId, FK
        int ЕдИзмеренияId, FK
        decimal Цена
    }
    Поставщик {
        int Id, PK
        nvarchar Название, UK
        nvarchar Город
        nvarchar Телефон
    }
    Приход {
        int Id, PK
        uniqueidentifier UETR
        datetime Date
        int Type
        int ПоставщикId, FK
        int НоменклатураId, FK
        int СкладId, FK
        decimal Quantity
        decimal Price
        decimal Sum
        uniqueidentifier СотрудникGuid
        nvarchar Comment
    }
    Расход {
        int Id, PK
        uniqueidentifier UETR
        datetime Date
        int Type
        int КлиентId, FK
        int НоменклатураId, FK
        int СкладId, FK
        decimal Quantity
        decimal Price
        decimal Sum
        uniqueidentifier СотрудникGuid
        nvarchar Comment
    }
    Расхождение {
        int Id, PK
        nvarchar КодРевизии, FK
        int Type
        int НоменклатураId, FK
        decimal AccountingValue
        decimal ActualValue
        decimal Delta
        nvarchar Comment
    }
    Ревизия {
        nvarchar Code, PK
        int Type
        date DateStart
        date DateEnd
        int Status
        datetime DateCreated
    }
    Склад {
        int Id, PK
        nvarchar Название, UK
        nvarchar Псевдоним, UK
    }
    ХарактеристикаНоменклатуры {
        int Id, PK
        int НоменклатураId, FK
        nvarchar Атрибут
        nvarchar Значение
    }
```

## Таблицы и колонки

### `АтрибутМатериала`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Название` | `nvarchar(50)` | UNIQUE, NOT NULL |

### `Деньги`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `UETR` | `uniqueidentifier` | NOT NULL |
| `Date` | `datetime` | NOT NULL |
| `OperationType` | `int` | NOT NULL |
| `CounterpartyId` | `int` | NULL |
| `CounterpartyType` | `int` | NOT NULL, DEFAULT ((0)) |
| `Sum` | `decimal(18,2)` | NOT NULL |
| `PaymentMethod` | `int` | NOT NULL, DEFAULT ((1)) |
| `СотрудникGuid` | `uniqueidentifier` | NULL |
| `Comment` | `nvarchar(500)` | NULL |

### `ЕдИзмерения`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Название` | `nvarchar(10)` | UNIQUE, NOT NULL |

### `Клиент`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Псевдоним` | `nvarchar(25)` | UNIQUE, NOT NULL |
| `Фио` | `nvarchar(100)` | NOT NULL, DEFAULT (N'none') |
| `Телефон` | `nvarchar(18)` | NOT NULL |
| `Город` | `nvarchar(50)` | NOT NULL |
| `ПределКредита` | `decimal(18,2)` | NOT NULL, DEFAULT ((10000)) |

### `Материал`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Название` | `nvarchar(50)` | UNIQUE, NOT NULL |

### `Номенклатура`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Название` | `nvarchar(100)` | NOT NULL |
| `МатериалId` | `int` | NOT NULL |
| `ЕдИзмеренияId` | `int` | NOT NULL |
| `Цена` | `decimal(18,2)` | NOT NULL, DEFAULT ((0)) |

### `Поставщик`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Название` | `nvarchar(50)` | UNIQUE, NOT NULL |
| `Город` | `nvarchar(50)` | NULL |
| `Телефон` | `nvarchar(18)` | NULL |

### `Приход`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `UETR` | `uniqueidentifier` | NOT NULL |
| `Date` | `datetime` | NOT NULL |
| `Type` | `int` | NOT NULL |
| `ПоставщикId` | `int` | NOT NULL |
| `НоменклатураId` | `int` | NOT NULL |
| `СкладId` | `int` | NOT NULL |
| `Quantity` | `decimal(18,3)` | NOT NULL |
| `Price` | `decimal(18,2)` | NOT NULL |
| `Sum` | `decimal(18,2)` | NOT NULL |
| `СотрудникGuid` | `uniqueidentifier` | NULL |
| `Comment` | `nvarchar(500)` | NULL |

### `Расход`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `UETR` | `uniqueidentifier` | NOT NULL |
| `Date` | `datetime` | NOT NULL |
| `Type` | `int` | NOT NULL |
| `КлиентId` | `int` | NOT NULL |
| `НоменклатураId` | `int` | NOT NULL |
| `СкладId` | `int` | NOT NULL |
| `Quantity` | `decimal(18,3)` | NOT NULL |
| `Price` | `decimal(18,2)` | NOT NULL |
| `Sum` | `decimal(18,2)` | NOT NULL |
| `СотрудникGuid` | `uniqueidentifier` | NULL |
| `Comment` | `nvarchar(500)` | NULL |

### `Расхождение`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `КодРевизии` | `nvarchar(30)` | NOT NULL |
| `Type` | `int` | NOT NULL |
| `НоменклатураId` | `int` | NULL |
| `AccountingValue` | `decimal(18,3)` | NOT NULL |
| `ActualValue` | `decimal(18,3)` | NOT NULL |
| `Delta` | `decimal(19,3)` | NULL |
| `Comment` | `nvarchar(500)` | NULL |

### `Ревизия`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Code` | `nvarchar(30)` | PK, NOT NULL |
| `Type` | `int` | NOT NULL |
| `DateStart` | `date` | NOT NULL |
| `DateEnd` | `date` | NOT NULL |
| `Status` | `int` | NOT NULL |
| `DateCreated` | `datetime` | NOT NULL, DEFAULT (getdate()) |

### `Склад`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Название` | `nvarchar(50)` | UNIQUE, NOT NULL |
| `Псевдоним` | `nvarchar(10)` | UNIQUE, NOT NULL |

### `ХарактеристикаНоменклатуры`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `НоменклатураId` | `int` | NOT NULL |
| `Атрибут` | `nvarchar(30)` | NOT NULL |
| `Значение` | `nvarchar(30)` | NOT NULL |
## Ключевые запросы

**Остаток по номенклатуре и складу:**

```sql
SELECT  н.[Id],
        н.[Название],
        ISNULL(п.Приход, 0) - ISNULL(р.Расход, 0) AS Остаток
FROM    dbo.Номенклатура AS н
LEFT JOIN (SELECT НоменклатураId, СкладId, SUM(Quantity) AS Приход
           FROM dbo.Приход GROUP BY НоменклатураId, СкладId) AS п
       ON п.НоменклатураId = н.[Id] AND п.СкладId = @СкладId
LEFT JOIN (SELECT НоменклатураId, СкладId, SUM(Quantity) AS Расход
           FROM dbo.Расход GROUP BY НоменклатураId, СкладId) AS р
       ON р.НоменклатураId = н.[Id] AND р.СкладId = @СкладId;
```

**Вся операция по сквозному идентификатору:**

```sql
DECLARE @u uniqueidentifier = 'A0000000-0000-0000-0000-000000000003';

SELECT N'Приход' AS [Таблица], Date, НоменклатураId, Quantity, Price, Sum FROM dbo.Приход WHERE UETR = @u
UNION ALL SELECT N'Расход',  Date, НоменклатураId, Quantity, Price, Sum FROM dbo.Расход  WHERE UETR = @u
UNION ALL SELECT N'Деньги',  Date, NULL,           NULL,      NULL, Sum FROM dbo.Деньги  WHERE UETR = @u;
```

**Долг клиента:**

```sql
SELECT  к.[Псевдоним], к.[Фио], к.[ПределКредита],
        ISNULL(SUM(CASE WHEN д.[OperationType] = 1 THEN д.[Sum] ELSE -д.[Sum] END), 0) AS Долг
FROM    dbo.Клиент AS к
LEFT JOIN dbo.Деньги AS д ON д.[CounterpartyType] = 1 AND д.[CounterpartyId] = к.[Id]
GROUP BY к.[Псевдоним], к.[Фио], к.[ПределКредита];
```

**Итог ревизии:**

```sql
SELECT р.Code, р.Type, р.DateStart, р.DateEnd, р.Status,
       COUNT(х.Id) AS Расхождений, SUM(х.Delta) AS СуммаДельты
FROM   dbo.Ревизия AS р
LEFT JOIN dbo.Расхождение AS х ON х.[КодРевизии] = р.Code
GROUP BY р.Code, р.Type, р.DateStart, р.DateEnd, р.Status;
```

## Данные

| Таблица | Строк | Источник |
|---------|-------|----------|
| `Номенклатура` | 202 | эталон `Номенклатура` |
| `Клиент` | 53 | эталон `Клиент` |
| `Материал` | 15 | эталон `Матерьял` |
| `Поставщик` | 9 | эталон `Поставщик` |
| `ЕдИзмерения` | 5 | эталон `ОсновнаяЕдИзмерения` |
| `Склад` | 2 | эталон `Скдад` |
| `Приход` | 469 (189 документов) | эталон `СкладПриход` + `НакладнаяПриход` |
| `Расход` | 6304 (2449 документов) | эталон `СкладРасход` + `НакладнаяРасход` |
| `Деньги` | 2134 | эталон `КлиентДеньги` |
| `Ревизия` / `Расхождение` | 5 / 758 | эталон `АудитМетка` / `РезультатАудита` |

Сверка с эталоном: приход 143 762,950; расход 155 580,160; деньги 68 518 859,40 —
значения совпали. `СотрудникGuid` в перенесённых данных пуст: в эталоне
исполнителей операций не было, поле заполняется при работе в новой системе.

## Проверено

- Полный прогон на SQL Server 2019 с нуля; повторный запуск всех скриптов.
- Таблиц `Упаковка` и `СвязиУпаковки` в базе нет, упаковочных колонок — 0.
- Межбазовый `JOIN` по `Guid` работает; сверка битых ссылок возвращает 0.
- `CK_Деньги_Контрагент` принимает выплату сотруднику по `СотрудникGuid`.
