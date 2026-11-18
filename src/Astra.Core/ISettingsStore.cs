namespace Astra.Core;

/// <summary>
/// Stores small, non-secret application settings as string values.
/// </summary>
/// <remarks>
/// Structured values are serialized by the caller, typically as JSON. Secrets must use an OS
/// secret store instead (see ADR 0002).
/// </remarks>
public interface ISettingsStore
{
	/// <summary>Gets a setting value, or <see langword="null"/> when it does not exist.</summary>
	Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

	/// <summary>Creates or replaces a setting value.</summary>
	Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

	/// <summary>Deletes a setting. Returns <see langword="true"/> when a value was removed.</summary>
	Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}
