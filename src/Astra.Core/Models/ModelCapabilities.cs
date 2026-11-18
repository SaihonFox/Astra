namespace Astra.Core.Models;

/// <summary>
/// Capabilities a model can provide. Stored as a bit mask.
/// </summary>
[Flags]
public enum ModelCapabilities
{
	/// <summary>No declared capability.</summary>
	None = 0,

	/// <summary>Instruction/chat style generation.</summary>
	Chat = 1 << 0,

	/// <summary>Plain text completion.</summary>
	Completion = 1 << 1,

	/// <summary>Embedding generation.</summary>
	Embedding = 1 << 2,

	/// <summary>Image understanding.</summary>
	Vision = 1 << 3,
}
