using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Astra.Core.Inference;
using Astra.ModelHost.Contracts;
using Grpc.Core;
using Grpc.Net.Client;
using V1 = Astra.ModelHost.Contracts.V1;

namespace Astra.ModelHost.Client;

/// <summary>
/// Client for a local ModelHost process. Implements the runtime contract over gRPC.
/// </summary>
public sealed class ModelHostClient : ILocalModelRuntime
{
	private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

	private readonly GrpcChannel _channel;
	private readonly V1.ModelHost.ModelHostClient _client;
	private string? _loadedModelId;

	private ModelHostClient(
		GrpcChannel channel,
		V1.ModelHost.ModelHostClient client,
		ModelHostInfo info)
	{
		_channel = channel;
		_client = client;
		Info = info;
		_loadedModelId = info.LoadedModelId;
	}

	/// <summary>Gets the handshake information captured when the connection was opened.</summary>
	public ModelHostInfo Info { get; }

	/// <inheritdoc />
	public string? LoadedModelId => Volatile.Read(ref _loadedModelId);

	/// <summary>
	/// Connects to a ModelHost endpoint and verifies the protocol version.
	/// </summary>
	/// <param name="endpoint">Local endpoint.</param>
	/// <param name="timeout">Handshake timeout; defaults to 10 seconds.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="TimeoutException">The handshake did not complete in time.</exception>
	/// <exception cref="InvalidOperationException">The protocol version is incompatible.</exception>
	public static async Task<ModelHostClient> ConnectAsync(
		ModelHostEndpoint endpoint,
		TimeSpan? timeout = null,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(endpoint);

		var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
		var channel = CreateChannel(endpoint);
		var client = new V1.ModelHost.ModelHostClient(channel);

		using var timeoutSource = new CancellationTokenSource(effectiveTimeout);
		using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
			cancellationToken,
			timeoutSource.Token);

		try
		{
			var response = await client
				.GetInfoAsync(new V1.GetInfoRequest(), cancellationToken: linkedSource.Token)
				.ConfigureAwait(false);

			if (!string.Equals(
					response.ProtocolVersion,
					ModelHostProtocol.Version,
					StringComparison.Ordinal))
			{
				throw new InvalidOperationException(
					$"ModelHost protocol '{response.ProtocolVersion}' is not compatible with " +
					$"'{ModelHostProtocol.Version}'.");
			}

			return new ModelHostClient(
				channel,
				client,
				new ModelHostInfo(
					response.ProtocolVersion,
					response.RuntimeName,
					string.IsNullOrEmpty(response.LoadedModelId) ? null : response.LoadedModelId,
					response.ProcessId));
		}
		// gRPC may surface a canceled handshake token either as OperationCanceledException or
		// as RpcException(Cancelled); both are retryable timeouts when the caller's token is
		// still active. Windows runners reproduced this as a startup failure under load.
		catch (Exception exception) when (
			!cancellationToken.IsCancellationRequested &&
			(exception is OperationCanceledException ||
			 exception is RpcException { StatusCode: StatusCode.Cancelled }))
		{
			channel.Dispose();
			throw new TimeoutException(
				$"ModelHost did not complete the handshake within {effectiveTimeout}.",
				exception);
		}
		catch (RpcException exception) when (cancellationToken.IsCancellationRequested)
		{
			channel.Dispose();
			throw new OperationCanceledException(exception.Status.Detail, exception, cancellationToken);
		}
		catch
		{
			channel.Dispose();
			throw;
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

		var request = new V1.LoadModelRequest
		{
			ModelId = modelId,
			ModelPath = options.ModelPath,
		};

		if (options.ContextWindow is { } contextWindow)
		{
			request.ContextWindow = contextWindow;
		}

		if (options.GpuLayerCount is { } gpuLayerCount)
		{
			request.GpuLayerCount = gpuLayerCount;
		}

		try
		{
			await _client
				.LoadModelAsync(request, cancellationToken: cancellationToken)
				.ConfigureAwait(false);
			Volatile.Write(ref _loadedModelId, modelId);
		}
		catch (RpcException exception)
		{
			throw Translate(exception);
		}
	}

