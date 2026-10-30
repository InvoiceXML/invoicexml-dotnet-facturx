using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InvoiceXml.FacturX.Internal;

namespace InvoiceXml.FacturX;

/// <summary>
/// Client for the Factur-X endpoints of the InvoiceXML API.
/// </summary>
/// <remarks>
/// <para>
/// The client is thread-safe; create one and reuse it. Pass your own <see cref="HttpClient"/>
/// (for example from <c>IHttpClientFactory</c>) to control its lifetime and handlers; otherwise
/// the client creates one and disposes it with itself.
/// </para>
/// <para>
/// Requests answered with HTTP 429 are retried after the interval in the <c>Retry-After</c>
/// header, up to <see cref="InvoiceXmlClientOptions.MaxRetries"/> times, except transform calls.
/// Uploads are buffered in memory so a retry can resend them.
/// </para>
/// </remarks>
public sealed class FacturXClient : IFacturXClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    /// <summary>Creates a client with its own <see cref="HttpClient"/>.</summary>
    /// <param name="apiKey">Your InvoiceXML API key.</param>
    public FacturXClient(string apiKey)
        : this(new InvoiceXmlClientOptions { ApiKey = apiKey })
    {
    }

    /// <summary>Creates a client with its own <see cref="HttpClient"/>.</summary>
    /// <param name="options">Client settings, including the API key.</param>
    public FacturXClient(InvoiceXmlClientOptions options)
        : this(new HttpClient(), options, ownsHttpClient: true)
    {
    }

    /// <summary>Creates a client on an existing <see cref="HttpClient"/>, which is not disposed by the client.</summary>
    /// <param name="httpClient">The HTTP client to send requests with.</param>
    /// <param name="apiKey">Your InvoiceXML API key.</param>
    public FacturXClient(HttpClient httpClient, string apiKey)
        : this(httpClient, new InvoiceXmlClientOptions { ApiKey = apiKey })
    {
    }

    /// <summary>Creates a client on an existing <see cref="HttpClient"/>, which is not disposed by the client.</summary>
    /// <param name="httpClient">The HTTP client to send requests with.</param>
    /// <param name="options">Client settings, including the API key.</param>
    public FacturXClient(HttpClient httpClient, InvoiceXmlClientOptions options)
        : this(httpClient, options, ownsHttpClient: false)
    {
    }

    private FacturXClient(HttpClient httpClient, InvoiceXmlClientOptions options, bool ownsHttpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (options is null) throw new ArgumentNullException(nameof(options));
        _ownsHttpClient = ownsHttpClient;
        Transport = new ApiTransport(_http, options);
    }

    internal ApiTransport Transport { get; }

    /// <inheritdoc />
    public async Task<InvoiceFile> CreateFacturXAsync(InvoiceDocument invoice, FacturXOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));
        var body = RequestContent.JsonBody(new CreateInvoiceRequestBody { Invoice = invoice, Options = InvoiceOptionsBody.From(options) });
        var response = await Transport.PostAsync("v1/create/facturx", body, cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadFileAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> EmailFacturXAsync(InvoiceDocument invoice, EmailMessage email, FacturXOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));
        if (email is null) throw new ArgumentNullException(nameof(email));
        var body = RequestContent.JsonBody(new CreateInvoiceEmailRequestBody { Invoice = invoice, Options = InvoiceOptionsBody.From(options), Email = EmailBody.FromMessage(email) });
        var response = await Transport.PostAsync("v1/create/facturx/email", body, cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadJsonAsync<EmailSendResult>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InvoiceValidationResult> ValidateFacturXAsync(byte[] pdf, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default)
    {
        if (pdf is null) throw new ArgumentNullException(nameof(pdf));
        var parts = new List<FormPart> { FormPart.FromFile("file", pdf, fileName) };
        parts.AddRange(RuleParts(rules));
        var response = await Transport.PostAsync("v1/validate/facturx", RequestContent.Form(parts.ToArray()), cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadJsonAsync<InvoiceValidationResult>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InvoiceValidationResult> ValidateFacturXAsync(Stream pdf, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default) =>
        await ValidateFacturXAsync(await RequestContent.BufferAsync(pdf, cancellationToken).ConfigureAwait(false), fileName, rules, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<InvoiceFile> EmbedFacturXAsync(byte[] pdf, byte[] xml, bool skipValidation = false, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default)
    {
        if (pdf is null) throw new ArgumentNullException(nameof(pdf));
        if (xml is null) throw new ArgumentNullException(nameof(xml));
        var parts = new List<FormPart>
        {
            FormPart.FromFile("pdf", pdf, fileName ?? "invoice.pdf"),
            FormPart.FromFile("xml", xml, "factur-x.xml"),
            FormPart.FromValue("skipValidation", skipValidation ? "true" : null),
        };
        parts.AddRange(RuleParts(rules));
        var response = await Transport.PostAsync("v1/embed/facturx", RequestContent.Form(parts.ToArray()), cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadFileAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InvoiceFile> EmbedFacturXAsync(Stream pdf, Stream xml, bool skipValidation = false, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default)
    {
        var pdfBytes = await RequestContent.BufferAsync(pdf, cancellationToken).ConfigureAwait(false);
        var xmlBytes = await RequestContent.BufferAsync(xml, cancellationToken).ConfigureAwait(false);
        return await EmbedFacturXAsync(pdfBytes, xmlBytes, skipValidation, fileName, rules, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> EmailEmbeddedFacturXAsync(byte[] pdf, byte[] xml, EmailMessage email, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default)
    {
        if (pdf is null) throw new ArgumentNullException(nameof(pdf));
        if (xml is null) throw new ArgumentNullException(nameof(xml));
        if (email is null) throw new ArgumentNullException(nameof(email));
        var parts = new List<FormPart>
        {
            FormPart.FromFile("pdf", pdf, fileName ?? "invoice.pdf"),
            FormPart.FromFile("xml", xml, "factur-x.xml"),
        };
        parts.AddRange(RuleParts(rules));
        parts.AddRange(EmailBody.FromMessage(email).FormParts());
        var response = await Transport.PostAsync("v1/embed/facturx/email", RequestContent.Form(parts.ToArray()), cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadJsonAsync<EmailSendResult>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<EmailSendResult> EmailEmbeddedFacturXAsync(Stream pdf, Stream xml, EmailMessage email, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default)
    {
        if (email is null) throw new ArgumentNullException(nameof(email));
        var pdfBytes = await RequestContent.BufferAsync(pdf, cancellationToken).ConfigureAwait(false);
        var xmlBytes = await RequestContent.BufferAsync(xml, cancellationToken).ConfigureAwait(false);
        return await EmailEmbeddedFacturXAsync(pdfBytes, xmlBytes, email, fileName, rules, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InvoiceFile> RenderCiiToPdfAsync(byte[] xml, RenderOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default)
    {
        if (xml is null) throw new ArgumentNullException(nameof(xml));
        var form = RequestContent.Form(
            FormPart.FromFile("file", xml, fileName),
            LanguagePart("language", options?.Language),
            FormPart.FromValue("logoUrl", options?.LogoUrl),
            FlagPart("includeQrCode", options?.IncludeQrCode));
        var response = await Transport.PostAsync("v1/render/cii/to/pdf", form, cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadFileAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InvoiceFile> RenderCiiToPdfAsync(Stream xml, RenderOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        await RenderCiiToPdfAsync(await RequestContent.BufferAsync(xml, cancellationToken).ConfigureAwait(false), options, fileName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<InvoiceFile> ConvertFacturXToUblAsync(byte[] pdf, ConvertOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        ConvertAsync("v1/convert/facturx/to/ubl", pdf, options, fileName, cancellationToken);

    /// <inheritdoc />
    public async Task<InvoiceFile> ConvertFacturXToUblAsync(Stream pdf, ConvertOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        await ConvertFacturXToUblAsync(await RequestContent.BufferAsync(pdf, cancellationToken).ConfigureAwait(false), options, fileName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<InvoiceFile> ConvertFacturXToCiiAsync(byte[] pdf, ConvertOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        ConvertAsync("v1/convert/facturx/to/cii", pdf, options, fileName, cancellationToken);

    /// <inheritdoc />
    public async Task<InvoiceFile> ConvertFacturXToCiiAsync(Stream pdf, ConvertOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        await ConvertFacturXToCiiAsync(await RequestContent.BufferAsync(pdf, cancellationToken).ConfigureAwait(false), options, fileName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<InvoiceFile> ConvertCiiToFacturXAsync(byte[] xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        ConvertToPdfAsync("v1/convert/cii/to/facturx", xml, options, fileName, cancellationToken);

    /// <inheritdoc />
    public async Task<InvoiceFile> ConvertCiiToFacturXAsync(Stream xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        await ConvertCiiToFacturXAsync(await RequestContent.BufferAsync(xml, cancellationToken).ConfigureAwait(false), options, fileName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<InvoiceFile> ConvertUblToFacturXAsync(byte[] xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        ConvertToPdfAsync("v1/convert/ubl/to/facturx", xml, options, fileName, cancellationToken);

    /// <inheritdoc />
    public async Task<InvoiceFile> ConvertUblToFacturXAsync(Stream xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        await ConvertUblToFacturXAsync(await RequestContent.BufferAsync(xml, cancellationToken).ConfigureAwait(false), options, fileName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<InvoiceFile> ConvertXRechnungToFacturXAsync(byte[] xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        ConvertToPdfAsync("v1/convert/xrechnung/to/facturx", xml, options, fileName, cancellationToken);

    /// <inheritdoc />
    public async Task<InvoiceFile> ConvertXRechnungToFacturXAsync(Stream xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        await ConvertXRechnungToFacturXAsync(await RequestContent.BufferAsync(xml, cancellationToken).ConfigureAwait(false), options, fileName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<ValidationReportResult> ValidateFacturXReportAsync(byte[] pdf, string? fileName = null, IEnumerable<string>? rules = null, PdfLanguage? validationLanguage = null, CancellationToken cancellationToken = default)
    {
        if (pdf is null) throw new ArgumentNullException(nameof(pdf));
        var parts = new List<FormPart> { FormPart.FromFile("file", pdf, fileName), LanguagePart("validationLanguage", validationLanguage) };
        parts.AddRange(RuleParts(rules));
        var response = await Transport.PostAsync("v1/validate/facturx/report", RequestContent.Form(parts.ToArray()), cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadValidationReportAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ValidationReportResult> ValidateFacturXReportAsync(Stream pdf, string? fileName = null, IEnumerable<string>? rules = null, PdfLanguage? validationLanguage = null, CancellationToken cancellationToken = default) =>
        await ValidateFacturXReportAsync(await RequestContent.BufferAsync(pdf, cancellationToken).ConfigureAwait(false), fileName, rules, validationLanguage, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<InvoiceFile> TransformToFacturXAsync(byte[] pdf, TransformOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default)
    {
        if (pdf is null) throw new ArgumentNullException(nameof(pdf));
        var form = RequestContent.Form(
            FormPart.FromFile("file", pdf, fileName),
            LanguagePart("language", options?.Language),
            FlagPart("preserveOriginalPdf", options?.PreserveOriginalPdf),
            FlagPart("includeAdvancedProperties", options?.IncludeAdvancedProperties),
            LanguagePart("validationLanguage", options?.ValidationLanguage));
        var response = await Transport.PostAsync("v1/transform/to/facturx", form, cancellationToken, retryRateLimit: false).ConfigureAwait(false);
        return await ApiTransport.ReadFileAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InvoiceFile> TransformToFacturXAsync(Stream pdf, TransformOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default) =>
        await TransformToFacturXAsync(await RequestContent.BufferAsync(pdf, cancellationToken).ConfigureAwait(false), options, fileName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<InvoiceDocument> ExtractJsonAsync(byte[] file, string? fileName = null, CancellationToken cancellationToken = default)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        var form = RequestContent.Form(FormPart.FromFile("file", file, fileName));
        var response = await Transport.PostAsync("v1/extract/json", form, cancellationToken).ConfigureAwait(false);
        var result = await ApiTransport.ReadJsonAsync<InvoiceJsonResponseBody>(response, cancellationToken).ConfigureAwait(false);
        return result.Invoice ?? throw new InvoiceXmlApiException(System.Net.HttpStatusCode.OK, "InvoiceXML API returned no invoice.");
    }

    /// <inheritdoc />
    public async Task<InvoiceDocument> ExtractJsonAsync(Stream file, string? fileName = null, CancellationToken cancellationToken = default) =>
        await ExtractJsonAsync(await RequestContent.BufferAsync(file, cancellationToken).ConfigureAwait(false), fileName, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<InvoiceFile> ExtractAttachmentsAsync(byte[] file, string? fileName = null, CancellationToken cancellationToken = default)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));
        var form = RequestContent.Form(FormPart.FromFile("file", file, fileName));
        var response = await Transport.PostAsync("v1/extract/attachments", form, cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadFileAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InvoiceFile> ExtractAttachmentsAsync(Stream file, string? fileName = null, CancellationToken cancellationToken = default) =>
        await ExtractAttachmentsAsync(await RequestContent.BufferAsync(file, cancellationToken).ConfigureAwait(false), fileName, cancellationToken).ConfigureAwait(false);

    /// <summary>Disposes the <see cref="HttpClient"/> if the client created it.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }

    private async Task<InvoiceFile> ConvertAsync(string path, byte[] pdf, ConvertOptions? options, string? fileName, CancellationToken cancellationToken)
    {
        if (pdf is null) throw new ArgumentNullException(nameof(pdf));
        var form = RequestContent.Form(
            FormPart.FromFile("file", pdf, fileName),
            LanguagePart("validationLanguage", options?.ValidationLanguage));
        var response = await Transport.PostAsync(path, form, cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadFileAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InvoiceFile> ConvertToPdfAsync(string path, byte[] xml, ConvertToPdfOptions? options, string? fileName, CancellationToken cancellationToken)
    {
        if (xml is null) throw new ArgumentNullException(nameof(xml));
        var form = RequestContent.Form(
            FormPart.FromFile("file", xml, fileName),
            LanguagePart("language", options?.Language),
            LanguagePart("validationLanguage", options?.ValidationLanguage));
        var response = await Transport.PostAsync(path, form, cancellationToken).ConfigureAwait(false);
        return await ApiTransport.ReadFileAsync(response, cancellationToken).ConfigureAwait(false);
    }

    // Each rule set travels as its own "rules" form field.
    private static IEnumerable<FormPart> RuleParts(IEnumerable<string>? rules) =>
        (rules ?? Enumerable.Empty<string>())
            .Where(rule => !string.IsNullOrWhiteSpace(rule))
            .Select(rule => FormPart.FromValue("rules", rule.Trim()));

    private static FormPart LanguagePart(string name, PdfLanguage? language) =>
        FormPart.FromValue(name, language is { } value ? EnumWire<PdfLanguage>.ToWire(value) : null);

    // An unset option stays off the form, so the API's default applies; false is sent as false.
    private static FormPart FlagPart(string name, bool? value) =>
        FormPart.FromValue(name, value is { } flag ? (flag ? "true" : "false") : null);
}
