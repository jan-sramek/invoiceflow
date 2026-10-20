using InvoiceFlow.Infrastructure.Messaging;

namespace InvoiceFlow.Worker.Tests;

public class RetryRoutingTests
{
    [Theory]
    [InlineData(0, "retry.10s")]
    [InlineData(1, "retry.1m")]
    [InlineData(2, "retry.5m")]
    public void NextRetryRoutingKey_MapsAttemptToTier(int attempt, string expected) =>
        RetryRouting.NextRetryRoutingKey(attempt).ShouldBe(expected);

    [Fact]
    public void NextRetryRoutingKey_Exhausted_ReturnsNull()
    {
        RetryRouting.NextRetryRoutingKey(3).ShouldBeNull();
        RetryRouting.IsExhausted(3).ShouldBeTrue();
        RetryRouting.IsExhausted(0).ShouldBeFalse();
    }
}
