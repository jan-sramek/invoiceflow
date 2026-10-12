namespace InvoiceFlow.Application.Abstractions.Messaging;

/// <summary>
/// A use case that changes state. Its handler runs inside a unit of work that commits after it succeeds.
/// </summary>
public interface ICommand<TResult>;
