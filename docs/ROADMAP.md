# Roadmap

Статус этапов — плановый. Этапы 0 и 1 завершены, спецификация утверждена.

| Этап | Название | Статус |
|---|---|---|
| 0 | Интервью, аудит, спецификация | Completed |
| 1 | Foundation | Completed |
| 2 | Core Infrastructure | Completed |
| 3 | Model Runtime | Not started |
| 4 | Agent Core | Not started |
| 5 | Tools & System Integration | Not started |
| 6 | Memory System | Not started |
| 7 | Desktop Application | Not started |
| 8 | Development Environment | Not started |
| 9 | Research & Knowledge | Not started |
| 10 | Vision & Documents | Not started |
| 11 | Plugins & Skills | Not started |
| 12 | Autonomous Operation | Not started |
| 13 | Android Client | Not started |
| 14 | Learning & Self-Improvement | Not started |

## Этап 1 — Foundation

### Цель
Подготовить воспроизводимую инженерную основу без преждевременной реализации продуктовых подсистем.

### Задачи
1. Проверить SDK, IDE/toolchain и целевые платформы. — выполнено (Linux локально, Windows в CI)
2. Зафиксировать ADR: solution layout, dependency direction, configuration, logging, testing, IPC strategy. — ADR 0001–0005 accepted 2026-11-18
3. Создать минимальный `.slnx`. — выполнено
4. Создать только реально необходимые проекты. — выполнено (`Astra.Host` + тесты)
5. Настроить Nullable, analyzers, `.editorconfig`, единый формат. — выполнено
6. Настроить unit test infrastructure. — выполнено
7. Настроить CI для build/test. — выполнено, CI зелёный на Ubuntu и Windows
8. Добавить базовые docs и PROJECT_STATUS. — выполнено
9. Проверить Windows build; подготовить Linux CI/build path. — выполнено в CI
10. Сформировать проверяемый Foundation exit report. — выполнено, [отчёт](STAGE_1_FOUNDATION_REPORT.md) подписан

### Критерии завершения
- Чистый checkout собирается воспроизводимо.
- Тесты запускаются.
- CI проверяет сборку и тесты.
- Документация соответствует фактической структуре.
- Нет пустых проектов без назначения.
- Все архитектурные решения оформлены ADR.
- Нет незафиксированных критических рисков.

## Этап 2 — Core Infrastructure

### Цель
Дать продуктовым подсистемам переиспользуемую инфраструктуру, не реализуя Agent Core,
память, инструменты или политику безопасности.

### Задачи
1. `Astra.Core`: `IClock`, `IEventBus`, `EventEnvelope`, `ISettingsStore`. — выполнено
2. `Astra.Infrastructure`: пути данных, SQLite, миграции, настройки, event bus, часы. — выполнено
3. Первая миграция `settings` с проверкой контрольных сумм. — выполнено
4. Композиция в Host: миграции при старте, health check БД. — выполнено
5. Serilog: консоль и rolling compact JSON в каталог логов. — выполнено
6. Тесты core/infrastructure/host. — выполнено (25 тестов)
7. ADR 0006 (persistence) и ADR 0007 (eventing). — Accepted 2026-11-18
8. CI на Ubuntu и Windows. — выполнено, run 44348269884 зелёный
9. Отчёт этапа. — подписан

### Критерии завершения
- Свежий каталог данных создаёт мигрированную SQLite БД с WAL.
- Миграции идемпотентны, изменение применённой миграции отклоняется.
- Настройки переживают новое соединение и новый экземпляр store.
- Event bus доставляет события всем подписчикам и изолирует ошибки handler'ов.
- `/health` возвращает 200 только при рабочей БД.
- Serilog пишет структурированные rolling-логи; секреты не логируются.
- Локальные build/test и CI на Ubuntu/Windows зелёные.
- Документация и статус соответствуют реализации.

## Правила этапов
Каждый этап должен иметь цели, требования, архитектуру, компоненты, файлы, тесты, критерии завершения, документацию и отчёт. Следующий этап начинается после проверки предыдущего.
