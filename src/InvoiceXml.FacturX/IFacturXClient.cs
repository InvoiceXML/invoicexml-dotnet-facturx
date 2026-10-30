using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace InvoiceXml.FacturX;

/// <summary>
/// Creates, validates, embeds, renders, converts, emails and reads Factur-X e-invoices through the
/// InvoiceXML API. Implemented by <see cref="FacturXClient"/>; use the interface to inject and
/// fake the client in your own tests.
/// </summary>
public interface IFacturXClient
{
    /// <summary>
    /// Creates a Factur-X invoice: a PDF/A-3 with the EN 16931 XML embedded, validated before delivery
    /// (<c>POST /v1/create/facturx</c>).
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="options">Rendering options and extra rule sets, or null for the defaults.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The Factur-X PDF.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The invoice would not be compliant (see <see cref="InvoiceXmlApiException.Findings"/>), or the request was refused.
    /// </exception>
    Task<InvoiceFile> CreateFacturXAsync(InvoiceDocument invoice, FacturXOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Factur-X invoice exactly like <see cref="CreateFacturXAsync"/> and sends it as a
    /// real email from your own verified domain (<c>POST /v1/create/facturx/email</c>). Returns the
    /// message id of the email, not the file.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="email">Sender, recipient and, optionally, the copies, subject and text of the email.</param>
    /// <param name="options">Rendering options and extra rule sets, or null for the defaults.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The accepted email with its message id.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The invoice would not be compliant or an email field is invalid (see <see cref="InvoiceXmlApiException.Findings"/>),
    /// the sender's domain is not verified (<see cref="InvoiceXmlErrorCode.EmailDomainNotVerified"/>),
    /// your plan does not include email delivery (<see cref="InvoiceXmlErrorCode.EmailNotInPlan"/>),
    /// sending is disabled for the account (<see cref="InvoiceXmlErrorCode.EmailSendingDisabled"/>), or the request was refused.
    /// </exception>
    Task<EmailSendResult> EmailFacturXAsync(InvoiceDocument invoice, EmailMessage email, FacturXOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a Factur-X PDF: extracts the embedded XML and checks it against the rules of the
    /// profile it declares (<c>POST /v1/validate/facturx</c>).
    /// </summary>
    /// <param name="pdf">The Factur-X PDF.</param>
    /// <param name="fileName">File name sent with the upload. Defaults to <c>invoice.pdf</c>.</param>
    /// <param name="rules">
    /// Additional rule sets to apply, for example <see cref="FacturXRuleSets.BrFr"/>. Null or empty for none.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The verdict. An invalid invoice is a normal result, not an exception.</returns>
    Task<InvoiceValidationResult> ValidateFacturXAsync(byte[] pdf, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ValidateFacturXAsync(byte[], string?, IEnumerable{string}?, CancellationToken)"/>
    Task<InvoiceValidationResult> ValidateFacturXAsync(Stream pdf, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns your own PDF plus a CII XML into a Factur-X PDF/A-3. The XML is validated before it is
    /// embedded (<c>POST /v1/embed/facturx</c>).
    /// </summary>
    /// <param name="pdf">Your invoice PDF, used as the visual layer.</param>
    /// <param name="xml">The CII invoice XML to embed.</param>
    /// <param name="skipValidation">
    /// True to embed without the business rule checks. Structural checks still apply. Defaults to false.
    /// </param>
    /// <param name="fileName">File name of the PDF upload. Defaults to <c>invoice.pdf</c>.</param>
    /// <param name="rules">
    /// Additional rule sets the XML must pass, for example <see cref="FacturXRuleSets.BrFr"/>. Null or empty for none.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The Factur-X PDF.</returns>
    /// <exception cref="InvoiceXmlApiException">The XML is not compliant, or the request was refused.</exception>
    Task<InvoiceFile> EmbedFacturXAsync(byte[] pdf, byte[] xml, bool skipValidation = false, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="EmbedFacturXAsync(byte[], byte[], bool, string?, IEnumerable{string}?, CancellationToken)"/>
    Task<InvoiceFile> EmbedFacturXAsync(Stream pdf, Stream xml, bool skipValidation = false, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns your own PDF plus a CII XML into a Factur-X PDF/A-3, always with full validation, and
    /// sends it as a real email from your own verified domain (<c>POST /v1/embed/facturx/email</c>).
    /// Returns the message id of the email, not the file.
    /// </summary>
    /// <param name="pdf">Your invoice PDF, used as the visual layer.</param>
    /// <param name="xml">The CII invoice XML to embed.</param>
    /// <param name="email">Sender, recipient and, optionally, the copies, subject and text of the email.</param>
    /// <param name="fileName">File name of the PDF upload. Defaults to <c>invoice.pdf</c>.</param>
    /// <param name="rules">
    /// Additional rule sets the XML must pass, for example <see cref="FacturXRuleSets.BrFr"/>. Null or empty for none.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The accepted email with its message id.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The XML is not compliant or an email field is invalid (see <see cref="InvoiceXmlApiException.Findings"/>),
    /// the sender's domain is not verified (<see cref="InvoiceXmlErrorCode.EmailDomainNotVerified"/>),
    /// your plan does not include email delivery (<see cref="InvoiceXmlErrorCode.EmailNotInPlan"/>),
    /// sending is disabled for the account (<see cref="InvoiceXmlErrorCode.EmailSendingDisabled"/>), or the request was refused.
    /// </exception>
    Task<EmailSendResult> EmailEmbeddedFacturXAsync(byte[] pdf, byte[] xml, EmailMessage email, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="EmailEmbeddedFacturXAsync(byte[], byte[], EmailMessage, string?, IEnumerable{string}?, CancellationToken)"/>
    Task<EmailSendResult> EmailEmbeddedFacturXAsync(Stream pdf, Stream xml, EmailMessage email, string? fileName = null, IEnumerable<string>? rules = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the CII XML of a Factur-X invoice as a human-readable PDF (<c>POST /v1/render/cii/to/pdf</c>).
    /// </summary>
    /// <param name="xml">The CII invoice XML.</param>
    /// <param name="options">Language, logo and payment QR code, or null for the defaults.</param>
    /// <param name="fileName">File name sent with the upload. Defaults to <c>invoice.xml</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The rendered PDF.</returns>
    Task<InvoiceFile> RenderCiiToPdfAsync(byte[] xml, RenderOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="RenderCiiToPdfAsync(byte[], RenderOptions?, string?, CancellationToken)"/>
    Task<InvoiceFile> RenderCiiToPdfAsync(Stream xml, RenderOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a Factur-X PDF to a UBL XML from the XML embedded in it (<c>POST /v1/convert/facturx/to/ubl</c>).
    /// </summary>
    /// <param name="pdf">The Factur-X PDF.</param>
    /// <param name="options">Language of the findings, or null for the defaults.</param>
    /// <param name="fileName">File name sent with the upload. Defaults to <c>invoice.pdf</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The UBL XML.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The PDF has no embedded XML (<see cref="InvoiceXmlErrorCode.NoEmbeddedXml"/>), the result
    /// would not be compliant (see <see cref="InvoiceXmlApiException.Findings"/>), or the request was refused.
    /// </exception>
    Task<InvoiceFile> ConvertFacturXToUblAsync(byte[] pdf, ConvertOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ConvertFacturXToUblAsync(byte[], ConvertOptions?, string?, CancellationToken)"/>
    Task<InvoiceFile> ConvertFacturXToUblAsync(Stream pdf, ConvertOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a Factur-X PDF to a CII XML from the XML embedded in it (<c>POST /v1/convert/facturx/to/cii</c>).
    /// </summary>
    /// <param name="pdf">The Factur-X PDF.</param>
    /// <param name="options">Language of the findings, or null for the defaults.</param>
    /// <param name="fileName">File name sent with the upload. Defaults to <c>invoice.pdf</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The CII XML.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The PDF has no embedded XML (<see cref="InvoiceXmlErrorCode.NoEmbeddedXml"/>), the result
    /// would not be compliant (see <see cref="InvoiceXmlApiException.Findings"/>), or the request was refused.
    /// </exception>
    Task<InvoiceFile> ConvertFacturXToCiiAsync(byte[] pdf, ConvertOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ConvertFacturXToCiiAsync(byte[], ConvertOptions?, string?, CancellationToken)"/>
    Task<InvoiceFile> ConvertFacturXToCiiAsync(Stream pdf, ConvertOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a CII XML to a Factur-X PDF/A-3, with the PDF rendered from the invoice data
    /// (<c>POST /v1/convert/cii/to/facturx</c>).
    /// </summary>
    /// <param name="xml">The CII invoice XML.</param>
    /// <param name="options">Language of the PDF and of the findings, or null for the defaults.</param>
    /// <param name="fileName">File name sent with the upload. Defaults to <c>invoice.xml</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The Factur-X PDF.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The result would not be compliant (see <see cref="InvoiceXmlApiException.Findings"/>), or the request was refused.
    /// </exception>
    Task<InvoiceFile> ConvertCiiToFacturXAsync(byte[] xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ConvertCiiToFacturXAsync(byte[], ConvertToPdfOptions?, string?, CancellationToken)"/>
    Task<InvoiceFile> ConvertCiiToFacturXAsync(Stream xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a UBL XML to a Factur-X PDF/A-3, with the PDF rendered from the invoice data
    /// (<c>POST /v1/convert/ubl/to/facturx</c>).
    /// </summary>
    /// <param name="xml">The UBL invoice XML.</param>
    /// <param name="options">Language of the PDF and of the findings, or null for the defaults.</param>
    /// <param name="fileName">File name sent with the upload. Defaults to <c>invoice.xml</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The Factur-X PDF.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The result would not be compliant (see <see cref="InvoiceXmlApiException.Findings"/>), or the request was refused.
    /// </exception>
    Task<InvoiceFile> ConvertUblToFacturXAsync(byte[] xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ConvertUblToFacturXAsync(byte[], ConvertToPdfOptions?, string?, CancellationToken)"/>
    Task<InvoiceFile> ConvertUblToFacturXAsync(Stream xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an XRechnung XML, UBL or CII, to a Factur-X PDF/A-3, with the PDF rendered from the
    /// invoice data (<c>POST /v1/convert/xrechnung/to/facturx</c>).
    /// </summary>
    /// <param name="xml">The XRechnung XML, UBL or CII.</param>
    /// <param name="options">Language of the PDF and of the findings, or null for the defaults.</param>
    /// <param name="fileName">File name sent with the upload. Defaults to <c>invoice.xml</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The Factur-X PDF.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The result would not be compliant (see <see cref="InvoiceXmlApiException.Findings"/>), or the request was refused.
    /// </exception>
    Task<InvoiceFile> ConvertXRechnungToFacturXAsync(byte[] xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ConvertXRechnungToFacturXAsync(byte[], ConvertToPdfOptions?, string?, CancellationToken)"/>
    Task<InvoiceFile> ConvertXRechnungToFacturXAsync(Stream xml, ConvertToPdfOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a Factur-X PDF like <see cref="ValidateFacturXAsync(byte[], string?, IEnumerable{string}?, CancellationToken)"/>
    /// and returns a printable PDF report with the verdict (<c>POST /v1/validate/facturx/report</c>).
    /// </summary>
    /// <param name="pdf">The Factur-X PDF.</param>
    /// <param name="fileName">File name sent with the upload. Defaults to <c>invoice.pdf</c>.</param>
    /// <param name="rules">
    /// Additional rule sets to apply, for example <see cref="FacturXRuleSets.BrFr"/>. Null or empty for none.
    /// </param>
    /// <param name="validationLanguage">Language of the whole report. Defaults to English.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report PDF and the verdict. An invalid invoice is a normal result, not an exception.</returns>
    /// <exception cref="InvoiceXmlApiException">The upload could not be read, or the request was refused.</exception>
    Task<ValidationReportResult> ValidateFacturXReportAsync(byte[] pdf, string? fileName = null, IEnumerable<string>? rules = null, PdfLanguage? validationLanguage = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ValidateFacturXReportAsync(byte[], string?, IEnumerable{string}?, PdfLanguage?, CancellationToken)"/>
    Task<ValidationReportResult> ValidateFacturXReportAsync(Stream pdf, string? fileName = null, IEnumerable<string>? rules = null, PdfLanguage? validationLanguage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns an ordinary PDF invoice into a Factur-X PDF/A-3 (<c>POST /v1/transform/to/facturx</c>).
    /// AI reads the PDF, so a person must check the result before it is used.
    /// A 429 on this method is not retried automatically, so wait for <c>Retry-After</c> before calling again if you want to retry.
    /// </summary>
    /// <param name="pdf">The PDF invoice.</param>
    /// <param name="options">Language, visual layer and language of the findings, or null for the defaults.</param>
    /// <param name="fileName">File name sent with the upload. Defaults to <c>invoice.pdf</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The Factur-X PDF.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The result would not be compliant (see <see cref="InvoiceXmlApiException.Findings"/>), or the request was refused.
    /// </exception>
    Task<InvoiceFile> TransformToFacturXAsync(byte[] pdf, TransformOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="TransformToFacturXAsync(byte[], TransformOptions?, string?, CancellationToken)"/>
    Task<InvoiceFile> TransformToFacturXAsync(Stream pdf, TransformOptions? options = null, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a Factur-X PDF or a CII invoice XML into an <see cref="InvoiceDocument"/> by
    /// deterministic extraction of its XML (<c>POST /v1/extract/json</c>).
    /// </summary>
    /// <param name="file">A Factur-X PDF or an invoice XML.</param>
    /// <param name="fileName">File name sent with the upload. Defaults by content type.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The invoice, ready to pass back to <see cref="CreateFacturXAsync"/>.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The PDF has no embedded XML (<see cref="InvoiceXmlErrorCode.NoEmbeddedXml"/>), or the request was refused.
    /// </exception>
    Task<InvoiceDocument> ExtractJsonAsync(byte[] file, string? fileName = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ExtractJsonAsync(byte[], string?, CancellationToken)"/>
    Task<InvoiceDocument> ExtractJsonAsync(Stream file, string? fileName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the supporting documents embedded in an e-invoice, such as timesheets or delivery
    /// notes, as one ZIP archive (<c>POST /v1/extract/attachments</c>).
    /// </summary>
    /// <param name="file">A Factur-X PDF or an invoice XML.</param>
    /// <param name="fileName">File name sent with the upload. Defaults by content type.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A ZIP archive with the original files.</returns>
    /// <exception cref="InvoiceXmlApiException">
    /// The invoice has no embedded attachments (<see cref="InvoiceXmlErrorCode.NoAttachments"/>), or the request was refused.
    /// </exception>
    Task<InvoiceFile> ExtractAttachmentsAsync(byte[] file, string? fileName = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ExtractAttachmentsAsync(byte[], string?, CancellationToken)"/>
    Task<InvoiceFile> ExtractAttachmentsAsync(Stream file, string? fileName = null, CancellationToken cancellationToken = default);
}
