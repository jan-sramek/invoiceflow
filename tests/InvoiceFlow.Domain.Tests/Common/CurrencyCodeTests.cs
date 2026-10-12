using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Domain.Tests.Common;

[Trait("Rule", "BR-06")]
public class CurrencyCodeTests
{
    [Theory]
    [InlineData("EUR")]
    [InlineData("CZK")]
    [InlineData("USD")]
    [InlineData("JPY")]
    public void Create_ActiveIsoCode_ReturnsCode(string code)
    {
        var currency = CurrencyCode.Create(code);

        currency.Value.ShouldBe(code);
    }

    [Theory]
    [InlineData("eur")]
    [InlineData(" Eur ")]
    public void Create_DifferentCaseOrSpaces_IsNormalised(string code)
    {
        CurrencyCode.Create(code).Value.ShouldBe("EUR");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("XYZ")]
    [InlineData("HRK")]
    [InlineData("XAU")]
    public void Create_UnknownWithdrawnOrNonCurrencyCode_Throws(string? code)
    {
        DomainValidationException exception = Should.Throw<DomainValidationException>(() => CurrencyCode.Create(code));

        exception.Code.ShouldBe(DomainErrors.Validation.CurrencyInvalid);
        exception.Kind.ShouldBe(ErrorKind.Validation);
    }

    [Fact]
    public void Equality_SameCode_AreEqual()
    {
        CurrencyCode.Create("eur").ShouldBe(CurrencyCode.Create("EUR"));
    }
}
