using System.Collections.Concurrent;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Tenants;
using Npgsql;
using Polly;
using Polly.CircuitBreaker;

namespace InvoiceFlow.Infrastructure.Tenancy;

/// <summary>
/// Per-tenant circuit breakers so one unreachable on-prem database does not stall other tenants.
/// </summary>
internal sealed class TenantDatabaseResilience
{
    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();

    public CircuitBreakerStateProvider StateOf(TenantId tenantId) => Get(tenantId).State;

    public ResiliencePipeline PipelineOf(TenantId tenantId) => Get(tenantId).Pipeline;

    public IEnumerable<(Guid TenantId, bool Open)> BreakerOpenSnapshot() =>
        _entries.Select(pair => (
            pair.Key,
            pair.Value.State.CircuitState is CircuitState.Open or CircuitState.Isolated));

    public async Task RecordSuccessAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await PipelineOf(tenantId).ExecuteAsync(_ => ValueTask.CompletedTask, cancellationToken);

    public async Task RecordFailureAsync(TenantId tenantId, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            await PipelineOf(tenantId).ExecuteAsync(
                _ => ValueTask.FromException(exception),
                cancellationToken);
        }
        catch (BrokenCircuitException)
        {
            // Failure already recorded; circuit may now be open.
        }
        catch (Exception caught) when (caught == exception || caught.InnerException == exception)
        {
            // Expected: the pipeline rethrows the recorded failure.
        }
    }

    private Entry Get(TenantId tenantId) =>
        _entries.GetOrAdd(tenantId.Value, static _ => Create());

    private static Entry Create()
    {
        var state = new CircuitBreakerStateProvider();
        ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                // Trip quickly on repeated connect failures (on-prem down / wrong cert).
                FailureRatio = 1.0,
                MinimumThroughput = 2,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<NpgsqlException>()
                    .Handle<TimeoutException>()
                    .Handle<TenantUnavailableException>(),
                StateProvider = state,
            })
            .Build();

        return new Entry(pipeline, state);
    }

    private sealed record Entry(ResiliencePipeline Pipeline, CircuitBreakerStateProvider State);
}
