namespace Astra.Core.Models;

/// <summary>
/// Live availability of a registered model file.
/// </summary>
public enum ModelAvailability
{
	/// <summary>Availability could not be determined.</summary>
	Unknown,

	/// <summary>The model file exists and can be opened for reading.</summary>
	Available,

	/// <summary>The model file is missing.</summary>
	Missing,

	/// <summary>The model file exists but cannot be read.</summary>
	Unreadable,
}
