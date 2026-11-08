#!/usr/bin/env python3
"""Бенчмарк разбора накладной: сравнение моделей Ollama на наборе фраз.

Запуск:  python3 scripts/bench-voice.py [--models m1,m2] [--json out.json]
Требует доступный Ollama (по умолчанию http://10.42.0.100:11434).
"""

import argparse
import json
import statistics
import sys
import time
import urllib.request

BASE = "http://10.42.0.100:11434"
DEFAULT_MODELS = ["qwen2.5:7b-instruct", "qwen2.5:14b-instruct", "gemma4:e2b"]

SCHEMA = {
    "type": "object",
    "properties": {
        "items": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "raw_name": {"type": "string"},
                    "quantity": {"type": "number"},
                    "unit": {"type": "string"},
                },
                "required": ["raw_name", "quantity", "unit"],
            },
        }
    },
    "required": ["items"],
}

HEAD = (
    "Разбери текст накладной на позиции.\n"
    "raw_name — название товара ВМЕСТЕ с размером и маркой.\n"
    "quantity — количество: число перед единицей измерения.\n"
    "unit — единица измерения как в тексте, иначе пустая строка.\n"
    "ВАЖНО: размеры и марки — часть названия, а не количество.\n"
    "Не придумывай товары, которых нет в тексте.\n\n"
    "Пример. Текст: краска белая, 5 литров\n"
    'Ответ: {"items":[{"raw_name":"краска белая","quantity":5,"unit":"литров"}]}\n\n'
)

# text, expected: (name, quantity, unit); unit="" — любая единица допустима
CASES = [
    ("шайба цинк 10, 100 штук", [("шайба цинк 10", 100, "штук")]),
    ("саморезы 3,5 на 25, 200 штук", [("саморезы 3,5 на 25", 200, "штук")]),
    ("гайка кошка 10, 50 штук", [("гайка кошка 10", 50, "штук")]),
    ("гвозди 1,6 х 25, 5 кг", [("гвозди 1,6 х 25", 5, "кг")]),
    ("шайба чрн 6, 500 штук", [("шайба чрн 6", 500, "штук")]),
    ("саморез 4.2х75, 100 штук", [("саморез 4.2х75", 100, "штук")]),
    ("гвозди 2,5х50, 3 кг", [("гвозди 2,5х50", 3, "кг")]),
    ("два мешка шпатлевки и полтора килограмма гвоздей",
     [("шпатлевка", 2, "мешок"), ("гвозди", 1.5, "кг")]),
    ("запиши 5... ой нет, давай 10 мешков цемента", [("цемент", 10, "мешок")]),
    ("шайба 10 100 штук и гвозди 2,5х50 три кило",
     [("шайба 10", 100, "штук"), ("гвозди 2,5х50", 3, "кг")]),
    ("саморезы 3.5х25 200 шт и саморезы 4.2х75 100 шт",
     [("саморезы 3.5х25", 200, "шт"), ("саморезы 4.2х75", 100, "шт")]),
    ("гайка цинк 8, 30 штук; шайба цинк 8, 40 штук",
     [("гайка цинк 8", 30, "штук"), ("шайба цинк 8", 40, "штук")]),
    ("гвозди 1,8 х 32 — 2,5 кг", [("гвозди 1,8 х 32", 2.5, "кг")]),
    ("болт мебельный черный 6 на 45, 100 штук",
     [("болт мебельный черный 6 на 45", 100, "штук")]),
    ("шайба 10, шайба 8, гайка 10",
     [("шайба 10", 1, ""), ("шайба 8", 1, ""), ("гайка 10", 1, "")]),
    ("Цемент М-500 20 мешков, гвозди 100 мм 5 кг, правило 2 м 1 шт",
     [("цемент М-500", 20, "мешок"), ("гвозди 100 мм", 5, "кг"), ("правило", 1, "шт")]),
    ("привези двадцать мешков цемента и сто штук гвоздей 1,6х25",
     [("цемент", 20, "мешок"), ("гвозди 1,6х25", 100, "шт")]),
    ("саморез 3,5 на 25 — двести штук", [("саморез 3,5 на 25", 200, "шт")]),
]


