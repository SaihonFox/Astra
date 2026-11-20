# Astra Agent — Project Handoff

> **Purpose:** this file is the single entry point for a new AI coding agent (or developer)
> continuing Astra on another machine, for example the owner's Windows laptop with the
> GTX 1650. It records what exists, what was verified, what is not verified, and exactly what
> to do next. Read it after `AGENTS.md` and before changing code.
>
> **Snapshot:** 2026-11-20, repository `SaihonFox/Astra`, branch `master`. The snapshot was
> refreshed after commit `e4ad401`; always confirm the actual commit with `git log -1` and the
> working tree with `git status`.

---

## 0. Кратко для владельца (Russian quick start)

**Что готово**

- Этапы 0–2 завершены и подписаны.
- Этап 3 (Model Runtime) почти завершён: реестр моделей, контракт runtime, отдельный процесс
  `Astra.ModelHost` с gRPC по Named Pipe/Unix Socket, адаптер LLamaSharp для CPU, надзор за
  процессом и базовые лимиты реализованы и покрыты тестами.
- Локально на Linux проверена реальная генерация и эмбеддинги на Qwen3-4B-Q4_K_M.

**Что осталось до завершения этапа 3**

1. Проверить CPU-инференс на Windows (ноутбук владельца).
2. Проверить CUDA 12 на GTX 1650 (опционально; при невозможности — зафиксировать CPU fallback).
3. Написать отчёт этапа 3.8, обновить статус и получить зелёный CI.

**С чего начать на ноутбуке**

```powershell
git clone https://github.com/SaihonFox/Astra.git
cd Astra
dotnet --version                      # нужен SDK 10.0.1xx
dotnet restore AstraAgent.slnx --locked-mode
dotnet build AstraAgent.slnx --no-restore -c Release
dotnet test AstraAgent.slnx --no-build -c Release
dotnet format AstraAgent.slnx --verify-no-changes
```

Затем — раздел 7 (настройка ноутбука) и раздел 6 (следующие шаги). Модели и CUDA в CI не
нужны; тесты на ноутбуке должны проходить без скачивания моделей.

**Жёсткие ограничения:** один агент; локальные модели — только сменные движки; никаких
облачных AI API; действия проходят через tools + Policy Engine (этап 4, требует одобрения
владельца); секреты и содержимое пользователя не логировать.

---

## 1. What Astra is

Astra Agent is a single local-first autonomous AI agent. The approved specification is
`docs/FINAL_PRODUCT_SPECIFICATION.md`.

Non-negotiable product rules:

- **One runtime agent** with one Agent Core, one task state, one context, one memory and one
  history. Multiple local models are interchangeable inference engines selected by the Model
  Orchestrator — not separate personalities or agents.
- **No cloud AI APIs and no third-party AI assistants** in the product runtime. The internet
  is allowed for ordinary research, documentation, dependencies and updates.
- **Windows is the primary platform**, Linux is supported, Android is a later remote client.
  LAN-only access; the host must not be exposed to the public internet.
- **The LLM never executes system actions directly.** Actions go through typed tools, the
  Policy Engine, permission checks and audit.
- **Memory, task state and tool contracts are model-independent.**
- **Security and permissions live outside the LLM.** Prompt-injection protection is
  architectural, not only prompt-based.
- External content (web pages, files, repositories, e-mail, documents) is untrusted data,
  never instructions.
- Self-update must support verification, backup and rollback.
- Core, security policy and permission-system changes require the owner's approval.

Stages 0–2 are complete and signed off. Stage 3 is in progress; Stage 4 (Agent Core) has not
started and needs a plan and ADRs before implementation.

---

## 2. Current state (authoritative summary)

| Area | Status |
|---|---|
| Stage 0 — specification | Completed, owner-approved 2026-11-18 |
| Stage 1 — Foundation | Completed, report signed off |
| Stage 2 — Core Infrastructure | Completed, report signed off; ADR 0006/0007 accepted |
| Stage 3 — Model Runtime | In progress: phases 3.1–3.5 and 3.7 implemented; 3.6 pending owner hardware; 3.8 pending |
| Stage 4 — Agent Core | Not started; Policy Engine requires owner approval |

