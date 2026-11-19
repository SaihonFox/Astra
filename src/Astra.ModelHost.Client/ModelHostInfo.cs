namespace Astra.ModelHost.Client;

/// <summary>
/// Information returned by the ModelHost handshake.
/// </summary>
/// <param name="ProtocolVersion">Wire protocol version.</param>
/// <param name="RuntimeName">Runtime implementation name.</param>
/// <param name="LoadedModelId">Loaded model identifier, or <see langword="null"/>.</param>
/// <param name="ProcessId">Operating-system process identifier.</param>
public sealed record ModelHostInfo(
	string ProtocolVersion,
	string RuntimeName,
	string? LoadedModelId,
	int ProcessId);
