using Astra.Core.Inference;
using Astra.ModelHost.Contracts;
using Grpc.Core;
using V1 = Astra.ModelHost.Contracts.V1;

namespace Astra.ModelHost.Services;

/// <summary>
/// gRPC surface over <see cref="ILocalModelRuntime"/>.
/// </summary>
internal sealed class ModelHostService : V1.ModelHost.ModelHostBase
{
	private readonly ILocalModelRuntime _runtime;
	private readonly ModelHostRuntimeInfo _info;
	private readonly IHostApplicationLifetime _lifetime;

	public ModelHostService(
		ILocalModelRuntime runtime,
		ModelHostRuntimeInfo info,
		IHostApplicationLifetime lifetime)
	{
		_runtime = runtime;
		_info = info;
		_lifetime = lifetime;
	}

	public override Task<V1.GetInfoResponse> GetInfo(
		V1.GetInfoRequest request,
		ServerCallContext context)
	{
		ArgumentNullException.ThrowIfNull(request);

		return Task.FromResult(new V1.GetInfoResponse
		{
			ProtocolVersion = ModelHostProtocol.Version,
			RuntimeName = _info.RuntimeName,
			LoadedModelId = _runtime.LoadedModelId ?? string.Empty,
			ProcessId = Environment.ProcessId,
		});
	}

	public override async Task<V1.LoadModelResponse> LoadModel(
		V1.LoadModelRequest request,
		ServerCallContext context)
	{
		ArgumentNullException.ThrowIfNull(request);

		var options = new ModelLoadOptions
		{
			ModelPath = request.ModelPath,
			ContextWindow = request.HasContextWindow ? request.ContextWindow : null,
			GpuLayerCount = request.HasGpuLayerCount ? request.GpuLayerCount : null,
		};

		try
		{
			await _runtime.LoadAsync(
				request.ModelId,
				options,
				context.CancellationToken);

			return new V1.LoadModelResponse();
		}
		catch (Exception exception) when (IsMappable(exception))
		{
			throw MapFailure(exception);
		}
	}

	public override async Task<V1.UnloadModelResponse> UnloadModel(
		V1.UnloadModelRequest request,
		ServerCallContext context)
	{
		ArgumentNullException.ThrowIfNull(request);

		try
		{
			await _runtime.UnloadAsync(context.CancellationToken);
			return new V1.UnloadModelResponse();
		}
		catch (Exception exception) when (IsMappable(exception))
		{
			throw MapFailure(exception);
		}
	}

	public override async Task Generate(
		V1.GenerateRequest request,
		IServerStreamWriter<V1.GenerateChunk> responseStream,
		ServerCallContext context)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(responseStream);

		var loadedModelId = _runtime.LoadedModelId;
		if (loadedModelId is null)
		{
			throw new RpcException(
				new Status(StatusCode.FailedPrecondition, "No model is loaded."));
		}

		if (!string.Equals(loadedModelId, request.ModelId, StringComparison.Ordinal))
		{
			throw new RpcException(new Status(
				StatusCode.FailedPrecondition,
				$"Model '{request.ModelId}' is not loaded; '{loadedModelId}' is active."));
		}

		var generationRequest = new GenerationRequest
		{
			Prompt = request.Prompt,
			SystemPrompt = string.IsNullOrEmpty(request.SystemPrompt) ? null : request.SystemPrompt,
			MaxTokens = request.MaxTokens,
			Temperature = request.Temperature,
			Seed = request.HasSeed ? request.Seed : null,
		};

		try
		{
			await foreach (var chunk in _runtime
				.GenerateAsync(generationRequest, context.CancellationToken)
				.ConfigureAwait(false))
			{
				await responseStream
					.WriteAsync(new V1.GenerateChunk { Text = chunk.Text }, context.CancellationToken)
					.ConfigureAwait(false);
			}
		}
		catch (Exception exception) when (IsMappable(exception))
		{
			throw MapFailure(exception);
		}
	}

	public override async Task<V1.EmbedResponse> Embed(
		V1.EmbedRequest request,
		ServerCallContext context)
	{
		ArgumentNullException.ThrowIfNull(request);

		var loadedModelId = _runtime.LoadedModelId;
		if (loadedModelId is null)
		{
			throw new RpcException(
				new Status(StatusCode.FailedPrecondition, "No model is loaded."));
		}

		if (!string.Equals(loadedModelId, request.ModelId, StringComparison.Ordinal))
		{
			throw new RpcException(new Status(
				StatusCode.FailedPrecondition,
				$"Model '{request.ModelId}' is not loaded; '{loadedModelId}' is active."));
		}

		try
		{
			var result = await _runtime.EmbedAsync(
				new EmbeddingRequest { Inputs = request.Inputs },
				context.CancellationToken);

			var response = new V1.EmbedResponse { Dimensions = result.Dimensions };

			foreach (var vector in result.Vectors)
			{
				var message = new V1.EmbeddingVector();
				message.Values.AddRange(vector.ToArray());
				response.Vectors.Add(message);
			}

			return response;
		}
		catch (Exception exception) when (IsMappable(exception))
		{
			throw MapFailure(exception);
		}
	}

	public override Task<V1.ShutdownResponse> Shutdown(
		V1.ShutdownRequest request,
		ServerCallContext context)
	{
		ArgumentNullException.ThrowIfNull(request);

		_ = Task.Run(async () =>
		{
			await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
			_lifetime.StopApplication();
		});

		return Task.FromResult(new V1.ShutdownResponse());
	}

	private static bool IsMappable(Exception exception) =>
		exception is ArgumentException or InvalidOperationException or OperationCanceledException;

	private static RpcException MapFailure(Exception exception) => exception switch
	{
		OperationCanceledException => new RpcException(
			new Status(StatusCode.Cancelled, "The operation was cancelled.")),
		ArgumentException => new RpcException(
			new Status(StatusCode.InvalidArgument, exception.Message)),
		InvalidOperationException => new RpcException(
			new Status(StatusCode.FailedPrecondition, exception.Message)),
		_ => new RpcException(
			new Status(StatusCode.Internal, "The model runtime failed.")),
	};
}
