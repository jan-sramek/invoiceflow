using InvoiceFlow.Application.Abstractions.Context;

namespace InvoiceFlow.Api.Authentication;

internal static class AuthenticationSetup
{
    public const string SectionName = "Authentication:DevelopmentUsers";

    public static IServiceCollection AddUserAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        bool developmentUsers = configuration.GetValue<bool>(SectionName);

        services.AddAuthentication(DevelopmentUserHandler.SchemeName)
            .AddScheme<DevelopmentUserOptions, DevelopmentUserHandler>(
                DevelopmentUserHandler.SchemeName,
                options => options.Enabled = developmentUsers);
        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }
}