Stage 3 details:

- Registry: SQLite `models` table (migration 2), CRUD, validation, live availability.
- Runtime contract: `ILocalModelRuntime` in `Astra.Core.Inference` plus shared validation and
  a deterministic fake used by contract tests.
- IPC: versioned gRPC `astra.modelhost.v1` over a named pipe (Windows) or a Unix domain
  socket (Linux); no TCP. See `docs/MODEL_HOST_IPC.md`.
- Inference: LLamaSharp 0.30.0 with the CPU backend inside `Astra.ModelHost`; optional CUDA 12
  build is wired but not validated on a GPU. Real inference is validated on Linux CPU.
- Supervision: `ModelHostSupervisor` (client library) starts the process on demand, restarts it
  after a crash with bounded backoff and reloads the active model. Host wiring is still
  pending.
- Admission: one active model, `--max-model-bytes`, context/token/prompt/embedding/GPU-layer
  limits.

Test suite: **85 tests** — 23 core, 34 infrastructure, 2 host, 26 ModelHost. CI runs them on
Ubuntu and Windows without models or GPU.

---

## 3. Repository map

```
AGENTS.md                       mandatory working rules (Russian)
AstraAgent.slnx                 solution
global.json                     SDK 10.0.1xx band, rollForward latestFeature
Directory.Build.props           nullable, analyzers, warnings-as-errors, deterministic build
Directory.Packages.props        central package versions incl. LLamaSharp 0.30.0
src/Astra.Core/                 contracts only (no infrastructure, no LLamaSharp)
  EventEnvelope.cs              versioned event envelope
  IClock.cs, IEventBus.cs, ISettingsStore.cs
  Models/                       ModelDescriptor, capabilities, format, test status,
                                availability, validator, IModelRegistry
  Inference/                    ILocalModelRuntime, ModelLoadOptions, GenerationRequest/Chunk,
                                EmbeddingRequest/Result, LocalRuntimeValidation
src/Astra.Infrastructure/
  AstraPaths.cs                 data root + areas, ASTRA_DATA_DIR / Astra:DataDirectory
  Database/                     connection factory, checksum migration runner, migrations,
                                settings store, SQLite model registry
  Events/InProcessEventBus.cs
  Time/SystemClock.cs
src/Astra.Host/                 ASP.NET Core composition root, /health, Serilog, DI
src/Astra.ModelHost.Contracts/  proto3 contract, protocol version, endpoint parsing
  Protos/model_host.proto       astra.modelhost.v1
  ModelHostEndpoint.cs          pipe:<name> / unix:<absolute path>
src/Astra.ModelHost/            separate process: Kestrel HTTP/2 on pipe/UDS, gRPC service
  Program.cs                    CLI: --endpoint, --data-root, --runtime, --max-model-bytes
  Services/ModelHostService.cs  gRPC service over ILocalModelRuntime
  Services/LlamaSharpModelRuntime.cs
  Services/EchoModelRuntime.cs  deterministic test/diagnostic runtime
  Services/ModelHostRuntimeLimits.cs
src/Astra.ModelHost.Client/     ILocalModelRuntime client + process supervision
  ModelHostClient.cs
  ModelHostProcess.cs           start, handshake wait, stale socket cleanup, stop/kill
  ModelHostProcessOptions.cs
  ModelHostSupervisor.cs
  ModelHostUnavailableException.cs
tests/Astra.Core.Tests/         runtime contract + event envelope tests
tests/Astra.Infrastructure.Tests/ 34 tests (paths, migrations, settings, registry, events)
tests/Astra.Host.Tests/         health endpoint + registry integration
tests/Astra.ModelHost.Tests/    26 tests: endpoint, IPC, lifecycle, limits, supervisor,
                                llama failure paths, hardware-gated real model
docs/                           specification, architecture, ADRs, stage plans/reports,
                                MODEL_HOST_IPC, hardware runbook, status
prompts/                        agent handoff/bootstrap prompts (this file)
skills/                         guidance for coding agents
.github/workflows/ci.yml        Ubuntu + Windows: locked restore, build, format, test
```

