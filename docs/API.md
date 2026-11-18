# API Contract Plan

The API is a local/LAN interface to the same Astra instance. It must not create a second agent or independent memory.

Transport:
- ASP.NET Core REST.
- WebSocket or SSE for live events.
- Authentication and authorization for LAN clients.
- Versioned contracts.

Planned endpoint groups:
- `/api/chat`
- `/api/sessions`
- `/api/tasks`
- `/api/models`
- `/api/memory`
- `/api/projects`
- `/api/tools`
- `/api/extensions`
- `/api/skills`
- `/api/learning`
- `/api/system`
- `/api/settings`

Event envelope:
- `eventId`
- `taskId`
- `timestamp`
- `eventType`
- `payload`
- `schemaVersion`

Exact DTOs, error model, pagination, authentication, and compatibility policy must be designed in ADRs before implementation.
