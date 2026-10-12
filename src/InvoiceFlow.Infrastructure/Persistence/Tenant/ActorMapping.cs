using InvoiceFlow.Domain.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceFlow.Infrastructure.Persistence.Tenant;

internal static class ActorMapping
{
    public static void Configure(ComplexPropertyBuilder<Actor> actor)
    {
        actor.Property(a => a.Type).HasConversion<string>().HasMaxLength(16);
        actor.Property(a => a.Id).HasMaxLength(100);
        actor.Property(a => a.Name).HasMaxLength(200);
    }
}
