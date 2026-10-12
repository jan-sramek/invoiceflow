namespace InvoiceFlow.Application.Common;

/// <summary>
/// A value together with the version it was read at. Writes quote the version to prove they saw this state.
/// The version is opaque: compare it, never parse it.
/// </summary>
public sealed record Versioned<T>(T Value, string Version);
