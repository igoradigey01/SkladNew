# Схема БД `Sklad-v2` — версия 2

Складская часть системы. Кадры вынесены в отдельную базу
[`Sklad_v1_HR`](../sklad_v1_hr/database-schema.md), связь — через
`СотрудникGuid`. Движения и деньги — ядро на сквозном идентификаторе `UETR`.

## Что изменилось относительно `Sklad_v1`

Справочники приведены к 1С-стилю — имена стали такими же, как в 1С, чтобы
бухгалте и разработчикам не приходилось держать в голове два словаря:

| Было (`Sklad_v1`) | Стало (`Sklad-v2`) | Почему |
|--------------------|--------------------|--------|
| `Материал` | `ВидНоменклатуры` | как в 1С: вид номенклатуры |
| `АтрибутМатериала` | `Характеристика` | короче и точнее по смыслу |
| `Номенклатура` | `Номенклатура` | без изменений |
| `ХарактеристикаНоменклатуры` | `ЗначениеХарактеристики` | яснее: это именно значения, а не сами характеристики |

Переименовано всё, что связано: имена ограничений (`PK_ВидНоменклатуры`,
`FK_Номенклатура_ВидНоменклатуры`, …), FK-колонки (`МатериалId` →
`ВидНоменклатурыId`, `Атрибут` → `ХарактеристикаId`) и индексы.

Структура не менялась: те же 13 таблиц, 11 внешних ключей, те же данные.

## Файлы

| Файл | Что делает |
|------|------------|
| `create-database.sql` | Создаёт `Sklad-v2` |
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

## Справочники номенклатуры

```
Характеристика (Id, Название)          ВидНоменклатуры (Id, Название)
        │                                      │
        │ ЗначениеХарактеристики.ХарактеристикаId
        │                                      │ ВидНоменклатурыId
        └──────────────┬───────────────────────┘
                       ▼
               Номенклатура (Id, Название, ВидНоменклатурыId, ЕдИзмеренияId, Цена)
```

Смысл такой же, как в 1С: `ВидНоменклатуры` — «из чего сделано» (ДСП, фанера,
ткань), `Номенклатура` — конкретная позиция с ценой и единицей измерения,
`Характеристика` / `ЗначениеХарактеристики` — дополнительные свойства позиции
(в эталоне обе пустые).

## Почему без упаковки

Механизм второй единицы измерения из эталона не переносится: `колAliasЕдИм` = 0
во всех 6773 строках движений, таблица коэффициентов перевода пуста,
`ценаAliasЕдИм` дублировала цену в основной единице. Одна единица измерения на
позицию — `ЕдИзмерения`, отпуск ведётся в ней же.

## Связь с кадровой базой

`FOREIGN KEY` между базами невозможен, поэтому связь держится на трёх опорах:
`UQ_Сотрудник_Guid` в `Sklad_v1_HR`, валидация в приложении и сверка по расписанию.

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

**Сверка битых ссылок** — результат должен быть пустым:

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
принадлежит другой базе:

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

**Справочники (7):** `Склад`, `ЕдИзмерения`, `ВидНоменклатуры`, `Характеристика`,
`ЗначениеХарактеристики`, `Номенклатура`, `Клиент`, `Поставщик`.

**Ядро (3):** `Приход`, `Расход`, `Деньги`.

**Ревизии (2):** `Ревизия`, `Расхождение` (`Delta` — вычисляемая колонка).

## ER-диаграмма

Сгенерирована из развёрнутой БД: 13 таблиц, 11 связей.

