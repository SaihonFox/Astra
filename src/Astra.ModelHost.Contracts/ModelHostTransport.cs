namespace Astra.ModelHost.Contracts;

/// <summary>
/// Local transports supported by ModelHost.
/// </summary>
public enum ModelHostTransport
{
	/// <summary>Windows named pipe.</summary>
	NamedPipe,

	/// <summary>Unix domain socket on Linux and macOS.</summary>
	UnixSocket,
}
