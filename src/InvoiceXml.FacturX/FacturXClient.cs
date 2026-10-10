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
/// header, up to <see cref="InvoiceXmlClientOptions.MaxRetries"/> times. Uploads are buffered in
/// memory so a retry can resend them.
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

    // Each rule set travels as its own "rules" form field.
    private static IEnumerable<FormPart> RuleParts(IEnumerable<string>? rules) =>
        (rules ?? Enumerable.Empty<string>())
            .Where(rule => !string.IsNullOrWhiteSpace(rule))
            .Select(rule => FormPart.FromValue("rules", rule.Trim()));
}