```mermaid
erDiagram
    ВидНоменклатуры ||--o{ Номенклатура : "ВидНоменклатурыId"
    ЕдИзмерения ||--o{ Номенклатура : "ЕдИзмеренияId"
    Клиент ||--o{ Расход : "КлиентId"
    Номенклатура ||--o{ ЗначениеХарактеристики : "НоменклатураId"
    Номенклатура ||--o{ Приход : "НоменклатураId"
    Номенклатура ||--o{ Расход : "НоменклатураId"
    Номенклатура ||--o{ Расхождение : "НоменклатураId"
    Поставщик ||--o{ Приход : "ПоставщикId"
    Ревизия ||--o{ Расхождение : "КодРевизии"
    Склад ||--o{ Приход : "СкладId"
    Склад ||--o{ Расход : "СкладId"
    Характеристика ||--o{ ЗначениеХарактеристики : "ХарактеристикаId"
    ВидНоменклатуры {
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
    ЗначениеХарактеристики {
        int Id, PK
        int НоменклатураId, FK
        int ХарактеристикаId, FK
        nvarchar Значение
    }
    Клиент {
        int Id, PK
        nvarchar Псевдоним, UK
        nvarchar Фио
        nvarchar Телефон
        nvarchar Город
        decimal ПределКредита
    }
    Номенклатура {
        int Id, PK
        nvarchar Название
        int ВидНоменклатурыId, FK
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
    Характеристика {
        int Id, PK
        nvarchar Название, UK
    }
```

## Таблицы и колонки

### `ВидНоменклатуры`

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

### `ЗначениеХарактеристики`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `НоменклатураId` | `int` | NOT NULL |
| `ХарактеристикаId` | `int` | NOT NULL |
| `Значение` | `nvarchar(30)` | NOT NULL |

### `Клиент`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Псевдоним` | `nvarchar(25)` | UNIQUE, NOT NULL |
| `Фио` | `nvarchar(100)` | NOT NULL, DEFAULT (N'none') |
| `Телефон` | `nvarchar(18)` | NOT NULL |
| `Город` | `nvarchar(50)` | NOT NULL |
| `ПределКредита` | `decimal(18,2)` | NOT NULL, DEFAULT ((10000)) |

### `Номенклатура`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Название` | `nvarchar(100)` | NOT NULL |
| `ВидНоменклатурыId` | `int` | NOT NULL |
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

### `Характеристика`

| Колонка | Тип | Ограничения |
|---------|-----|-------------|
| `Id` | `int` | IDENTITY, PK, NOT NULL |
| `Название` | `nvarchar(50)` | UNIQUE, NOT NULL |
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
| `ВидНоменклатуры` | 15 | эталон `Матерьял` |
| `Клиент` | 53 | эталон `Клиент` |
| `Поставщик` | 9 | эталон `Поставщик` |
| `ЕдИзмерения` | 5 | эталон `ОсновнаяЕдИзмерения` |
| `Склад` | 2 | эталон `Скдад` |
| `Характеристика` / `ЗначениеХарактеристики` | 0 / 0 | эталон (пусто) |
| `Приход` | 469 (189 документов) | эталон `СкладПриход` + `НакладнаяПриход` |
| `Расход` | 6304 (2449 документов) | эталон `СкладРасход` + `НакладнаяРасход` |
| `Деньги` | 2134 | эталон `КлиентДеньги` |
| `Ревизия` / `Расхождение` | 5 / 758 | эталон `АудитМетка` / `РезультатАудита` |

Сверка с эталоном: приход 143 762,950; расход 155 580,160; деньги 68 518 859,40 —
значения совпали с `Sklad_v1` и с эталоном. `СотрудникGuid` в перенесённых данных
пуст: в эталоне исполнителей операций не было.

## Проверено

- Полный прогон на SQL Server 2019 с нуля; повторный запуск всех скриптов.
- Старых имён (`Материал`, `АтрибутМатериала`, `ХарактеристикаНоменклатуры`,
  `Упаковка`, `СвязиУпаковки`) в базе нет.
- FK `Номенклатура → ВидНоменклатуры` и `ЗначениеХарактеристики → Характеристика`
  на месте; перенос данных не сломался.
- Межбазовый `JOIN` по `Guid` работает; сверка битых ссылок возвращает 0.
