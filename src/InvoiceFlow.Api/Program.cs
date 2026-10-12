using System.Text.Json.Serialization;
using InvoiceFlow.Api.Authentication;
using InvoiceFlow.Api.Errors;
using InvoiceFlow.Application;
using InvoiceFlow.Infrastructure;
using InvoiceFlow.ServiceDefaults;
using InvoiceFlow.ServiceDefaults.Health;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddCatalogCheck(HealthEndpoints.ReadyTag);

builder.Services.AddErrorContract();
builder.Services.AddUserAuthentication(builder.Configuration);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // A misspelt property would otherwise be dropped without the client noticing
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.AllowInputFormatterExceptionMessages = false;
});
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

app.UseServiceDefaults();
app.UseErrorContract();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

await app.RunAsync();
