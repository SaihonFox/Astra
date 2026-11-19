namespace Astra.ModelHost.Services;

/// <summary>
/// Resource limits enforced by the process before loading a model.
/// </summary>
/// <param name="MaxModelBytes">Largest accepted model file size, or <see langword="null"/>.</param>
internal sealed record ModelHostRuntimeLimits(long? MaxModelBytes);
