using InvoiceFlow.Domain.Invoices;
using InvoiceFlow.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Persistence.Tenant;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public const string VersionProperty = "Version";

    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.Ignore(i => i.DomainEvents);

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property<TenantId>(TenantDbContext.TenantIdProperty);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(i => i.DuplicateStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(i => i.DuplicateRule).HasMaxLength(32);

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

        builder.ComplexProperty(i => i.FieldStatuses, statuses =>
        {
            statuses.Property(s => s.SupplierName).HasConversion<string>().HasMaxLength(16)
                .HasColumnName("supplier_name_status");
            statuses.Property(s => s.InvoiceNumber).HasConversion<string>().HasMaxLength(16)
                .HasColumnName("invoice_number_status");
            statuses.Property(s => s.InvoiceDate).HasConversion<string>().HasMaxLength(16)
                .HasColumnName("invoice_date_status");
            statuses.Property(s => s.DueDate).HasConversion<string>().HasMaxLength(16)
                .HasColumnName("due_date_status");
            statuses.Property(s => s.Currency).HasConversion<string>().HasMaxLength(16)
                .HasColumnName("currency_status");
            statuses.Property(s => s.Subtotal).HasConversion<string>().HasMaxLength(16)
                .HasColumnName("subtotal_status");
            statuses.Property(s => s.TaxAmount).HasConversion<string>().HasMaxLength(16)
                .HasColumnName("tax_amount_status");
            statuses.Property(s => s.TotalAmount).HasConversion<string>().HasMaxLength(16)
                .HasColumnName("total_amount_status");
            statuses.Property(s => s.PurchaseOrderReference).HasConversion<string>().HasMaxLength(16)
                .HasColumnName("purchase_order_reference_status");
        });

        builder.Property(i => i.DocumentLanguage).HasMaxLength(DocumentLanguage.MaxLength);

        builder.OwnsOne(i => i.Document, document =>
        {
            document.ToTable("invoice_documents");
            document.WithOwner().HasForeignKey("invoice_id");
            document.HasKey(d => d.Id);
            document.Property(d => d.Id).ValueGeneratedNever();
            document.Property<TenantId>(TenantDbContext.TenantIdProperty);
            document.Property(d => d.StorageKey).HasMaxLength(200);
            document.Property(d => d.OriginalFileName).HasMaxLength(InvoiceDocument.MaxFileNameLength);
            document.Property(d => d.ContentType).HasMaxLength(100);
            document.Property(d => d.SizeBytes);
            document.Property(d => d.Sha256).HasMaxLength(64);
            document.HasIndex(d => d.Sha256);
        });

        builder.ComplexProperty(i => i.UploadedBy, ActorMapping.Configure);
        builder.Property(i => i.UploadedAt);
        builder.ComplexProperty(i => i.SubmittedBy, ActorMapping.Configure);
        builder.Property(i => i.PaymentReference).HasMaxLength(Invoice.MaxPaymentReferenceLength);

        // Maps to PostgreSQL's xmin system column, which changes on every update.
        builder.Property<uint>(VersionProperty).IsRowVersion();

        // Indexes on the field columns are created in SQL by the AddInvoiceListIndexes migration,
        // because EF Core cannot index properties of a complex type.
    }
}
