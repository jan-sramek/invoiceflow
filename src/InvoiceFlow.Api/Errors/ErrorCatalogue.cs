using System.Collections.Frozen;
using System.ComponentModel;
using System.Reflection;
using InvoiceFlow.Application.Errors;
using InvoiceFlow.Domain.Errors;

namespace InvoiceFlow.Api.Errors;

/// <summary>
/// Every error code the API can return, read from the code classes of each layer. A code without a
/// title or a duplicate code throws on first use, which the catalogue tests catch.
/// </summary>
internal static class ErrorCatalogue
{
    public const string DocumentationUrl = "https://github.com/jan-sramek/invoiceflow/blob/main/docs/error-codes.md";

    public static FrozenDictionary<string, string> Titles { get; } = Constants(
            typeof(DomainErrors), typeof(ApplicationErrors), typeof(ApiErrors))
        .ToFrozenDictionary(
            constant => constant.Code,
            constant => constant.Title ?? throw new InvalidOperationException($"Error code '{constant.Code}' has no title."));

    public static string TypeOf(string code) => $"{DocumentationUrl}#{code}";

    private static IEnumerable<(string Code, string? Title)> Constants(params Type[] types) =>
        from type in types
        from field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string))
            .Select(f => ((string)f.GetRawConstantValue()!, f.GetCustomAttribute<DescriptionAttribute>()?.Description))
            .Concat(Constants(type.GetNestedTypes()))
        select field;
}
