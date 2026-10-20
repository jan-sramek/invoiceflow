using System.Diagnostics;
using InvoiceFlow.Infrastructure.Messaging;

namespace InvoiceFlow.Worker.Tests;

public class MessagingTraceContextTests
{
    [Fact]
    public void CaptureJson_WithCurrentActivity_RoundTripsTraceParent()
    {
        // Listen by name before touching MessagingTraceContext so its static ActivitySource can initialise.
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "InvoiceFlow.Messaging",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using Activity? root = MessagingTraceContext.ActivitySource.StartActivity("upload");
        root.ShouldNotBeNull();

        string? json = MessagingTraceContext.CaptureJson();
        json.ShouldNotBeNull();

        Dictionary<string, object?>? rabbit = MessagingTraceContext.ToRabbitHeaders(json);
        rabbit.ShouldNotBeNull();
        rabbit.ShouldContainKey(MessagingTraceContext.TraceParentHeader);

        using Activity? consumer = MessagingTraceContext.StartConsumerActivity("invoice.uploaded", rabbit);
        consumer.ShouldNotBeNull();
        consumer.TraceId.ShouldBe(root.TraceId);
        consumer.ParentSpanId.ShouldBe(root.SpanId);
    }

    [Fact]
    public void CaptureJson_WithoutActivity_ReturnsNull()
    {
        Activity.Current = null;
        MessagingTraceContext.CaptureJson().ShouldBeNull();
    }

    [Fact]
    public void CopyTraceHeaders_PreservesTraceParent()
    {
        var source = new Dictionary<string, object?>
        {
            [MessagingTraceContext.TraceParentHeader] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
            [InvoiceFlowTopology.AttemptHeader] = 2,
        };
        var destination = new Dictionary<string, object?>();

        MessagingTraceContext.CopyTraceHeaders(source, destination);

        destination.ShouldContainKey(MessagingTraceContext.TraceParentHeader);
        destination.ShouldNotContainKey(InvoiceFlowTopology.AttemptHeader);
    }
}
