# План на завтра: Оптимизация Flint 40B и запуск API

> **Статус:** план. Результат не зафиксирован; рабочие модели — Qwen 2.5 Coder 14B и Flint Local.

## Цель
Настроить сервер `ks34` для максимально быстрой работы с Flint 40B (насколько это возможно на 11 ГБ VRAM), используя `Modelfile`, `OLLAMA_KEEP_ALIVE` и `numactl`.

## Этап 1: Оптимизация Ollama (на сервере `ks34`)

### Шаг 1.1. Настройка удержания модели в памяти
1. Открой настройки службы: `sudo systemctl edit ollama`
2. Добавь в блок `[Service]`:
   ```ini
   Environment="OLLAMA_KEEP_ALIVE=-1"
   ```
3. Примени: `sudo systemctl daemon-reload && sudo systemctl restart ollama`

**Зачем:** Чтобы Flint не выгружался из памяти через 5 минут простоя и не загружался заново (это экономит минуты ожидания).

### Шаг 1.2. Оптимизация NUMA (для CPU-вычислений)
1. Проверь путь к ollama: `which ollama` (обычно `/usr/local/bin/ollama`)
2. Снова открой `sudo systemctl edit ollama`
3. Измени строку `ExecStart`:
   ```ini
   ExecStart=/usr/bin/numactl --interleave=all /usr/local/bin/ollama serve
   ```
   *(Если `numactl` не установлен, сначала выполни `sudo apt install numactl`).*
4. Примени: `sudo systemctl daemon-reload && sudo systemctl restart ollama`

**Зачем:** `numactl` заставляет систему равномерно распределять память между процессорами, убирая «тормоза» при передаче данных.

## Этап 2: Создание кастомной модели (Modelfile)

### Шаг 2.1. Создай `Modelfile.flint`
В папке `/mnt/ollama-disk/models/flint/` создай файл:
```dockerfile
FROM hf.co/unsloth/flint-GGUF:UD-Q4_K_XL

# Контекст 16k (безопасно для 11 ГБ VRAM)
PARAMETER num_ctx 16384

# Потоки CPU (у тебя 16, ставим 14)
PARAMETER num_thread 14

# Настройки для кодинга (низкая температура)
PARAMETER temperature 0.2
PARAMETER top_p 0.95
PARAMETER num_predict -1
```

### Шаг 2.2. Собери модель
```bash
cd /mnt/ollama-disk/models/flint/
ollama create flint-server -f Modelfile.flint
```
Проверь: `ollama list` (должна появиться `flint-server`).

## Этап 3: Запуск и проверка

### Шаг 3.1. Запусти `toolrails` (если еще не запущен)
```bash
source $HOME/.local/bin/env
uvx toolrails --ollama http://localhost:11434 --host 0.0.0.0
```
*Оставь терминал открытым.*

### Шаг 3.2. Настрой OpenCode на `ks33`
В `~/.config/opencode/opencode.json` добавь модель `flint-server`:
```json
"flint-server": {
  "name": "Flint Server (Custom)",
  "tools": true
}
```
*(В строке `"model"` поставь `flint-server` или `qwen2.5-coder:14b` в зависимости от задачи).*

### Шаг 3.3. Проверка скорости
1. Запусти OpenCode.
2. Выбери `Flint Server (Custom)`.
3. Дай задачу: «Создай файл TestSpeed.txt с текстом Hello».
4. Замерь время ответа (должно быть быстрее, чем вчера).

## Этап 4: Возврат к задаче 09 (API)

Если Flint покажется слишком медленным даже после оптимизаций:
1. Переключись на **Qwen 2.5 Coder 14B** (она быстрее).
2. Продолжи создание API для клиентов (DTO, репозитории, контроллер).

## Ограничения
- ❌ Не ставить `num_gpu 20` (это переполнит VRAM, Flint все равно не влезет).
- ✅ `OLLAMA_KEEP_ALIVE=-1` и `numactl` — обязательны для Flint.
- ✅ Для Qwen 14B эти настройки не критичны (она работает на GPU).