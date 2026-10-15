using FluentValidation;
using InvoiceFlow.Application.Abstractions.Context;
using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Abstractions.Persistence;
using InvoiceFlow.Application.Behaviors;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Application.Invoices.GetInvoice;
using InvoiceFlow.Application.Invoices.ListInvoices;
using InvoiceFlow.Domain.Common;
using InvoiceFlow.Domain.Invoices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace InvoiceFlow.Application.Tests.Behaviors;

public class PipelineTests
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Command_Valid_ValidatesHandlesThenCommits()
    {
        await using ServiceProvider services = Build();
        Calls calls = services.GetRequiredService<Calls>();

        int result = await HandleAsync(services, new TestCommand(5));

        result.ShouldBe(5);
        calls.ShouldBe(["validate", "handle", "commit"]);
        FakeLogRecord completed = services.GetFakeLogCollector().GetSnapshot().ShouldHaveSingleItem();
        completed.Id.ShouldBe(new EventId(300, "UseCaseCompleted"));
        completed.Level.ShouldBe(LogLevel.Debug);
        completed.Category.ShouldBe("InvoiceFlow.Application.UseCases");
        completed.StructuredState!.ShouldContain(pair => pair.Key == "UseCase" && pair.Value == "Test");
    }

    [Fact]
    public async Task Command_Invalid_NeitherHandlesNorCommits()
    {
        await using ServiceProvider services = Build();

        RequestValidationException exception = await Should.ThrowAsync<RequestValidationException>(
            () => HandleAsync(services, new TestCommand(0)));

        services.GetRequiredService<Calls>().ShouldBe(["validate"]);
        RequestFieldError error = exception.Errors.ShouldHaveSingleItem();
        error.Property.ShouldBe(nameof(TestCommand.Value));
        error.Code.ShouldBe("validation.must_be_positive");
        error.Params.ShouldBe(new Dictionary<string, object?> { ["min"] = 1 });
        ShouldBeRejected(services, "validation.failed");
    }

    [Fact]
    public async Task Command_HandlerFails_DoesNotCommit()
    {
        await using ServiceProvider services = Build();

        await Should.ThrowAsync<InvoiceNotFoundException>(() => HandleAsync(services, new TestCommand(-1, Fail: true)));

        services.GetRequiredService<Calls>().ShouldBe(["validate", "handle"]);
    }

    [Fact]
    public async Task Command_CommitFails_IsLoggedAsRejected()
    {
        await using ServiceProvider services = Build(failCommit: true);

        await Should.ThrowAsync<ConcurrencyException>(() => HandleAsync(services, new TestCommand(5)));

        ShouldBeRejected(services, "concurrency.stale_version");
    }

    [Fact]
    public async Task Query_ValidatesAndHandlesWithoutCommitting()
    {
        await using ServiceProvider services = Build();
        IQueryHandler<TestQuery, int> handler = services.GetRequiredService<IQueryHandler<TestQuery, int>>();

        int result = await handler.HandleAsync(new TestQuery(7), CancellationToken);

        result.ShouldBe(7);
        services.GetRequiredService<Calls>().ShouldBe(["validate", "handle"]);
        handler.ShouldBeOfType<LoggingQueryDecorator<TestQuery, int>>();
    }

    [Fact]
    public async Task AddApplication_RegistersUseCasesOfTheAssembly()
    {
        await using ServiceProvider services = Build();

        services.GetRequiredService<IQueryHandler<GetInvoiceQuery, Versioned<InvoiceDetailDto>>>()
            .ShouldBeOfType<LoggingQueryDecorator<GetInvoiceQuery, Versioned<InvoiceDetailDto>>>();
    }

    private static ServiceProvider Build(bool failCommit = false) =>
        new ServiceCollection()
            .AddLogging(logging => logging.AddFakeLogging().SetMinimumLevel(LogLevel.Trace))
            .AddScoped<Calls>()
            .AddScoped<IUnitOfWork>(provider => new RecordingUnitOfWork(provider.GetRequiredService<Calls>(), failCommit))
            .AddScoped<IInvoiceReadService, NoInvoices>()
            .AddScoped<IInvoiceRepository, NoInvoices>()
            .AddScoped<ICurrentUser, NoUser>()
            .AddScoped<ICommandHandler<TestCommand, int>, TestCommandHandler>()
            .AddScoped<IQueryHandler<TestQuery, int>, TestQueryHandler>()
            .AddScoped<IValidator<TestCommand>, TestCommandValidator>()
            .AddScoped<IValidator<TestQuery>, TestQueryValidator>()
            .AddApplication()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = false, ValidateOnBuild = true });

    private static Task<int> HandleAsync(ServiceProvider services, TestCommand command) =>
        services.GetRequiredService<ICommandHandler<TestCommand, int>>().HandleAsync(command, CancellationToken);

    private static void ShouldBeRejected(ServiceProvider services, string errorCode)
    {
        FakeLogRecord rejected = services.GetFakeLogCollector().GetSnapshot().ShouldHaveSingleItem();
        rejected.Id.ShouldBe(new EventId(301, "UseCaseRejected"));
        rejected.Level.ShouldBe(LogLevel.Information);
        rejected.StructuredState!.ShouldContain(pair => pair.Key == "ErrorCode" && pair.Value == errorCode);
    }

    public sealed class Calls : List<string>;

    public sealed record TestCommand(int Value, bool Fail = false) : ICommand<int>;

    public sealed record TestQuery(int Value) : IQuery<int>;

    private sealed class TestCommandHandler(Calls calls) : ICommandHandler<TestCommand, int>
    {
        public Task<int> HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            calls.Add("handle");
            return command.Fail ? throw new InvoiceNotFoundException() : Task.FromResult(command.Value);
        }
    }

    private sealed class TestQueryHandler(Calls calls) : IQueryHandler<TestQuery, int>
    {
        public Task<int> HandleAsync(TestQuery query, CancellationToken cancellationToken)
        {
            calls.Add("handle");
            return Task.FromResult(query.Value);
        }
    }

    private sealed class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator(Calls calls)
        {
            RuleFor(c => c.Value)
                .Must((command, value) =>
                {
                    calls.Add("validate");
                    return value > 0 || command.Fail;
                })
                .WithErrorCode("validation.must_be_positive")
                .WithState(_ => new Dictionary<string, object?> { ["min"] = 1 });
        }
    }

    private sealed class TestQueryValidator : AbstractValidator<TestQuery>
    {
        public TestQueryValidator(Calls calls)
        {
            RuleFor(q => q.Value).Must(_ =>
            {
                calls.Add("validate");
                return true;
            });
        }
    }

    private sealed class RecordingUnitOfWork(Calls calls, bool fail) : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            calls.Add("commit");
            return fail ? throw new ConcurrencyException() : Task.CompletedTask;
        }
    }

    private sealed class NoInvoices : IInvoiceReadService, IInvoiceRepository
    {
        public Task<Versioned<InvoiceDetailDto>?> GetDetailAsync(InvoiceId id, CancellationToken cancellationToken) =>
            Task.FromResult<Versioned<InvoiceDetailDto>?>(null);

        public Task<PagedList<InvoiceSummaryDto>> ListAsync(
            ListInvoicesQuery query, InvoiceSort sort, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedList<InvoiceSummaryDto>([], query.Page, query.PageSize, 0));

        public Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken) =>
            Task.FromResult<Invoice?>(null);

        public Task<Invoice?> GetAsync(InvoiceId id, string expectedVersion, CancellationToken cancellationToken) =>
            Task.FromResult<Invoice?>(null);

        public void Add(Invoice invoice) => throw new NotSupportedException();
    }

    private sealed class NoUser : ICurrentUser
    {
        public Actor Actor => throw new UserRequiredException();
    }
}
