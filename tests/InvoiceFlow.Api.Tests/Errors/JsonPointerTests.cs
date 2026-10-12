using InvoiceFlow.Api.Errors;

namespace InvoiceFlow.Api.Tests.Errors;

public class JsonPointerTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("$", "")]
    [InlineData("$.name", "/name")]
    [InlineData("$.lines[0].amount", "/lines/0/amount")]
    [InlineData("Lines[2].Amount", "/lines/2/amount")]
    [InlineData("TotalAmount", "/totalAmount")]
    [InlineData("a/b~c", "/a~1b~0c")]
    public void FromPath_ReturnsCamelCasePointer(string path, string expected)
    {
        JsonPointer.FromPath(path).ShouldBe(expected);
    }
}