---

## 4. Runtime architecture

```
Astra.Host (future Agent Core, Stage 4)
   │  ILocalModelRuntime
   ▼
Astra.ModelHost.Client
   ├─ ModelHostSupervisor   start/restart/reload policy
   ├─ ModelHostProcess      child process + handshake + shutdown
   └─ ModelHostClient       gRPC client, error translation
   │  gRPC over local transport only
   ▼
Astra.ModelHost (separate process)
   ├─ ModelHostService      gRPC service over ILocalModelRuntime
   ├─ LlamaSharpModelRuntime (default, --runtime llama)
   │     └─ LLamaSharp 0.30.0 → llama.cpp → GGUF on disk
   └─ EchoModelRuntime      (--runtime echo, tests/diagnostics only)
```

Key facts:

- **Transport:** named pipe on Windows (`pipe:<name>`), Unix domain socket on Linux/macOS
  (`unix:<absolute path>`), HTTP/2, gRPC. No TCP listener. The named pipe is created with
  `PipeOptions.CurrentUserOnly`; the Unix socket lives in the `ipc` directory, created `0700`.
- **Endpoint naming:** `<data-root>/ipc/modelhost-<instance>-<hash>.sock` or
  `astra-modelhost-<instance>-<hash>`; the hash is the first 4 bytes of SHA-256 over the data
  root, so different data roots cannot collide. Socket path is limited to 100 UTF-8 bytes.
- **Handshake:** `GetInfo` returns protocol version (`1.0`), server runtime name
  (`llamasharp` or `echo`), loaded model id and process id. The client verifies the protocol
  major version and the process id, which detects a stale endpoint owned by another process.
- **Limits:** gRPC messages up to 8 MiB in both directions; one model at a time; generation
  ≤ 32,768 tokens; prompt ≤ 1,000,000 characters; embedding batch ≤ 256 inputs; GPU layers
  ≤ 1000; temperature 0–2.
- **Lifecycle:** `ModelHostProcess.StartAsync` has a 20 s default startup deadline,
  verifies the process id, and on failure kills the child and cleans up. `StopAsync` sends
  `Shutdown` with a 3 s deadline, waits up to 5 s, then kills the process tree.
- **Supervision:** `ModelHostSupervisor` serializes calls, restarts on demand with bounded
  exponential backoff (`MaxRestartAttempts`, `RestartDelay`), and reloads the last model after
  a crash.
- **Model load:** the model path must be absolute; the runtime checks existence and the
  optional size limit before touching native code. A missing or corrupt GGUF fails as a
  managed error and the process keeps serving.
- **Generation:** tokens are streamed from `InteractiveExecutor` with a
  `DefaultSamplingPipeline` (temperature, optional seed); `MaxTokens` is enforced. A system
  prompt is prepended to the user prompt.
- **Threading:** llama.cpp contexts are not thread-safe, so load, generate, embed and unload
  are serialized. Unload waits for an active stream instead of disposing it mid-flight.
- **GPU:** each load logs `GPU offload supported: {bool}; requested GPU layers: {n}`. With the
  CPU build it is `False`; requesting GPU layers without a usable GPU logs a warning and
  falls back to CPU.
- **Not wired yet:** `Astra.Host` does not start ModelHost. `ModelHostSupervisor` is a
  library with process-kill tests and will be wired when Agent Core needs inference.

---

## 5. Verification and evidence

Local environment (this snapshot), Ubuntu 26.04, 4 vCPU, 7.6 GB RAM, no GPU, SDK 10.0.115:

