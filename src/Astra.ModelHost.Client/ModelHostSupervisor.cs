using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Astra.Core.Inference;
using Astra.Infrastructure;

namespace Astra.ModelHost.Client;

/// <summary>
/// Options for <see cref="ModelHostSupervisor"/>.
/// </summary>
public sealed record ModelHostSupervisorOptions
{
	/// <summary>Gets the resolved Astra paths.</summary>
	public required AstraPaths Paths { get; init; }

	/// <summary>Gets the absolute path to Astra.ModelHost.dll or the app host.</summary>
	public required string AssemblyPath { get; init; }

	/// <summary>Gets the process tunables; defaults are used when omitted.</summary>
	public ModelHostProcessOptions Process { get; init; } = new();

	/// <summary>Gets the maximum number of start attempts after a crash.</summary>
	public int MaxRestartAttempts { get; init; } = 3;

	/// <summary>Gets the base delay between restart attempts.</summary>
	public TimeSpan RestartDelay { get; init; } = TimeSpan.FromMilliseconds(250);
}

/// <summary>
/// Supervises a local ModelHost process and exposes it as <see cref="ILocalModelRuntime"/>.
/// </summary>
/// <remarks>
/// The process starts on demand. When it exits unexpectedly, the next operation starts a new
/// process with bounded exponential backoff and reloads the last model. Operations are
/// serialized: generation and embeddings hold the process until the stream completes, so an
/// unload cannot dispose native state underneath an active request.
/// </remarks>
public sealed class ModelHostSupervisor : ILocalModelRuntime
{
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly ModelHostSupervisorOptions _options;
	private ModelHostProcess? _process;
	private ModelLoadOptions? _loadOptions;
	private string? _loadedModelId;
	private int _restartCount;
	private int _disposeRequested;
	private bool _startedOnce;

