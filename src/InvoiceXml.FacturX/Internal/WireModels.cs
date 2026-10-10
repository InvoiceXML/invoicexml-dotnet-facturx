using System.Collections.Generic;
using System.Linq;

namespace InvoiceXml.FacturX.Internal;

/// <summary>The <c>{ "invoice": ..., "options": ... }</c> envelope of the create endpoints.</summary>
internal sealed class CreateInvoiceRequestBody
{
    public InvoiceDocument? Invoice { get; set; }

    public InvoiceOptionsBody? Options { get; set; }
}

/// <summary>The <c>options</c> object of the create endpoints.</summary>
internal sealed class InvoiceOptionsBody
{
    public PdfLanguage? Language { get; set; }

    public string? BrandColor { get; set; }

    public string? PdfUrl { get; set; }

    public string? LogoUrl { get; set; }

    public bool? IncludeAdvancedProperties { get; set; }

    public string[]? Rules { get; set; }

    public static InvoiceOptionsBody? From(FacturXOptions? options) => options is null
        ? null
        : new InvoiceOptionsBody
        {
            Language = options.Language,
            BrandColor = options.BrandColor,
            PdfUrl = options.PdfUrl,
            LogoUrl = options.LogoUrl,
            IncludeAdvancedProperties = options.IncludeAdvancedProperties,
            Rules = options.Rules is { Count: > 0 } rules ? rules.ToArray() : null,
        };
}

/// <summary>The <c>{ "invoice": ... }</c> envelope returned by <c>/v1/extract/json</c>.</summary>
internal sealed class InvoiceJsonResponseBody
{
    public InvoiceDocument? Invoice { get; set; }
}

/// <summary>RFC 7807 problem details with the API's extensions.</summary>
internal sealed class ProblemBody
{
    public string? Title { get; set; }

    public string? Detail { get; set; }

    public int? Status { get; set; }

    public int? ErrorCode { get; set; }

    public string? TraceId { get; set; }

    public List<ValidationFinding>? Errors { get; set; }
}
