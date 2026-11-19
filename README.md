# Astra Agent

[![CI](https://github.com/SaihonFox/Astra/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/SaihonFox/Astra/actions/workflows/ci.yml)

**Статус:** Этапы 0–2 завершены; Этап 3 — Model Runtime в работе (остались проверка на оборудовании владельца и отчёт этапа)
**Основная платформа:** Windows  
**Дополнительная платформа:** Linux  
**Клиент:** Android  
**Язык основной платформы:** C# / .NET 10  
**Desktop:** Avalonia UI  
**Inference:** llama.cpp / GGUF

Astra Agent — персональный автономный локальный ИИ-агент. Это **один агент с единым состоянием, памятью, планированием и исполнением**, который может использовать несколько локальных моделей как сменные вычислительные движки.

## Важное разграничение

- В готовом продукте Astra не делегирует интеллектуальные задачи другим ИИ-агентам и не использует облачные AI API.
- Во время разработки репозитория владелец может привлекать любые внешние инструменты и coding agents как разработчиков. Это не часть runtime-архитектуры Astra.
- Интернет разрешён для поиска документации, загрузки исходников, зависимостей и обновлений. Облачные ИИ-сервисы для выполнения задач Astra запрещены.

## Текущее состояние

Каркас Foundation, базовая инфраструктура и рантайм локальных моделей: solution `AstraAgent.slnx`, хост `Astra.Host`, SQLite с миграциями, in-process event bus, настройки, реестр моделей, Serilog-логирование, health check и CI для Linux/Windows. `Astra.ModelHost` запускает llama.cpp через LLamaSharp в отдельном процессе (Named Pipe на Windows, Unix Socket на Linux), поддерживает загрузку GGUF, потоковую генерацию, эмбеддинги, лимиты и перезапуск после падения. Реальная генерация проверена на Linux CPU; проверка на Windows и CUDA ожидает оборудования владельца. Agent Core, память, инструменты и Policy Engine ещё не реализованы.

Проверено на .NET SDK 10.0.115 (Ubuntu 26.04):

```bash
dotnet restore AstraAgent.slnx --locked-mode
dotnet build AstraAgent.slnx --no-restore -c Release
dotnet test AstraAgent.slnx --no-build -c Release
dotnet format AstraAgent.slnx --verify-no-changes
ASTRA_DATA_DIR=/tmp/astra-data dotnet run --project src/Astra.Host
curl http://127.0.0.1:5298/health
```

## Лицензия

MIT — см. [LICENSE](LICENSE). Сторонние зависимости и модели сохраняют собственные лицензии; см. [LICENSE-DECISION.md](LICENSE-DECISION.md).

## Документы

- [Утверждённая спецификация](docs/FINAL_PRODUCT_SPECIFICATION.md)
- [Инструкции для coding agents](AGENTS.md)
- [План разработки](docs/ROADMAP.md)
- [Передача проекта новому агенту](prompts/PROJECT_HANDOFF.md)
- [Статус проекта](docs/PROJECT_STATUS.md)
- [Архитектура](docs/ARCHITECTURE.md)
- [Безопасность](docs/SECURITY.md)
- [Разработка](docs/DEVELOPMENT.md)
- [Архитектурные решения (ADR)](docs/adr/README.md)
- [Отчёт Stage 1 — Foundation](docs/STAGE_1_FOUNDATION_REPORT.md)
- [План Stage 2 — Core Infrastructure](docs/STAGE_2_CORE_INFRASTRUCTURE.md)
- [Отчёт Stage 2 — Core Infrastructure](docs/STAGE_2_CORE_INFRASTRUCTURE_REPORT.md)
- [План Stage 3 — Model Runtime](docs/STAGE_3_MODEL_RUNTIME.md)
- [Проверка этапа 3 на оборудовании](docs/STAGE_3_HARDWARE_VALIDATION.md)
- [Навыки агентов](skills/README.md)

## Правило начала работ

Спецификация утверждена владельцем. Разработка идёт по этапам дорожной карты. До реализации необходимо проверять актуальные версии SDK и библиотек, лицензии, платформенную совместимость и доступность нужных инструментов.

Не создавайте весь проект одним большим коммитом. Двигайтесь небольшими проверяемыми итерациями. Не заявляйте, что сборка или тесты прошли, если они фактически не запускались.

## Цели первого этапа

1. Проверить среду разработки и целевые платформы.
2. Зафиксировать ADR для ключевых фундаментальных решений.
3. Создать минимальный solution и инфраструктурный каркас.
4. Настроить форматирование, анализаторы, тестирование и CI.
5. Обеспечить воспроизводимую сборку.
6. Не реализовывать преждевременно все подсистемы и не создавать пустые проекты «на будущее».
