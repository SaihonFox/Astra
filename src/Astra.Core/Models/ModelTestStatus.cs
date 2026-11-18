namespace Astra.Core.Models;

/// <summary>
/// Outcome of the last manual or automated model validation.
/// </summary>
public enum ModelTestStatus
{
	/// <summary>The model has not been validated yet.</summary>
	Untested,

	/// <summary>The model loaded and produced output during validation.</summary>
	Passed,

	/// <summary>The model failed validation; <see cref="ModelDescriptor.TestNotes"/> may explain why.</summary>
	Failed,
}
