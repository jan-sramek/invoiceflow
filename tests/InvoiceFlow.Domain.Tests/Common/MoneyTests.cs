using System.Globalization;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Domain.Tests.Common;

public class MoneyTests
{
    private static readonly CurrencyCode Eur = CurrencyCode.Create("EUR");
    private static readonly CurrencyCode Czk = CurrencyCode.Create("CZK");

    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("1234.5")]
    [InlineData("1234.50")]
    [InlineData("9999999999999999.99")]
    public void Create_AtMostTwoDecimalsWithinRange_Succeeds(string amount)
    {
        var money = Money.Create(decimal.Parse(amount, CultureInfo.InvariantCulture), Eur);

        money.Amount.ShouldBe(decimal.Parse(amount, CultureInfo.InvariantCulture));
        money.Currency.ShouldBe(Eur);
    }

    [Fact]
    public void Create_TrailingZerosBeyondTwoDecimals_Succeeds()
    {
        Money.Create(10.500m, Eur).Amount.ShouldBe(10.5m);
    }

    [Fact]
    public void Create_MoreThanTwoDecimals_Throws()
    {
        DomainValidationException exception = Should.Throw<DomainValidationException>(() => Money.Create(10.005m, Eur));

        exception.Code.ShouldBe(DomainErrors.Validation.AmountPrecision);
        exception.Params["maxDecimals"].ShouldBe(2);
    }

    [Theory]
    [InlineData("10000000000000000")]
    [InlineData("-10000000000000000")]
    public void Create_OutsideStorageRange_Throws(string amount)
    {
        DomainValidationException exception = Should.Throw<DomainValidationException>(
            () => Money.Create(decimal.Parse(amount, CultureInfo.InvariantCulture), Eur));

        exception.Code.ShouldBe(DomainErrors.Validation.AmountOutOfRange);
    }

    [Fact]
    public void Equality_SameValueDifferentScale_AreEqual()
    {
        Money.Create(10.5m, Eur).ShouldBe(Money.Create(10.50m, Eur));
    }

    [Fact]
    public void Equality_SameAmountDifferentCurrency_AreNotEqual()
    {
        Money.Create(10m, Eur).ShouldNotBe(Money.Create(10m, Czk));
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSum()
    {
        Money sum = Money.Create(100.10m, Eur).Add(Money.Create(21.02m, Eur));

        sum.ShouldBe(Money.Create(121.12m, Eur));
    }

    [Fact]
    [Trait("Rule", "BR-06")]
    public void Add_DifferentCurrency_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Money.Create(1m, Eur).Add(Money.Create(1m, Czk)));
    }

    [Theory]
    [InlineData("100.01", "100", true)]
    [InlineData("100", "100", false)]
    [InlineData("99.99", "100", false)]
    public void IsGreaterThan_SameCurrency_ComparesAmounts(string left, string right, bool expected)
    {
        var a = Money.Create(decimal.Parse(left, CultureInfo.InvariantCulture), Eur);
        var b = Money.Create(decimal.Parse(right, CultureInfo.InvariantCulture), Eur);

        a.IsGreaterThan(b).ShouldBe(expected);
    }

    [Fact]
    [Trait("Rule", "BR-06")]
    public void IsGreaterThan_DifferentCurrency_Throws()
    {
        Should.Throw<InvalidOperationException>(() => Money.Create(1m, Eur).IsGreaterThan(Money.Create(1m, Czk)));
    }

    [Fact]
    public void ToString_AnyCulture_UsesInvariantFormat()
    {
        Money.Create(1234.5m, Eur).ToString().ShouldBe("1234.50 EUR");
    }
}
