using InvoiceFlow.Infrastructure;
using InvoiceFlow.Infrastructure.Persistence.SchemaMigration;
using InvoiceFlow.ServiceDefaults;

// Runs as a Job. A failed tenant doesn't fail the Job: it is recorded in the catalog and served as
// "under maintenance" until a later run migrates it.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSchemaMigrator(builder.Configuration);

using IHost host = builder.Build();
IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

// Started and stopped explicitly, so telemetry is exported before the process exits.
await host.StartAsync();
try
{
    await host.Services.GetRequiredService<ISchemaMigrator>().MigrateAsync(lifetime.ApplicationStopping);
}
finally
{
    await host.StopAsync();
}
