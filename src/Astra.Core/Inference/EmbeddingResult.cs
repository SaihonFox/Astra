namespace Astra.Core.Inference;

/// <summary>
/// Embedding vectors aligned with <see cref="EmbeddingRequest.Inputs"/>.
/// </summary>
public sealed record EmbeddingResult
{
	/// <summary>Gets the embedding vectors in input order.</summary>
	public required IReadOnlyList<ReadOnlyMemory<float>> Vectors { get; init; }

	/// <summary>Gets the vector size shared by all vectors.</summary>
	public required int Dimensions { get; init; }
}
