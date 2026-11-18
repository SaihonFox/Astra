# Roadmap

Статус этапов — плановый. Этап 0 завершён и спецификация утверждена.

| Этап | Название | Статус |
|---|---|---|
| 0 | Интервью, аудит, спецификация | Completed |
| 1 | Foundation | Not started |
| 2 | Core Infrastructure | Not started |
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

### Предлагаемые задачи
1. Проверить SDK, IDE/toolchain и целевые платформы.
2. Зафиксировать ADR: solution layout, dependency direction, configuration, logging, testing, IPC strategy (не обязательно реализовывать IPC сразу).
3. Создать минимальный `.slnx`.
4. Создать только реально необходимые проекты.
5. Настроить Nullable, analyzers, `.editorconfig`, единый формат.
6. Настроить unit test infrastructure.
7. Настроить CI для build/test.
8. Добавить базовые docs и PROJECT_STATUS.
9. Проверить Windows build; подготовить Linux CI/build path.
10. Сформировать проверяемый Foundation exit report.

### Критерии завершения
- Чистый checkout собирается воспроизводимо.
- Тесты запускаются.
- CI проверяет сборку и тесты.
- Документация соответствует фактической структуре.
- Нет пустых проектов без назначения.
- Все архитектурные решения оформлены ADR.
- Нет незафиксированных критических рисков.

## Правила этапов
Каждый этап должен иметь цели, требования, архитектуру, компоненты, файлы, тесты, критерии завершения, документацию и отчёт. Следующий этап начинается после проверки предыдущего.
