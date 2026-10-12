using System.Collections.Concurrent;
using System.Diagnostics;
using InvoiceFlow.Application.Behaviors;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Application.Invoices.GetInvoice;
using Microsoft.Extensions.Logging.Testing;

namespace InvoiceFlow.Application.Tests.Behaviors;

public sealed class UseCaseExecutionTests : IDisposable
{
    private readonly ConcurrentQueue<Activity> _activities = new();
    private readonly ActivityListener _listener;
    private readonly string _useCase = $"Test{Guid.NewGuid():N}";

    public UseCaseExecutionTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "InvoiceFlow.Application",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _activities.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public async Task Run_Succeeds_RecordsSpanNamedAfterUseCase()
    {
        await UseCaseExecution.RunAsync(_useCase, new FakeLogger(), () => Task.FromResult(1));

        Activity span = Span();
        span.Status.ShouldBe(ActivityStatusCode.Unset);
        span.GetTagItem("error.code").ShouldBeNull();
    }

    [Fact]
    public async Task Run_Rejected_TagsSpanWithErrorCodeOnly()
    {
        await Should.ThrowAsync<InvoiceNotFoundException>(() =>
            UseCaseExecution.RunAsync<int>(_useCase, new FakeLogger(), () => throw new InvoiceNotFoundException()));

        Activity span = Span();
        span.GetTagItem("error.code").ShouldBe("invoice.not_found");
        span.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task Run_Unexpected_MarksSpanFailedWithoutLogging()
    {
        var logger = new FakeLogger();

        await Should.ThrowAsync<InvalidOperationException>(() =>
            UseCaseExecution.RunAsync<int>(_useCase, logger, () => throw new InvalidOperationException()));

        Activity span = Span();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.Events.ShouldContain(e => e.Name == "exception");
        logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Run_Cancelled_LeavesSpanUnmarked()
    {
        await Should.ThrowAsync<OperationCanceledException>(() =>
            UseCaseExecution.RunAsync<int>(_useCase, new FakeLogger(), () => throw new OperationCanceledException()));

        Span().Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Theory]
    [InlineData(typeof(GetInvoiceQuery), "GetInvoice")]
    [InlineData(typeof(ApproveInvoiceCommand), "ApproveInvoice")]
    [InlineData(typeof(Query), "Query")]
    [InlineData(typeof(UseCaseExecutionTests), "UseCaseExecutionTests")]
    public void NameOf_DropsCommandOrQuerySuffix(Type request, string expected)
    {
        UseCaseExecution.NameOf(request).ShouldBe(expected);
    }

    public void Dispose() => _listener.Dispose();

    private Activity Span() => _activities.Where(a => a.OperationName == _useCase).ShouldHaveSingleItem();

    private sealed record ApproveInvoiceCommand;

    private sealed record Query;
}
