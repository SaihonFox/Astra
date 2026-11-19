namespace Astra.ModelHost.Client;

/// <summary>
/// The ModelHost process could not be reached, typically because it exited or is restarting.
/// </summary>
public sealed class ModelHostUnavailableException : InvalidOperationException
{
	/// <summary>Initializes a new instance of the <see cref="ModelHostUnavailableException"/> class.</summary>
	/// <param name="message">Message.</param>
	/// <param name="innerException">Underlying transport failure.</param>
	public ModelHostUnavailableException(string message, Exception? innerException = null)
		: base(message, innerException)
	{
	}
}
