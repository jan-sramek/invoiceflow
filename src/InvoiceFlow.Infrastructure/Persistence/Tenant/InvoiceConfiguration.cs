using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Persistence.Tenant;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public const string VersionProperty = "Version";

    private const string DocumentInvoiceIdProperty = "InvoiceId";

    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.Ignore(i => i.DomainEvents);

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property<TenantId>(TenantDbContext.TenantIdProperty);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(32);

        // Fields keep their plain names instead of the default fields_ prefix.
        builder.ComplexProperty(i => i.Fields, fields =>
        {
            fields.Property(f => f.SupplierName).HasColumnName("supplier_name");
            fields.Property(f => f.InvoiceNumber).HasColumnName("invoice_number");
            fields.Property(f => f.InvoiceDate).HasColumnName("invoice_date");
            fields.Property(f => f.DueDate).HasColumnName("due_date");
            fields.Property(f => f.Currency).HasColumnName("currency");
            fields.Property(f => f.Subtotal).HasColumnName("subtotal");
            fields.Property(f => f.TaxAmount).HasColumnName("tax_amount");
            fields.Property(f => f.TotalAmount).HasColumnName("total_amount");
            fields.Property(f => f.PurchaseOrderReference).HasColumnName("purchase_order_reference");
        });

        builder.ComplexProperty(i => i.UploadedBy, ActorMapping.Configure);
        builder.Property(i => i.UploadedAt);
        builder.ComplexProperty(i => i.SubmittedBy, ActorMapping.Configure);
        builder.Property(i => i.PaymentReference).HasMaxLength(Invoice.MaxPaymentReferenceLength);

        builder.OwnsOne(i => i.Document, document =>
        {
            document.ToTable("invoice_documents");
            document.WithOwner().HasForeignKey(DocumentInvoiceIdProperty);
            document.HasKey(DocumentInvoiceIdProperty);
            document.Property<InvoiceId>(DocumentInvoiceIdProperty).HasColumnName("invoice_id");
            document.Property<TenantId>(TenantDbContext.TenantIdProperty);
            document.Property(d => d.Id).HasColumnName("id").ValueGeneratedNever();
            document.HasIndex(d => d.Id).IsUnique();
            document.Property(d => d.StorageKey).HasMaxLength(256);
            document.Property(d => d.FileName).HasMaxLength(InvoiceDocument.MaxFileNameLength);
            document.Property(d => d.SizeBytes);
            document.Property(d => d.Sha256).HasMaxLength(InvoiceDocument.Sha256Length).IsFixedLength();
        });
        builder.Navigation(i => i.Document).IsRequired();

        // Maps to PostgreSQL's xmin system column, which changes on every update.
        builder.Property<uint>(VersionProperty).IsRowVersion();

        // Indexes on the field columns are created in SQL by the AddInvoiceListIndexes migration,
        // because EF Core cannot index properties of a complex type.
    }
}
