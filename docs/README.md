# Документация SkladNew (backend + БД)

Задачи, журнал и шпаргалки по складу. Код — рядом, в `SkladX01/`; схема БД — `db/sklad_v4_1/`.

## Структура

| Каталог | Что внутри | Статус |
|---|---|---|
| [`help/`](help/) | `architecture.md` — проверка связки API + фронт; `Flint 40B help.md` — работа с локальной моделью | актуально |
| [`journal/`](journal/) | записи по датам: что сделано и почему | актуально |
| [`prompts/`](prompts/) | 01–03 — варианты схемы БД (эталон, гибрид, внешние стандарты) | архив: реализован `db/sklad_v4_1` |
| [`tasks/`](tasks/) | задачи по backend и инфраструктуре | см. таблицу ниже |

`help/deploy.md` существует локально, но **исключён из git** (в нём адрес сервера и команды
доступа) — в репозиторий не попадёт, при переносе машины его нужно восстановить вручную.

## Задачи

| Файл | Тема | Статус |
|---|---|---|
| [`01-identity-setup.md`](tasks/01-identity-setup.md) | Identity и аудит | выполнено |
| [`02-audit-trail-implementation.md`](tasks/02-audit-trail-implementation.md) | Audit Trail в коде | выполнено |
| [`03-backend-structure.md`](tasks/03-backend-structure.md) | структура Solution | выполнено |
| [`04-create-email-service.md`](tasks/04-create-email-service.md) | EmailService | выполнено |
| [`06-create-hrdb-project.md`](tasks/06-create-hrdb-project.md) | проект HRDB | выполнено |
| [`07-rename-to-skladx01.md`](tasks/07-rename-to-skladx01.md) | переименование в SkladX01 | выполнено |
| [`09-create-clients-api.md`](tasks/09-create-clients-api.md) | API клиентов | выполнено, позже расширено |
| [`11-connect-remote-ollama.md`](tasks/11-connect-remote-ollama.md) | Ollama через прокси | выполнено |
| [`12-optimize-flint-server.md`](tasks/12-optimize-flint-server.md) | оптимизация Flint | план |

Номера 05, 08, 10 в этой серии не использовались (10 — фронтенд, он в `wsv3/docs/tasks`).

## Текущее состояние API

Маршруты, DTO и правила — в хабе: `/mnt/developer/_hubx01/AGENTS.md`, раздел 3.
Коротко: чтение клиентов, сводки, сделок, товаров и денег + `POST` создания сделки;
id сделки — `КодСделки` (Guid), `LegacyId` — только аудит переноса.

## Правила для документов

1. Задача не удаляется после выполнения — в первую строку после заголовка ставится
   `**Статус:** выполнено | частично | план | архив`.
2. Пути и имена в исторических задачах не переписываются: если что-то переименовано,
   это отмечается в статусе (например, `backend/` → `SkladX01/`).
3. Ссылки на схему БД ведут на актуальный каталог `db/sklad_v4_1/`, а не на варианты
   из `prompts/`.
4. Журнал — по дате в имени файла: `journal/YYYY-MM-DD-тема.md`.
