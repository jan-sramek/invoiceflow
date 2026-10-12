using InvoiceFlow.Api.Http;

namespace InvoiceFlow.Api.Tests.Http;

public class EntityTagsTests
{
    [Fact]
    public void RequiredVersion_StrongTag_ReturnsVersion()
    {
        EntityTags.RequiredVersion("\"1234\"").ShouldBe("1234");
    }

    [Fact]
    public void From_Version_IsStrongQuotedTag()
    {
        EntityTags.From("1234").ToString().ShouldBe("\"1234\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("W/\"1234\"")]
    [InlineData("*")]
    [InlineData("\"1234\", \"5678\"")]
    public void RequiredVersion_NoSingleStrongTag_IsRequired(string? ifMatch)
    {
        Should.Throw<IfMatchRequiredException>(() => EntityTags.RequiredVersion(ifMatch));
    }
}
