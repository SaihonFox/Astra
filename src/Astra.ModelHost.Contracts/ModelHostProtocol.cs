namespace Astra.ModelHost.Contracts;

/// <summary>
/// Version information shared by the ModelHost client and server.
/// </summary>
public static class ModelHostProtocol
{
	/// <summary>Wire protocol version negotiated during the handshake.</summary>
	public const string Version = "1.0";

	/// <summary>Major protocol version; incompatible when the peer differs.</summary>
	public const int MajorVersion = 1;
}
