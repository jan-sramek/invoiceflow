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

        if (decimal.Round(amount, MaxDecimals) != amount)
        {
            throw new DomainValidationException(
                DomainErrors.Validation.AmountPrecision,
                new Dictionary<string, object?> { ["maxDecimals"] = MaxDecimals });
        }

        if (Math.Abs(amount) > MaxAbsoluteAmount)
        {
            throw new DomainValidationException(
                DomainErrors.Validation.AmountOutOfRange,
                new Dictionary<string, object?> { ["max"] = MaxAbsoluteAmount });
        }

        return new Money(amount, currency);
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
