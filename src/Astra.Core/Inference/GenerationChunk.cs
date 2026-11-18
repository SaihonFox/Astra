namespace Astra.Core.Inference;

/// <summary>
/// One streamed piece of generated text. The end of the stream marks completion.
/// </summary>
public sealed record GenerationChunk
{
	/// <summary>Gets the generated text fragment. Never empty.</summary>
	public required string Text { get; init; }
}
