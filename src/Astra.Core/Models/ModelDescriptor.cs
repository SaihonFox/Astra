namespace Astra.Core.Models;

/// <summary>
/// Metadata describing one locally available model.
/// </summary>
/// <remarks>
/// The descriptor contains no secrets. <see cref="CreatedAt"/> and <see cref="UpdatedAt"/> are
/// managed by <see cref="IModelRegistry"/>; values supplied by callers are ignored on write.
/// </remarks>
public sealed record ModelDescriptor
{
	/// <summary>Gets the stable, lowercase identifier used across the system.</summary>
	public required string Id { get; init; }

	/// <summary>Gets the human-readable display name.</summary>
	public required string Name { get; init; }

	/// <summary>Gets the absolute path of the model file.</summary>
	public required string Path { get; init; }

	/// <summary>Gets the model file format. Defaults to <see cref="ModelFormat.Gguf"/>.</summary>
	public ModelFormat Format { get; init; } = ModelFormat.Gguf;

	/// <summary>Gets the quantization label, for example <c>Q4_K_M</c>.</summary>
	public string? Quantization { get; init; }

	/// <summary>Gets the model file size in bytes.</summary>
	public required long SizeBytes { get; init; }

	/// <summary>Gets the maximum context window in tokens, when known.</summary>
	public int? ContextWindow { get; init; }

	/// <summary>Gets the declared capabilities.</summary>
	public ModelCapabilities Capabilities { get; init; } = ModelCapabilities.None;

	/// <summary>Gets the origin of the model, typically a repository or catalog URL.</summary>
	public string? Source { get; init; }

	/// <summary>Gets the license identifier or short license name.</summary>
	public string? License { get; init; }

	/// <summary>Gets the estimated RAM requirement in bytes, when known.</summary>
	public long? RequiredRamBytes { get; init; }

	/// <summary>Gets the estimated VRAM requirement in bytes, when known.</summary>
	public long? RequiredVramBytes { get; init; }

	/// <summary>Gets a free-form hint about preferred tasks, for example <c>chat, code</c>.</summary>
	public string? PreferredTasks { get; init; }

	/// <summary>Gets the upstream model version or revision, when known.</summary>
	public string? ModelVersion { get; init; }

	/// <summary>Gets the last validation outcome.</summary>
	public ModelTestStatus TestStatus { get; init; } = ModelTestStatus.Untested;

	/// <summary>Gets the UTC time of the last validation, when known.</summary>
	public DateTimeOffset? TestedAt { get; init; }

	/// <summary>Gets short notes about the last validation.</summary>
	public string? TestNotes { get; init; }

	/// <summary>Gets the UTC time at which the registry entry was created.</summary>
	public DateTimeOffset CreatedAt { get; init; }

	/// <summary>Gets the UTC time at which the registry entry was last changed.</summary>
	public DateTimeOffset UpdatedAt { get; init; }
}
