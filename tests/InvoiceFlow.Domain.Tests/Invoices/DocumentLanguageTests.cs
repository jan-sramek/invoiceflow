using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Domain.Tests.Invoices;

public class DocumentLanguageTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("cs", "cs")]
    [InlineData("fr", "fr")]
    [InlineData("pl", "pl")]
    [InlineData("zh-Hans", "zh-Hans")]
    [InlineData("Portuguese", "pt")]
    [InlineData("Deutsch", "de")]
    public void Normalize_AnyLanguage_KeepsOrResolvesTag(string input, string expected) =>
        DocumentLanguage.Normalize(input).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Normalize_Unknown_ReturnsNull(string? input) =>
        DocumentLanguage.Normalize(input).ShouldBeNull();
}
