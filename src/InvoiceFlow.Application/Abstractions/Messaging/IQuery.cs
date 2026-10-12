namespace InvoiceFlow.Application.Abstractions.Messaging;

/// <summary>
/// A use case that only reads. Its handler goes through a read service and never loads aggregates.
/// </summary>
public interface IQuery<TResult>;
