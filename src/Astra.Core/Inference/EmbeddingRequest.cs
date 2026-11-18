namespace Astra.Core.Inference;

/// <summary>
/// Inputs for one embedding request.
/// </summary>
public sealed record EmbeddingRequest
{
	/// <summary>Gets the texts to embed.</summary>
	public required IReadOnlyList<string> Inputs { get; init; }
}