	/// <summary>Initializes a new instance of the <see cref="ModelHostSupervisor"/> class.</summary>
	/// <param name="options">Supervision and process options.</param>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">A limit is outside its allowed range.</exception>
	public ModelHostSupervisor(ModelHostSupervisorOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.MaxRestartAttempts < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(options),
				options.MaxRestartAttempts,
				"At least one start attempt is required.");
		}

		if (options.RestartDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(options),
				options.RestartDelay,
				"The restart delay must not be negative.");
		}

		_options = options;
	}

	/// <summary>Gets whether the supervised process is currently running.</summary>
	public bool IsRunning => _process is { IsRunning: true };

	/// <summary>Gets the process identifier when running.</summary>
	public int? ProcessId => _process is { IsRunning: true } process ? process.ProcessId : null;

	/// <summary>Gets how many times the process has been restarted after a crash.</summary>
	public int RestartCount => Volatile.Read(ref _restartCount);

	/// <inheritdoc />
	public string? LoadedModelId
	{
		get
		{
			var process = _process;

			return process is { IsRunning: true }
				? Volatile.Read(ref _loadedModelId)
				: null;
		}
	}

	/// <inheritdoc />
	public async Task LoadAsync(
		string modelId,
		ModelLoadOptions options,
		CancellationToken cancellationToken = default)
	{
		LocalRuntimeValidation.ValidateModelId(modelId);
		LocalRuntimeValidation.ValidateLoadOptions(options);

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			ObjectDisposedException.ThrowIf(
				Volatile.Read(ref _disposeRequested) != 0,
				this);

			var client = await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

			try
			{
				await client.LoadAsync(modelId, options, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exception) when (IsProcessFailure(exception))
			{
				await DisposeProcessAsync().ConfigureAwait(false);
				throw;
			}

			Volatile.Write(ref _loadedModelId, modelId);
			_loadOptions = options;
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <inheritdoc />
	public async Task UnloadAsync(CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			Volatile.Write(ref _loadedModelId, null);
			_loadOptions = null;

			if (_process is { IsRunning: true } process)
			{
				try
				{
					await process.Client.UnloadAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (Exception exception) when (IsProcessFailure(exception))
				{
					await DisposeProcessAsync().ConfigureAwait(false);
					throw;
				}
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<GenerationChunk> GenerateAsync(
		GenerationRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		LocalRuntimeValidation.ValidateGenerationRequest(request);

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			ObjectDisposedException.ThrowIf(
				Volatile.Read(ref _disposeRequested) != 0,
				this);

			var client = await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

			await foreach (var chunk in client
				.GenerateAsync(request, cancellationToken)
				.ConfigureAwait(false))
			{
				yield return chunk;
			}
		}
		finally
		{
			if (_process is { IsRunning: false })
			{
				await DisposeProcessAsync().ConfigureAwait(false);
			}

			_gate.Release();
		}
	}

	/// <inheritdoc />
	public async Task<EmbeddingResult> EmbedAsync(
		EmbeddingRequest request,
		CancellationToken cancellationToken = default)
	{
		LocalRuntimeValidation.ValidateEmbeddingRequest(request);

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			ObjectDisposedException.ThrowIf(
				Volatile.Read(ref _disposeRequested) != 0,
				this);

			var client = await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);

			try
			{
				return await client.EmbedAsync(request, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exception) when (IsProcessFailure(exception))
			{
				await DisposeProcessAsync().ConfigureAwait(false);
				throw;
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
		{
			return;
		}

		await _gate.WaitAsync().ConfigureAwait(false);

		try
		{
			Volatile.Write(ref _loadedModelId, null);
			_loadOptions = null;
			await DisposeProcessAsync().ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
			_gate.Dispose();
		}
	}

	private async Task<ModelHostClient> EnsureReadyAsync(CancellationToken cancellationToken)
	{
		if (_process is { IsRunning: true } running)
		{
			return running.Client;
		}

		await DisposeProcessAsync().ConfigureAwait(false);

		Exception? lastError = null;

		for (var attempt = 1; attempt <= _options.MaxRestartAttempts; attempt++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (attempt > 1)
			{
				var delay = TimeSpan.FromMilliseconds(Math.Min(
					_options.RestartDelay.TotalMilliseconds * attempt,
					TimeSpan.FromSeconds(5).TotalMilliseconds));

				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}

			try
			{
				var process = await ModelHostProcess
					.StartAsync(
						_options.Paths,
						_options.AssemblyPath,
						_options.Process,
						cancellationToken)
					.ConfigureAwait(false);

				_process = process;

				if (_startedOnce)
				{
					Interlocked.Increment(ref _restartCount);
				}

				_startedOnce = true;
				break;
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				lastError = exception;
				await DisposeProcessAsync().ConfigureAwait(false);
			}
		}

		if (_process is null)
		{
			throw new ModelHostUnavailableException(
				$"ModelHost could not be started after {_options.MaxRestartAttempts} attempts.",
				lastError);
		}

		await RestoreLoadedModelAsync(cancellationToken).ConfigureAwait(false);

		return _process.Client;
	}

	private async Task RestoreLoadedModelAsync(CancellationToken cancellationToken)
	{
		var modelId = Volatile.Read(ref _loadedModelId);
		var options = _loadOptions;

		if (modelId is null || options is null || _process is null)
		{
			return;
		}

		try
		{
			await _process.Client
				.LoadAsync(modelId, options, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception exception)
		{
			Volatile.Write(ref _loadedModelId, null);
			_loadOptions = null;

			throw new InvalidOperationException(
				"The model was not reloaded after ModelHost restarted.",
				exception);
		}
	}

	private async Task DisposeProcessAsync()
	{
		var process = _process;
		_process = null;

		if (process is not null)
		{
			await process.DisposeAsync().ConfigureAwait(false);
		}
	}

	private bool IsProcessFailure(Exception exception) =>
		exception is ModelHostUnavailableException
			or TimeoutException
			or IOException
			or SocketException
			or HttpRequestException ||
		_process is null ||
		!_process.IsRunning;
}
