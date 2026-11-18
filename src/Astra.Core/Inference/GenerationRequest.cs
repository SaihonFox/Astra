namespace Astra.Core.Inference;

/// <summary>
/// Parameters for one text generation request against the loaded model.
/// </summary>
public sealed record GenerationRequest
{
	/// <summary>Gets the user prompt.</summary>
	public required string Prompt { get; init; }

	/// <summary>Gets an optional system prompt.</summary>
	public string? SystemPrompt { get; init; }

	/// <summary>Gets the maximum number of generated tokens. Defaults to 512.</summary>
	public int MaxTokens { get; init; } = 512;

	/// <summary>Gets the sampling temperature. Defaults to 0.8.</summary>
	public float Temperature { get; init; } = 0.8f;

	/// <summary>Gets an optional sampling seed for reproducible output.</summary>
	public int? Seed { get; init; }
}
