# Stage 3 — hardware validation runbook

This runbook covers the two Stage 3 acceptance criteria that cannot be verified on the Linux
CI machine:

- **CPU:** a real GGUF model loads and generates text on Windows.
- **GPU:** optional CUDA 12 offload works on the target GTX 1650 (4 GB) or is documented as
  unavailable with the CPU fallback.

Linux CPU inference is already recorded in [STAGE_3_MODEL_RUNTIME.md](STAGE_3_MODEL_RUNTIME.md).
CI never downloads models and never requires a GPU.

## Prerequisites

- Windows 10/11 x64, .NET SDK 10.0.1xx (`dotnet --version`).
- The repository at the commit under validation.
- A GGUF model file with a known license and source. A 4B Q4_K_M model (2–3 GB) is a good
  fit for 16 GB RAM; a smaller model also validates the pipeline.
- For CUDA: an NVIDIA driver and the **CUDA 12 toolkit** on the machine. The NuGet backend
  ships only the llama.cpp libraries; `cudart64_12.dll` and `cublas64_12.dll` come from the
  toolkit, and LLamaSharp reads `CUDA_PATH` to decide which CUDA version to load. A driver
  without the toolkit is not enough.
- A GTX 1650 with at least 4 GB VRAM.
- About 3 GB of free disk space for build output (the CPU backend carries all platform
  assets) plus the model file.

Do not commit model weights to the repository.

## 1. Windows CPU validation

```powershell
$env:ASTRA_TEST_MODEL = "C:\models\<model>.gguf"
$env:ASTRA_TEST_GPU_LAYERS = "0"

dotnet test tests/Astra.ModelHost.Tests -c Release `
  --filter "Category=Hardware" --logger "console;verbosity=detailed"
```

Expected output includes a passing `RealModelLoadsGeneratesAndEmbeds` test, the runtime
capability line and three timing lines:

```
[.. INF] GPU offload supported: False; requested GPU layers: 0.
load: <n> s, gpu layers: 0
generate: <n> chunks, <n> characters, <n> s
embed: <n> dimensions, <n> s
```

Record the Windows version, CPU, RAM, model file name and quantization, and the three
timings. If the model does not support embeddings, the test fails at the embedding step;
record the exception and note that generation still passed (the runtime reports missing
embedding support).

## 2. CUDA 12 validation (optional)

The CUDA backend is not part of the default build or CI. `UseCuda=true` adds
`LLamaSharp.Backend.Cuda12` and writes its own lock file to
`src/Astra.ModelHost/obj/packages.cuda.lock.json`, so the committed CPU lock file and the
normal locked restore are unaffected.

```powershell
$env:ASTRA_TEST_MODEL = "C:\models\<model>.gguf"
$env:ASTRA_TEST_GPU_LAYERS = "99"

dotnet test tests/Astra.ModelHost.Tests -c Release -p:UseCuda=true `
  --filter "Category=Hardware" --logger "console;verbosity=detailed"
```

While the test runs, check GPU use in a second terminal:

```powershell
nvidia-smi
```

Acceptance evidence:

1. The runtime log reports `GPU offload supported: True; requested GPU layers: 99.` before
   the model loads. `False` means LLamaSharp did not select the CUDA backend.
2. The test passes with a non-zero `gpu layers` value.
3. `nvidia-smi` shows the ModelHost process with allocated GPU memory during load/inference.
4. Record the working `ASTRA_TEST_GPU_LAYERS` value, VRAM used and timings.

If the runtime logs `GPU layers were requested but the native backend has no usable GPU`,
the CUDA libraries were not loaded or no device was visible; treat it as a failed GPU check
and see the fallback note below.

If 99 layers exceed 4 GB VRAM, lower the value (for example 20–30) until load succeeds and
record that setting. If CUDA cannot be loaded (driver, toolkit or package mismatch), record the error, run the
CPU procedure again and document CUDA as unavailable with the CPU fallback. That also
satisfies acceptance criterion 6.

First-time restore downloads about 500 MB of CUDA runtime packages, and the build output
grows to roughly 1.2 GB. The committed `packages.lock.json` must stay unchanged; `git status`
should show no modifications after the CUDA build.

To return to the normal CPU build, run `dotnet restore AstraAgent.slnx` without `UseCuda`.

## 3. Reporting

Add the results to the Stage 3 report and `docs/PROJECT_STATUS.md`:

- host OS and driver version;
- model name, quantization and license;
- load/generate/embed timings;
- GPU layer count and VRAM use, or the CUDA failure and CPU fallback result;
- CI run for the commit under validation.

Do not mark CUDA as working without a recorded test run on the target machine.