def norm(value):
    value = (value or "").lower().replace("ё", "е")
    value = "".join(c if (c.isalnum() or c == " ") else " " for c in value)
    return " ".join(value.split())


def unit_stem(unit):
    u = norm(unit)
    if not u:
        return ""
    if u.startswith(("шт", "штук")):
        return "шт"
    if u.startswith(("кг", "кило", "килограмм")):
        return "кг"
    if u.startswith(("меш", "пачк", "пач")):
        return "меш"
    if u.startswith(("метр", "м")):
        return "м"
    if u.startswith(("литр", "л")):
        return "л"
    return u


def name_ok(expected, produced):
    exp_tokens = [t for t in norm(expected).split() if len(t) >= 4]
    prod = norm(produced)
    if not exp_tokens:
        return norm(expected) in prod
    return all(any(t[:5] == p[:5] for p in prod.split() if len(p) >= 3) for t in exp_tokens)


def score(expected, produced):
    """Сколько ожидаемых позиций найдено и сколько лишних вернула модель."""
    used = [False] * len(produced)
    matched = 0

    for name, qty, unit in expected:
        for idx, item in enumerate(produced):
            if used[idx]:
                continue
            if abs(float(item.get("quantity", 0)) - qty) > 0.011:
                continue
            if unit and unit_stem(item.get("unit", "")) != unit_stem(unit):
                continue
            if not name_ok(name, item.get("raw_name", "")):
                continue
            used[idx] = True
            matched += 1
            break

    return matched, sum(1 for u in used if not u)


def call(model, text):
    body = {
        "model": model,
        "prompt": HEAD + "Текст: " + text,
        "stream": False,
        "format": SCHEMA,
        "options": {"temperature": 0},
    }
    if model.startswith("gemma4"):
        body["think"] = False  # иначе модель тратит время на блок рассуждений

    request = urllib.request.Request(
        BASE + "/api/generate",
        json.dumps(body).encode(),
        {"Content-Type": "application/json"},
    )
    started = time.time()
    with urllib.request.urlopen(request, timeout=300) as response:
        data = json.load(response)
    elapsed = time.time() - started

    try:
        return json.loads(data["response"])["items"], elapsed
    except (KeyError, json.JSONDecodeError) as error:
        raise RuntimeError(f"невалидный ответ модели: {data.get('response', '')[:120]!r}") from error


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--models", default=",".join(DEFAULT_MODELS))
    parser.add_argument("--json", default="")
    args = parser.parse_args()

    models = [m.strip() for m in args.models.split(",") if m.strip()]
    results = {}

    for model in models:
        total = correct = extras = 0
        times, failures = [], 0

        for text, expected in CASES:
            total += len(expected)
            try:
                items, elapsed = call(model, text)
                good, extra = score(expected, items)
                correct += good
                extras += extra
                times.append(elapsed)
            except Exception as error:  # noqa: BLE001 - бенчмарк не должен падать целиком
                failures += 1
                print(f"  ! {model}: {error}", file=sys.stderr)

        results[model] = {
            "correct": correct,
            "total": total,
            "extra": extras,
            "failures": failures,
            "avg_seconds": round(statistics.mean(times), 2) if times else None,
            "max_seconds": round(max(times), 2) if times else None,
        }
        r = results[model]
        print(f"{model:24} {r['correct']}/{r['total']} позиций, "
              f"лишних {r['extra']}, ошибок {r['failures']}, "
              f"среднее {r['avg_seconds']} c, макс {r['max_seconds']} c")

    if args.json:
        with open(args.json, "w", encoding="utf-8") as file:
            json.dump(results, file, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
