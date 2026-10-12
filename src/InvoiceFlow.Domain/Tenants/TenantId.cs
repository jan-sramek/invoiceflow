namespace InvoiceFlow.Domain.Tenants;

public readonly record struct TenantId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
