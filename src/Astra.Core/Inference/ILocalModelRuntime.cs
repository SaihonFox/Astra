namespace Astra.Core.Inference;

/// <summary>
/// Runs one locally loaded GGUF model.
/// </summary>
/// <remarks>
/// At most one model is loaded at a time. Implementations isolate native failures from the
/// caller, honor cancellation and never access the network. The contract is consumed by Agent
/// Core; native runtime types must not leak through it.
/// </remarks>
public interface ILocalModelRuntime : IAsyncDisposable
{
	/// <summary>Gets the identifier of the loaded model, or <see langword="null"/> when none is loaded.</summary>
	string? LoadedModelId { get; }

	/// <summary>
	/// Loads a model. Loading while another model is loaded is rejected; unload it first.
	/// </summary>
	/// <param name="modelId">Registered model identifier.</param>
	/// <param name="options">Load parameters, including the resolved absolute model path.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <exception cref="ArgumentException"><paramref name="modelId"/> or <paramref name="options"/> is invalid.</exception>
	/// <exception cref="InvalidOperationException">Another model is already loaded.</exception>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	Task LoadAsync(
		string modelId,
		ModelLoadOptions options,
		CancellationToken cancellationToken = default);

	/// <summary>Unloads the current model. Does nothing when no model is loaded.</summary>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	Task UnloadAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Streams generated text from the loaded model.
	/// </summary>
	/// <exception cref="ArgumentException"><paramref name="request"/> is invalid.</exception>
	/// <exception cref="InvalidOperationException">No model is loaded.</exception>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	IAsyncEnumerable<GenerationChunk> GenerateAsync(
		GenerationRequest request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates embeddings with the loaded model.
	/// </summary>
	/// <exception cref="ArgumentException"><paramref name="request"/> is invalid.</exception>
	/// <exception cref="InvalidOperationException">
	/// No model is loaded, or the loaded model does not support embeddings.
	/// </exception>
	/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
	Task<EmbeddingResult> EmbedAsync(
		EmbeddingRequest request,
		CancellationToken cancellationToken = default);
}
