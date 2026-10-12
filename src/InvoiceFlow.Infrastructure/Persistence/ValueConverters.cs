using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InvoiceFlow.Infrastructure.Persistence;

internal sealed class TenantIdConverter() : ValueConverter<TenantId, Guid>(id => id.Value, value => new TenantId(value));

internal sealed class InvoiceIdConverter() : ValueConverter<InvoiceId, Guid>(id => id.Value, value => new InvoiceId(value));

internal sealed class CurrencyCodeConverter()
    : ValueConverter<CurrencyCode, string>(code => code.Value, value => CurrencyCode.Create(value));

internal sealed class SupplierNameConverter()
    : ValueConverter<SupplierName, string>(name => name.Value, value => SupplierName.Create(value));

internal sealed class InvoiceNumberConverter()
    : ValueConverter<InvoiceNumber, string>(number => number.Value, value => InvoiceNumber.Create(value));

internal sealed class PurchaseOrderReferenceConverter()
    : ValueConverter<PurchaseOrderReference, string>(reference => reference.Value, value => PurchaseOrderReference.Create(value));
