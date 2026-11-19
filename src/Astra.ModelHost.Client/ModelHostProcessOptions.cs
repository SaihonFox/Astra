namespace Astra.ModelHost.Client;

/// <summary>
/// Tunables for starting a ModelHost process.
/// </summary>
public sealed record ModelHostProcessOptions
{
	/// <summary>Gets the logical instance name used to isolate IPC endpoints.</summary>
	public string InstanceName { get; init; } = "default";

	/// <summary>Gets the maximum time to wait for the handshake.</summary>
	public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(20);

	/// <summary>
	/// Gets the runtime to start: <c>llama</c> (default) or <c>echo</c>. Tests use <c>echo</c>
	/// so CI does not need model files or native inference.
	/// </summary>
	public string? RuntimeName { get; init; }

	/// <summary>Gets the largest model file the runtime may load, in bytes.</summary>
	public long? MaxModelBytes { get; init; }
}
