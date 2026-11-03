# Задача: Подключить OpenCode к удалённому серверу Ollama

## Контекст
- **Сервер (Ollama):** `ks34` (IP: `10.42.0.100`), модели лежат в `/mnt/ollama-disk/models`.
- **Клиент (OpenCode):** `ks33`, здесь запускается агент.
- **Прокси (toolrails):** Запущен на сервере `ks34` на порту `11500`, слушает `0.0.0.0`.
- **Цель:** Настроить OpenCode на клиенте так, чтобы он использовал модели с сервера через прокси.

## Архитектура
```
[Клиент ks33] OpenCode ──HTTP──> [Сервер ks34] toolrails (порт 11500) ──> Ollama (порт 11434)
```

## Развертывание на сервере (ks34)

### 1. Установка Ollama
Если Ollama еще не установлена, выполни:
```bash
curl -fsSL https://ollama.com/install.sh | sh
```

### 2. Настройка пути к моделям и доступа по сети
Открой настройки службы:
```bash
sudo systemctl edit ollama
```
Добавь строки (если их нет):
```ini
[Service]
Environment="OLLAMA_MODELS=/mnt/ollama-disk/models"
Environment="OLLAMA_HOST=0.0.0.0:11434"
```
Примени:
```bash
sudo systemctl daemon-reload
sudo systemctl restart ollama
```

### 3. Скачивание модели (пример)
```bash
ollama pull qwen2.5-coder:14b
```
*(Или скачай через Hugging Face, как мы делали ранее).*

### 4. Установка и запуск `toolrails`
Установи `uv` (если его нет):
```bash
curl -LsSf https://astral.sh/uv/install.sh | sh
source $HOME/.local/bin/env
```
Запусти прокси (в отдельном терминале, не закрывай его):
```bash
uvx toolrails --ollama http://localhost:11434 --host 0.0.0.0
```
*(Если нужно, чтобы он работал в фоне после закрытия терминала: `nohup uvx toolrails --ollama http://localhost:11434 --host 0.0.0.0 > /tmp/toolrails.log 2>&1 &`).*

## Настройка клиента (ks33)

### 1. Конфигурация OpenCode
Создай или обнови файл `~/.config/opencode/opencode.json`.
Используй провайдер `@ai-sdk/openai-compatible`. В качестве `baseURL` укажи **IP сервера** и **порт прокси** (`11500`), с обязательным суффиксом `/v1`.

**Пример конфига:**
```json
{
  "$schema": "https://opencode.ai/config.json",
  "provider": {
    "ollama-remote": {
      "npm": "@ai-sdk/openai-compatible",
      "name": "Ollama (Remote ks34)",
      "options": {
        "baseURL": "http://10.42.0.100:11500/v1"
      },
      "models": {
        "hf.co/bartowski/Qwen2.5-Coder-14B-Instruct-GGUF:Q4_K_M": {
          "name": "Qwen 2.5 Coder 14B (Remote)",
          "tools": true
        }
      }
    }
  },
  "model": "ollama-remote/hf.co/bartowski/Qwen2.5-Coder-14B-Instruct-GGUF:Q4_K_M"
}
```

### 2. Проверка связи
Перед запуском OpenCode проверь, что прокси доступен:
```bash
curl http://10.42.0.100:11500/v1/models
```
В ответе должен быть JSON со списком моделей (Qwen, Flint и т.д.).

## Ограничения
- ❌ Не указывать в `baseURL` адрес `127.0.0.1` (это локальный адрес сервера, с клиента он недоступен).
- ❌ Не использовать порт `11434` напрямую (он слушает только локально на сервере, если не настроен `OLLAMA_HOST`).
- ✅ Всегда использовать порт `11500` (прокси `toolrails`) для подключения с других машин.
- ✅ Убедиться, что на сервере `toolrails` запущен с флагом `--host 0.0.0.0`.

## Проверка
1. Запустить OpenCode на `ks33`.
2. Ввести `/model` и выбрать `Qwen 2.5 Coder 14B (Remote)`.
3. Дать простую задачу (например, «Создай файл Test.txt»).
4. Убедиться, что файл создался, а в логах `toolrails` на `ks34` появились записи о вызове инструмента.