	/// <inheritdoc />
	public async Task UnloadAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			await _client
				.UnloadModelAsync(
					new V1.UnloadModelRequest(),
					cancellationToken: cancellationToken)
				.ConfigureAwait(false);
			Volatile.Write(ref _loadedModelId, null);
		}
		catch (RpcException exception)
		{
			throw Translate(exception);
		}
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<GenerationChunk> GenerateAsync(
		GenerationRequest request,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		LocalRuntimeValidation.ValidateGenerationRequest(request);

		var modelId = LoadedModelId
			?? throw new InvalidOperationException("No model is loaded.");

		var message = new V1.GenerateRequest
		{
			ModelId = modelId,
			Prompt = request.Prompt,
			SystemPrompt = request.SystemPrompt ?? string.Empty,
			MaxTokens = request.MaxTokens,
			Temperature = request.Temperature,
		};

		if (request.Seed is { } seed)
		{
			message.Seed = seed;
		}

		using var call = _client.Generate(message, cancellationToken: cancellationToken);

		while (true)
		{
			bool hasNext;

			try
			{
				hasNext = await call.ResponseStream
					.MoveNext(cancellationToken)
					.ConfigureAwait(false);
			}
			catch (RpcException exception)
			{
				throw Translate(exception);
			}

			if (!hasNext)
			{
				yield break;
			}

			yield return new GenerationChunk { Text = call.ResponseStream.Current.Text };
		}
	}

	/// <inheritdoc />
	public async Task<EmbeddingResult> EmbedAsync(
		EmbeddingRequest request,
		CancellationToken cancellationToken = default)
	{
		LocalRuntimeValidation.ValidateEmbeddingRequest(request);

		var modelId = LoadedModelId
			?? throw new InvalidOperationException("No model is loaded.");

		var message = new V1.EmbedRequest { ModelId = modelId };
		message.Inputs.AddRange(request.Inputs);

		V1.EmbedResponse response;

		try
		{
			response = await _client
				.EmbedAsync(message, cancellationToken: cancellationToken)
				.ConfigureAwait(false);
		}
		catch (RpcException exception)
		{
			throw Translate(exception);
		}

		var vectors = new ReadOnlyMemory<float>[response.Vectors.Count];

		for (var index = 0; index < response.Vectors.Count; index++)
		{
			var values = response.Vectors[index].Values;

			if (values.Count != response.Dimensions)
			{
				throw new InvalidDataException(
					"ModelHost returned an embedding vector with an unexpected dimension.");
			}

			vectors[index] = values.ToArray();
		}

		return new EmbeddingResult
		{
			Vectors = vectors,
			Dimensions = response.Dimensions,
		};
	}

	/// <summary>Requests a graceful shutdown of the remote process.</summary>
	/// <param name="timeout">Time allowed for the remote process to accept the request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task ShutdownAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await _client
				.ShutdownAsync(
					new V1.ShutdownRequest(),
					new CallOptions(
						deadline: DateTime.UtcNow.Add(timeout),
						cancellationToken: cancellationToken))
				.ConfigureAwait(false);
		}
		catch (RpcException exception)
		{
			throw Translate(exception);
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await _channel.ShutdownAsync().ConfigureAwait(false);
		_channel.Dispose();
	}

	private static GrpcChannel CreateChannel(ModelHostEndpoint endpoint)
	{
		var handler = new SocketsHttpHandler
		{
			ConnectCallback = (_, cancellationToken) => OpenTransportAsync(endpoint, cancellationToken),
			EnableMultipleHttp2Connections = true,
			PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
		};

		return GrpcChannel.ForAddress(
			"http://localhost",
			new GrpcChannelOptions
			{
				HttpHandler = handler,
				DisposeHttpClient = true,
			});
	}

	private static async ValueTask<Stream> OpenTransportAsync(
		ModelHostEndpoint endpoint,
		CancellationToken cancellationToken)
	{
		if (endpoint.Transport == ModelHostTransport.NamedPipe)
		{
			if (!OperatingSystem.IsWindows())
			{
				throw new PlatformNotSupportedException("Named pipes require Windows.");
			}

			var pipe = new NamedPipeClientStream(
				".",
				endpoint.Address,
				PipeDirection.InOut,
				PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

			try
			{
				await pipe
					.ConnectAsync((int)ConnectTimeout.TotalMilliseconds, cancellationToken)
					.ConfigureAwait(false);
				return pipe;
			}
			catch
			{
				pipe.Dispose();
				throw;
			}
		}

		var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

		try
		{
			await socket
				.ConnectAsync(
					new UnixDomainSocketEndPoint(endpoint.Address),
					cancellationToken)
				.ConfigureAwait(false);
			return new NetworkStream(socket, ownsSocket: true);
		}
		catch
		{
			socket.Dispose();
			throw;
		}
	}

	private static Exception Translate(RpcException exception) => exception.StatusCode switch
	{
		StatusCode.InvalidArgument => new ArgumentException(exception.Status.Detail, exception),
		StatusCode.FailedPrecondition => new InvalidOperationException(exception.Status.Detail, exception),
		StatusCode.NotFound => new KeyNotFoundException(exception.Status.Detail, exception),
		StatusCode.Cancelled => new OperationCanceledException(exception.Status.Detail, exception),
		StatusCode.DeadlineExceeded => new TimeoutException(exception.Status.Detail, exception),
		StatusCode.Unavailable => new ModelHostUnavailableException(
			"ModelHost is unavailable.",
			exception),
		_ => new InvalidOperationException(
			$"ModelHost call failed: {exception.Status.Detail}",
			exception),
	};
}
