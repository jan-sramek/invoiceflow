using System.Text.RegularExpressions;
using InvoiceFlow.Api.Errors;
using InvoiceFlow.Domain.Errors;
using Microsoft.AspNetCore.Http;

namespace InvoiceFlow.Api.Tests.Errors;

public partial class ErrorCatalogueTests
{
    [Fact]
    public void Codes_AreAreaDotReasonInSnakeCase()
    {
        ErrorCatalogue.Titles.Keys.Where(code => !CodeFormat().IsMatch(code)).ShouldBeEmpty();
    }

    [Fact]
    public void Titles_AreNotBlank()
    {
        ErrorCatalogue.Titles.Where(pair => string.IsNullOrWhiteSpace(pair.Value)).ShouldBeEmpty();
    }

    [Fact]
    public void Documentation_ListsExactlyTheKnownCodes()
    {
        string documentation = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "error-codes.md"));
        string[] documented = [.. DocumentedCode().Matches(documentation).Select(match => match.Groups[1].Value)];

        documented.ShouldBeUnique();
        documented.ShouldBe(ErrorCatalogue.Titles.Keys, ignoreOrder: true);
    }

    [Fact]
    public void ErrorKinds_AllMapToClientOrAvailabilityStatus()
    {
        Enum.GetValues<ErrorKind>()
            .Where(kind => InvoiceFlowExceptionHandler.StatusOf(kind) == StatusCodes.Status500InternalServerError)
            .ShouldBeEmpty();
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "InvoiceFlow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [GeneratedRegex("^[a-z]+(_[a-z]+)*\\.[a-z]+(_[a-z]+)*$")]
    private static partial Regex CodeFormat();

    [GeneratedRegex("<a id=\"([^\"]+)\"></a>")]
    private static partial Regex DocumentedCode();
}
