using System.Globalization;
using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Domain.Common;

public sealed record Money
{
    public const int MaxDecimals = 2;

    // Matches the numeric(18,2) column the amounts are stored in.
    public const decimal MaxAbsoluteAmount = 9_999_999_999_999_999.99m;

    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public CurrencyCode Currency { get; }

    public static Money Create(decimal amount, CurrencyCode currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        return new Money(ValidAmount(amount), currency);
    }

    internal static decimal ValidAmount(decimal amount)
    {
        // Round tiny binary/JSON noise to money scale; reject values that clearly use more than 2 dp.
        decimal rounded = decimal.Round(amount, MaxDecimals, MidpointRounding.AwayFromZero);
        if (Math.Abs(amount - rounded) > 0.000_000_1m)
        {
            throw new DomainValidationException(
                DomainErrors.Validation.AmountPrecision,
                new Dictionary<string, object?> { ["maxDecimals"] = MaxDecimals });
        }

        if (Math.Abs(rounded) > MaxAbsoluteAmount)
        {
            throw new DomainValidationException(
                DomainErrors.Validation.AmountOutOfRange,
                new Dictionary<string, object?> { ["max"] = MaxAbsoluteAmount });
        }

        return rounded;
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return Create(Amount + other.Amount, Currency);
    }

    public bool IsGreaterThan(Money other)
    {
        EnsureSameCurrency(other);
        return Amount > other.Amount;
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other.Currency != Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine amounts in {Currency} and {other.Currency}.");
        }
    }
}
