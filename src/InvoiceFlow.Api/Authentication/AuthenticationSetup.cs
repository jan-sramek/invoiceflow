using InvoiceFlow.Application.Abstractions.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace InvoiceFlow.Api.Authentication;

internal static class AuthenticationSetup
{
    public const string SchemeSelectorName = "InvoiceFlow";

    public static IServiceCollection AddUserAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        AuthenticationOptions auth = configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>()
            ?? new AuthenticationOptions();

        AuthenticationBuilder builder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = SchemeSelectorName;
            options.DefaultChallengeScheme = SchemeSelectorName;
        });

        builder.AddPolicyScheme(SchemeSelectorName, SchemeSelectorName, options =>
        {
            options.ForwardDefaultSelector = context => SelectScheme(context, auth);
        });

        if (auth.HasJwtBearer)
        {
            builder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = auth.Authority;
                options.Audience = auth.Audience;
                options.RequireHttpsMetadata = auth.RequireHttpsMetadata;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = string.IsNullOrWhiteSpace(auth.ValidIssuer) ? auth.Authority : auth.ValidIssuer,
                    ValidateAudience = true,
                    ValidAudience = auth.Audience,
                    NameClaimType = UserClaims.Name,
                    RoleClaimType = "roles",
                };
                // Keycloak puts roles under realm_access; map name/sub without renaming.
                options.MapInboundClaims = false;
            });
        }

        builder.AddScheme<DevelopmentUserOptions, DevelopmentUserHandler>(
            DevelopmentUserHandler.SchemeName,
            options => options.Enabled = auth.DevelopmentUsers);

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }

    private static string SelectScheme(HttpContext context, AuthenticationOptions auth)
    {
        string? header = context.Request.Headers.Authorization;
        if (auth.HasJwtBearer
            && !string.IsNullOrEmpty(header)
            && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        if (auth.DevelopmentUsers)
        {
            return DevelopmentUserHandler.SchemeName;
        }

        return auth.HasJwtBearer
            ? JwtBearerDefaults.AuthenticationScheme
            : DevelopmentUserHandler.SchemeName;
    }
}
