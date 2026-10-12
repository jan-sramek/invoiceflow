using System.Reflection;
using FluentValidation;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceFlow.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers every handler and validator of this assembly and wraps the handlers in the pipeline:
    /// logging, then validation, then (commands only) the unit of work.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(
                classes => classes
                    .AssignableToAny(typeof(ICommandHandler<,>), typeof(IQueryHandler<,>))
                    .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal)),
                publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Each Decorate wraps the previous one, so the last registered runs first.
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(UnitOfWorkCommandDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationCommandDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingCommandDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationQueryDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingQueryDecorator<,>));

        return services;
    }
}
