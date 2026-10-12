using InvoiceFlow.Application.Audit;
using InvoiceFlow.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Persistence.Tenant;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.ComplexProperty(e => e.Actor, ActorMapping.Configure);
        builder.Property(e => e.Action).HasMaxLength(64);
        builder.Property(e => e.SubjectType).HasMaxLength(32);
        builder.ComplexCollection(e => e.Changes, changes => changes.ToJson());
        builder.Property(e => e.Reason).HasMaxLength(Invoice.MaxReasonLength);
        builder.Property(e => e.CorrelationId).HasMaxLength(32);

        builder.HasIndex(e => new { e.SubjectType, e.SubjectId, e.OccurredAt });
        builder.HasIndex(e => e.OccurredAt);
    }
}
