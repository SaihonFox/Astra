# ModelHost IPC — design note

- **Status:** Implemented in Stage 3 phase 3.4
- **Applies to:** `Astra.Host` ↔ `Astra.ModelHost`
- **Related ADRs:** [0005](adr/0005-ipc-and-process-boundaries.md),
  [0008](adr/0008-model-runtime-strategy.md)

This note records the concrete choices that ADR 0005 left to a later design note: transport
names, access control, handshake fields, message limits and supervision behavior.

## Transport

| Platform | Transport | Endpoint form |
|---|---|---|
| Windows | Named pipe | `pipe:astra-modelhost-<instance>-<hash>` |
| Linux, macOS | Unix domain socket | `unix:<data-root>/ipc/modelhost-<instance>-<hash>.sock` |

- Kestrel 10 exposes `ListenNamedPipe` and `ListenUnixSocket`; only HTTP/2 is enabled, which
  is what gRPC requires.
- No TCP listener exists in ModelHost. The public API in `Astra.Host` is a separate boundary.
- The `<hash>` is the first four bytes of the SHA-256 of the absolute data-root path. It keeps
  parallel Astra instances with different data roots from colliding without embedding the
  path in a global pipe name space. It is not a secret.
- `ModelHostEndpoint` validates names and paths before anything is started: pipe names are
  limited to ASCII letters, digits, `.`, `_`, `-`; socket paths must be absolute, control-free
  and at most 100 UTF-8 bytes (conservative `sun_path` limit).

## Access control

- Windows: the pipe is created with `PipeOptions.CurrentUserOnly` through
  `NamedPipeTransportOptions.CreateNamedPipeServerStream`. Other users cannot open it.
- Linux/macOS: the socket lives in the data root's `ipc` directory, created with mode `0700`
  by `AstraPaths`. The socket inode itself is created as `0755`, so only the owner has write
  permission, which is what `connect(2)` requires.
- The client connects with `PipeOptions.CurrentUserOnly` as well.
- Transport security relies on OS access control only. There is no TLS and no shared secret;
  a same-user process is considered trusted, consistent with ADR 0005.

## Protocol

- Contract: `src/Astra.ModelHost.Contracts/Protos/model_host.proto`, package
  `astra.modelhost.v1`, C# namespace `Astra.ModelHost.Contracts.V1`.
- Version: `ModelHostProtocol.Version = "1.0"`, major version `1`.
- Handshake: `GetInfo` returns protocol version, runtime name, loaded model, process id.
  The client rejects any version string that differs from its own, and `ModelHostProcess`
  additionally verifies the returned process id so an unrelated instance on a stale endpoint
  cannot be mistaken for the child process.
- RPCs: `GetInfo`, `LoadModel`, `UnloadModel`, `Generate` (server streaming), `Embed`,
  `Shutdown`. `LoadModel` receives the resolved absolute model path, so ModelHost never reads
  the registry database.
- `Generate` is the only streaming call. The server checks that the requested model id is the
  loaded one and writes chunks as they are produced; cancellation propagates through the gRPC
  call and the runtime's `CancellationToken`.
- Error mapping: `InvalidArgument` → `ArgumentException`, `FailedPrecondition` →
  `InvalidOperationException`, `NotFound` → `KeyNotFoundException`, `Cancelled` →
  `OperationCanceledException`, `DeadlineExceeded` → `TimeoutException`, `Unavailable` →
  `InvalidOperationException("ModelHost is unavailable.")`. Details are transported only for
  these mapped codes; unexpected failures become `Internal` with a generic message.

## Limits and validation

- Maximum gRPC message size: 8 MiB in both directions, configured on the server. Larger
  requests fail at the transport instead of reaching the runtime.
- Requests are validated on the client and again on the server with
  `LocalRuntimeValidation`, so a compromised or buggy peer cannot bypass limits such as
  maximum prompt size, token count, temperature range or embedding batch size.
- One model may be loaded at a time; a second load is rejected until `UnloadModel`.

## Lifecycle

- `ModelHostProcess.StartAsync` owns the child process: it resolves the endpoint, removes a
  stale Unix socket if nothing answers on it, starts the app host (or `dotnet Astra.ModelHost.dll`
  when no app host is available), and polls the handshake until a deadline (20 s default).
- Startup failures are bounded and observable: early exit throws with the exit code, a
  timeout throws `TimeoutException` with the last transient error, and a live foreign process
  on the endpoint is rejected.
- `StopAsync` sends `Shutdown` with a 3 s deadline, waits up to 5 s for exit, then kills the
  process tree. `DisposeAsync` always disposes the channel and the `Process` handle.
- Logs go to `<data-root>/logs/modelhost-*.jsonl`; the host process does not write model
  content or prompts to logs.

## Known limitations

- gRPC over named pipes is only exercised on Windows CI; Linux CI exercises Unix sockets.
- If the owning `Astra.Host` process crashes after startup, ModelHost is not terminated by
  the OS. A watchdog or job object is planned with the supervision work in phase 3.7.
- A same-user process can still connect to the endpoint and call `Shutdown` or `Generate`.
  Peer authentication beyond OS ACLs is not implemented and is not planned for local IPC.
- Backpressure during streaming is provided by HTTP/2 flow control only; there is no
  application-level rate limit yet.
- The process starts the LLamaSharp runtime by default. The deterministic echo runtime is
  still selectable with `--runtime echo` and exists for tests and diagnostics only.
- Model size admission and GPU configuration arrive with phases 3.6–3.7.
