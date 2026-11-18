namespace Astra.Core.Inference;

/// <summary>
/// Optional overrides applied when a model is loaded.
/// </summary>
public sealed record ModelLoadOptions
{
	/// <summary>Gets the context window override in tokens.</summary>
	public int? ContextWindow { get; init; }

	/// <summary>Gets the number of layers offloaded to the GPU.</summary>
	public int? GpuLayerCount { get; init; }
}