```
dotnet restore AstraAgent.slnx --locked-mode                  → succeeded
dotnet build AstraAgent.slnx --no-restore -c Release          → 0 warnings, 0 errors
dotnet test AstraAgent.slnx --no-build -c Release             → 85 passed, 0 failed, 0 skipped
dotnet format AstraAgent.slnx --verify-no-changes             → exit 0
```

A clean copy without `bin/obj` was restored with `--locked-mode`, built and tested
successfully.

Real-model validation on Linux CPU with `Qwen3-4B-Q4_K_M.gguf` (2.4 GB, pre-existing local
file, context 512):

```
load                          7.5 s
generate 16 tokens            3.9 s
embeddings, 1 input           0.6 s, 2560 dimensions
unload                        graceful, process exits
```

Hardware test (trait `Hardware`, skipped unless `ASTRA_TEST_MODEL` is set):

```
ASTRA_TEST_MODEL=<absolute path to .gguf> \
  dotnet test tests/Astra.ModelHost.Tests -c Release --filter "Category=Hardware"
```

CI (GitHub Actions, Ubuntu + Windows):

- Latest green run for the documentation snapshot before the CI diagnostics fix (`e4ad401`):
  [44471553581](https://github.com/SaihonFox/Astra/actions/runs/44471553581).
- Latest green run for the inference code (`b274875`):
  [44468595677](https://github.com/SaihonFox/Astra/actions/runs/44468595677).
- Earlier green runs are listed in `docs/PROJECT_STATUS.md`.
- CI never downloads models and never uses a GPU.
- **Known flake:** Windows failed twice on commits that changed no product code — runs
  [44464834270](https://github.com/SaihonFox/Astra/actions/runs/44464834270) and
  [44471882692](https://github.com/SaihonFox/Astra/actions/runs/44471882692). The second run
  proved that the first version of the reporting step was skipped: its
  `steps.test.outcome == 'failure'` condition did not run after the failure. The step now uses
  `if: failure()`, tees `dotnet test` output to `test-output.log`, and publishes non-passed TRX
  results, run-level errors and a log tail in the step summary and in error annotations.
  ModelHost test collections are serialized (`xunit.runner.json`) to reduce process-start
  contention on Windows. The failing test is still unknown; the next Windows failure will
  name it.

---

## 6. Next work, in order

Do not start Stage 4 implementation before Stage 3 is signed off unless the owner explicitly
asks. Each step ends with recorded commands/results, updated docs and a green CI run.

### 6.1 Phase 3.6 — Windows CPU validation (owner's laptop)

1. Install .NET SDK 10.0.1xx and Git; clone the repository.
2. Run the full local verification from section 5 on Windows and record the output.
3. Obtain a GGUF model locally. The model used for Linux validation was Qwen3-4B-Q4_K_M
   (~2.4 GB); verify its license before use and do not commit weights.
4. Follow `docs/STAGE_3_HARDWARE_VALIDATION.md`, section 1.
5. Record OS build, CPU, RAM, model name/quantization/license and the three timings.

Acceptance: the hardware test passes on Windows CPU and the runtime logs
`GPU offload supported: False; requested GPU layers: 0.`

### 6.2 Phase 3.6 — optional CUDA 12 validation on the GTX 1650

1. Install the NVIDIA driver and the **CUDA 12 toolkit** (the NuGet CUDA backend ships only
   llama.cpp libraries; `cudart64_12.dll` and `cublas64_12.dll` come from the toolkit, and
   LLamaSharp uses `CUDA_PATH` to select the backend).
2. Run the CUDA procedure in `docs/STAGE_3_HARDWARE_VALIDATION.md`, section 2:

   ```powershell
   $env:ASTRA_TEST_MODEL = "C:\models\<model>.gguf"
   $env:ASTRA_TEST_GPU_LAYERS = "99"
   dotnet test tests/Astra.ModelHost.Tests -c Release -p:UseCuda=true `
     --filter "Category=Hardware" --logger "console;verbosity=detailed"
   ```

3. Confirm `GPU offload supported: True` before the load and check `nvidia-smi` for GPU
   memory during load/inference. Lower the layer count if 99 exceeds 4 GB VRAM.
4. If CUDA cannot work (driver/toolkit/package mismatch or OOM), record the exact error, rerun
   the CPU procedure and document CUDA as unavailable with the CPU fallback. That satisfies
   the acceptance criterion too.

Note: `-p:UseCuda=true` writes its own lock file to
`src/Astra.ModelHost/obj/packages.cuda.lock.json` and restores ~500 MB of CUDA packages.
The committed `packages.lock.json` must stay unchanged; `git status` should show no lock-file
modification. Return to the normal CPU build with `dotnet restore AstraAgent.slnx`.

### 6.3 Phase 3.8 — Stage 3 report and sign-off

Create `docs/STAGE_3_MODEL_RUNTIME_REPORT.md` (not present yet) with:

- environment and OS versions used for validation;
- model name, quantization, license and source;
- CPU and GPU (or fallback) timings;
- the exact commands and their output;
- CI run links for the final commit;
- limitations that remain (Host wiring, orphan-process watchdog, log redaction, flake).

Then update:

- `docs/STAGE_3_MODEL_RUNTIME.md` (phase table and verification log);
- `docs/PROJECT_STATUS.md` (status, counts, current work, not-yet-verified list);
- `README.md` (current state);
- `docs/ROADMAP.md` (Stage 3 statuses);
- `docs/ARCHITECTURE.md` if the runtime description changes.

Ask the owner to sign off before starting Stage 4.

### 6.4 Stage 4 — Agent Core (only after sign-off)

Stage 4 has no detailed plan document yet. Derive it from
`docs/FINAL_PRODUCT_SPECIFICATION.md` section 4: Request Analyzer, Intent Classifier, Context
Manager, Model Orchestrator, Task Planner, Task Executor, Tool Dispatcher, Result Validator,
Response Generator, Session Manager, Task State Engine and Policy Engine.

Rules for starting Stage 4:

- Write `docs/STAGE_4_AGENT_CORE.md` with goals, scope, components, files, tests, exit
  criteria and an explicit statement of what is out of scope.
- Propose ADRs for task-state persistence, model orchestration/selection, context assembly and
  the Policy Engine before implementing them.
- The Policy Engine, permission model and audit trail are security-critical and require the
  owner's explicit approval.
- Wire `ModelHostSupervisor` into `Astra.Host` only as part of a concrete Agent Core
  requirement, with a job object/watchdog so ModelHost cannot outlive a crashed host.
- Keep everything local: no cloud inference, no multi-agent delegation, no AI provider SDKs.

---

## 7. Setting up the Windows laptop

1. Install Git and .NET SDK 10.0.1xx. Verify `dotnet --list-sdks` contains `10.0.x`;
   `global.json` pins the `10.0.100` feature band with `rollForward: latestFeature`.
2. Clone `https://github.com/SaihonFox/Astra.git` (the repository is public; use the owner's
   credentials only if pushing).
3. From the repository root:

   ```powershell
   dotnet restore AstraAgent.slnx --locked-mode
   dotnet build AstraAgent.slnx --no-restore -c Release
   dotnet test AstraAgent.slnx --no-build -c Release
   dotnet format AstraAgent.slnx --verify-no-changes
   ```

4. Optional host smoke test (loopback only):

   ```powershell
   $env:ASTRA_DATA_DIR = "$env:TEMP\astra-data"
   dotnet run --project src/Astra.Host
   # in another terminal:
   Invoke-WebRequest http://127.0.0.1:5298/health
   ```

5. Optional manual ModelHost run (echo runtime, no model):

   ```powershell
   dotnet run --project src/Astra.ModelHost -- `
     --endpoint pipe:astra-manual --data-root "$env:TEMP\astra-data" --runtime echo
   ```

6. For model validation set `ASTRA_TEST_MODEL`; for CUDA also install the toolkit and use
   `-p:UseCuda=true` as in section 6.2.

Do not put model weights in the repository or in the data root of another user. The data
root stores SQLite, logs, IPC sockets and later models/knowledge/backups.

---

## 8. Working rules for the next agent

Read, in order:

1. `AGENTS.md` — mandatory rules (Russian).
2. This file.
3. `docs/FINAL_PRODUCT_SPECIFICATION.md` — approved specification.
4. `docs/ROADMAP.md`, `docs/PROJECT_STATUS.md`.
5. `docs/ARCHITECTURE.md`, the relevant ADRs, `docs/MODEL_HOST_IPC.md`.
6. `docs/DEVELOPMENT.md`, `docs/SECURITY.md`, `docs/CONTRIBUTING.md`.
7. The relevant `skills/*/SKILL.md`.

Process:

- Check `git status` and `git log -1` first; do not overwrite uncommitted owner work.
- Work in small, verified iterations. No giant commits and no empty placeholder projects.
- Before architecture, security, data or API changes, write an ADR and get owner approval.
- Never claim a check passed unless it was actually run; report exact commands and results.
- Update `docs/PROJECT_STATUS.md` when the stage status changes and add/refresh the stage
  report at the end of a stage.
- Add tests for new behavior; keep CI green on Ubuntu and Windows without models or GPU.
- Do not disable analyzers, formatting or tests to make CI green without a documented
  decision.
- Keep secrets out of source, JSON, SQLite and logs.
- Do not log prompts, model outputs, file contents or user data: central log redaction is not
  implemented yet.

Definition of Done: behavior implemented, architectural boundaries respected, tests
added/updated, checks actually run, docs updated, security reviewed, remaining limitations
listed.

Git: `master` is the only long-lived branch; CI runs on every push. On this development
machine pushes use a deploy key:

```bash
GIT_SSH_COMMAND="ssh -i /root/.ssh/github_actions_deploy -o IdentitiesOnly=yes -o BatchMode=yes -o StrictHostKeyChecking=yes" \
  git push git@github.com:SaihonFox/Astra.git master
```

On the laptop use the owner's normal Git credentials instead.

---

## 9. Known pitfalls

- **Warnings are errors.** Analyzers (`latest-recommended`) and code style are enforced in the
  build. Use source-generated logging (`LoggerMessage`), invariant culture for all formatting,
  and explicit `CancellationToken` propagation. `async void` is only allowed for UI handlers.
- **Packages are centrally managed and locked.** Update `Directory.Packages.props`, then run
  `dotnet restore` without `--locked-mode`, commit the changed `packages.lock.json` files.
  CI restores with `--locked-mode`. Do not pass `-p:RuntimeIdentifier` to the solution: the
  lock files do not contain RIDs and restore fails with NU1004.
- **`UseCuda` has its own lock file** (`src/Astra.ModelHost/obj/packages.cuda.lock.json`).
  Never let the CUDA restore rewrite the committed CPU lock file.
- **LLamaSharp API (0.30.0).** Set sampling through `InferenceParams.SamplingPipeline`
  (`DefaultSamplingPipeline` carries temperature and seed). `InteractiveExecutor` is not
  `IDisposable`; `LLamaContext` and `LLamaWeights` are. Contexts are not thread-safe. The
  native backend is selected at first use; CUDA needs `CUDA_PATH` from a toolkit install.
  Native log output is filtered to warning/error to avoid per-tensor spam.
- **Native failures.** Missing/corrupt GGUF files must fail as managed errors and keep the
  process alive. A hard native crash is contained by the process boundary; the supervisor
  restarts it. Do not assume every native failure is recoverable in-process.
- **Tests and temp data.** Tests use per-test temporary data roots. Clear SQLite connection
  pools before deleting a database directory on Windows. ModelHost tests spawn real child
  processes; use the `echo` runtime unless the test is about llama.
- **IPC is local-only.** Do not add a TCP listener to ModelHost. Windows uses named pipes,
  Linux/macOS Unix sockets. `Unix socket paths must fit 100 UTF-8 bytes`; keep data roots
  short.
- **One instance per endpoint.** A second process on the same instance/data root must fail
  (the handshake detects a different process id). Stale Unix socket files are cleaned up on
  start; on Windows the pipe disappears with the process.
- **Generation is serialized.** Do not call load/unload concurrently with generation; the
  runtime and supervisor serialize calls on purpose, and unload waits for the stream.
- **Model paths must be absolute**, and the file must exist before native loading. The
  optional `--max-model-bytes` is enforced before loading.
- **CI logs and artifacts need authentication.** The GitHub API without a token is limited to
  60 requests/hour and job logs/artifact downloads are not reachable. Use the check-runs
  endpoint for status; failed test names now appear in annotations and the step summary.
- **Orphan process risk.** If `Astra.Host` crashes, the OS does not kill ModelHost; a job
  object/watchdog is still pending for the Host wiring phase.
- **Log redaction is not implemented.** Never log prompts, completions, file contents or
  secrets. Serilog writes console and rolling compact JSON under `<data-root>/logs`.
- **Flaky Windows test run.** Two runs failed on commits with identical product code. Treat a
  single Windows failure as suspicious but not automatically as a code regression; read the
  `Report failures` step summary or annotation for the test name, rerun, and record it. Public
  job logs and TRX artifacts need authentication, but `GET /repos/SaihonFox/Astra/actions/runs/<id>/jobs`
  is public and shows which step failed; `.../annotations_partial` (with
  `X-Requested-With: XMLHttpRequest`) shows annotations. A browser user agent is needed for the
  run HTML page; job logs still return 404 without a token.
- **No models in CI.** Do not add a test that downloads or commits weights. Hardware tests
  are trait-gated by `ASTRA_TEST_MODEL` and are no-ops otherwise.

---

## 10. Open questions for the owner

1. When is Stage 3 considered signed off — after Windows CPU only, or after the CUDA attempt
   (success or documented fallback)?
2. Which GGUF model should be the standard validation model (name, quantization, license,
   source) for future hardware runs?
3. Which Stage 4 acceptance criteria matter most: task state/persistence, model
   orchestration, or the Policy Engine skeleton?
4. Should ModelHost be started by `Astra.Host` at Stage 4, or only when the first Agent Core
   flow needs inference?
5. Preferred model storage location on Windows (data root `models/` vs a separate drive) for
   multi-GB files.
6. Whether to add Windows-specific integration tests that run only locally (hardware tests
   already cover this partially).

Record the answers in the relevant ADR or stage plan rather than only in chat.

---

## 11. Copy-paste bootstrap prompt for the next agent

```text
You are continuing Astra Agent at /path/to/Astra (branch master).

Read in this order:
1. AGENTS.md
2. prompts/PROJECT_HANDOFF.md
3. docs/FINAL_PRODUCT_SPECIFICATION.md
4. docs/ROADMAP.md and docs/PROJECT_STATUS.md
5. docs/ARCHITECTURE.md, docs/MODEL_HOST_IPC.md, docs/STAGE_3_MODEL_RUNTIME.md,
   docs/STAGE_3_HARDWARE_VALIDATION.md
6. The relevant skills/*/SKILL.md

Then:
- run `git log -1` and `git status`, and summarize the actual state;
- verify the toolchain (`dotnet --list-sdks`, `dotnet --version`);
- run restore --locked-mode, build, tests and format as documented;
- do not implement Stage 4 before Stage 3 sign-off;
- continue with phase 3.6 on this Windows machine using the hardware runbook, or phase 3.8
  if hardware results are already recorded.

Rules: one agent, local models only, no cloud AI APIs, no multi-agent runtime. Security and
permissions are outside the LLM. Do not claim checks passed unless you ran them. Update
docs/PROJECT_STATUS.md and the stage report with exact commands and results. Do not commit
model weights or secrets.
```
