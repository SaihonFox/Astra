namespace Astra.Core.Models;

/// <summary>
/// Persists model metadata and reports live file availability.
/// </summary>
/// <remarks>
/// Implementations must be safe for concurrent use. Paths are never used to execute code;
/// they only point at GGUF files that a runtime may load.
/// </remarks>
public interface IModelRegistry
{
	/// <summary>Lists all registered models ordered by display name.</summary>
	Task<IReadOnlyList<ModelDescriptor>> ListAsync(CancellationToken cancellationToken = default);

	/// <summary>Gets a model by identifier, or <see langword="null"/> when it is not registered.</summary>
	Task<ModelDescriptor?> GetAsync(string id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Registers a model and stamps its creation and update times.
	/// </summary>
	/// <returns>The stored descriptor, including registry-managed timestamps.</returns>
	/// <exception cref="ArgumentException">The descriptor is invalid.</exception>
	/// <exception cref="InvalidOperationException">
	/// A model with the same identifier or path already exists.
	/// </exception>
	Task<ModelDescriptor> AddAsync(
		ModelDescriptor descriptor,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Replaces the metadata of a registered model and stamps its update time.
	/// </summary>
	/// <returns>The stored descriptor, including registry-managed timestamps.</returns>
	/// <exception cref="ArgumentException">The descriptor is invalid.</exception>
	/// <exception cref="KeyNotFoundException">The identifier is not registered.</exception>
	/// <exception cref="InvalidOperationException">Another model already uses the path.</exception>
	Task<ModelDescriptor> UpdateAsync(
		ModelDescriptor descriptor,
		CancellationToken cancellationToken = default);

	/// <summary>Deletes a model. Returns <see langword="true"/> when an entry was removed.</summary>
	Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

	/// <summary>
	/// Checks whether the model file currently exists and can be opened for reading.
	/// </summary>
	/// <exception cref="KeyNotFoundException">The identifier is not registered.</exception>
	Task<ModelAvailability> CheckAvailabilityAsync(
		string id,
		CancellationToken cancellationToken = default);
}
