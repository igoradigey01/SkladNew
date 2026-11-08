# 2026-11-08 — разбор накладной голосом

## Что появилось

| Компонент | Назначение |
|---|---|
| `Controllers/InvoiceController.cs` | `POST /api/invoice/parse-voice`: текст → позиции |
| `Services/OllamaSettings.cs` | адрес и модель Ollama (`http://10.42.0.100:11434`, `qwen2.5:7b-instruct`) |
| `Services/FuzzyMatcher.cs` | Левенштейн + проверка чисел |
| `Repository/НоменклатураRepository.cs` | активная номенклатура из `dbo.Номенклатура` |
| `Controllers/NomenclatureController.cs` | `GET /api/nomenclature?q=&limit=` |
| `SkladApi.Tests` | 10 тестов: FuzzyMatcher и InvoiceController с fake HttpClient |

## Решения

- `gemma2b-invoice` не установлена — используем `qwen2.5:7b-instruct`;
- JSON-схема ответа (`{"items":[…]}`) вместо `format: "json"`: иначе модель возвращала
  одну позицию;
- few-shot примеры в промте: без них модель принимала размер за количество;
- порог сопоставления 75%, плюс отбрасывание кандидатов с несовпадающими числами;
- 503 при недоступной БД вместо стектрейса.

## Проверки

```
dotnet test SkladApi.Tests → 10 passed
POST /api/invoice/parse-voice на живой БД — совпадения 82–100%,
несуществующие позиции → «НЕ НАЙДЕНО В БД»
```

Живые примеры: «шайба цинк 10, 100 штук» → `Шайба Цинк_10`, conf 100;
«саморез 4.2х75, 100 штук» → `Саморезы_4,2 х 75`, conf 82;
«гвозди 2,5х50» → «НЕ НАЙДЕНО В БД» (такого размера нет).
