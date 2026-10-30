using System.Net;

namespace InvoiceXml.FacturX.Tests;

public class RenderConvertReportTransformTests
{
    private const string RefusedInvoice =
        """{"title":"Validation failed","status":400,"detail":"The invoice is not compliant.","errorCode":4001,"errors":[{"rule":"BR-CO-15","layer":"en16931","line":null,"message":"Le montant total est incorrect.","fields":["totals.grandTotalAmount"],"raw":null}]}""";

    private const string MissingFile =
        """{"title":"Missing file","status":400,"detail":"No file was uploaded.","errorCode":4003}""";

    private static readonly Dictionary<string, Route<ConvertOptions>> XmlRoutes = new()
    {
        ["facturx/to/ubl"] = new((c, f, o) => c.ConvertFacturXToUblAsync(f, o), (c, s, o) => c.ConvertFacturXToUblAsync(s, o)),
        ["facturx/to/cii"] = new((c, f, o) => c.ConvertFacturXToCiiAsync(f, o), (c, s, o) => c.ConvertFacturXToCiiAsync(s, o)),
    };

    private static readonly Dictionary<string, Route<ConvertToPdfOptions>> PdfRoutes = new()
    {
        ["cii/to/facturx"] = new((c, f, o) => c.ConvertCiiToFacturXAsync(f, o), (c, s, o) => c.ConvertCiiToFacturXAsync(s, o)),
        ["ubl/to/facturx"] = new((c, f, o) => c.ConvertUblToFacturXAsync(f, o), (c, s, o) => c.ConvertUblToFacturXAsync(s, o)),
        ["xrechnung/to/facturx"] = new((c, f, o) => c.ConvertXRechnungToFacturXAsync(f, o), (c, s, o) => c.ConvertXRechnungToFacturXAsync(s, o)),
    };

    private delegate Task<InvoiceFile> ByteCall<TOptions>(IFacturXClient client, byte[] file, TOptions? options)
        where TOptions : class;

    private delegate Task<InvoiceFile> StreamCall<TOptions>(IFacturXClient client, Stream file, TOptions? options)
        where TOptions : class;

    [Fact]
    public async Task RenderCii_sends_every_option()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");

        var file = await client.RenderCiiToPdfAsync(
            TestData.Xml,
            new RenderOptions { Language = PdfLanguage.Fr, LogoUrl = "https://example.com/logo.png", IncludeQrCode = true });

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/render/cii/to/pdf", request.Uri.AbsolutePath);
        Assert.Equal(TestData.Xml, request.Part("file").Body);
        Assert.Equal("fr", request.Part("language").Text);
        Assert.Equal("https://example.com/logo.png", request.Part("logoUrl").Text);
        Assert.Equal("true", request.Part("includeQrCode").Text);
        Assert.Equal("application/pdf", file.ContentType);
    }

    [Fact]
    public async Task RenderCii_omits_unset_options()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf").ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");

        await client.RenderCiiToPdfAsync(TestData.Xml);
        using var stream = new MemoryStream(TestData.Xml);
        await client.RenderCiiToPdfAsync(stream, new RenderOptions());

        Assert.All(handler.Requests, request => Assert.Equal("file", Assert.Single(request.Parts).Name));
        Assert.Equal(TestData.Xml, handler.Requests[1].Part("file").Body);
    }

    [Fact]
    public async Task RenderCii_sends_a_qr_code_set_to_false()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");

        await client.RenderCiiToPdfAsync(TestData.Xml, new RenderOptions { IncludeQrCode = false });

        Assert.Equal("false", handler.Requests[0].Part("includeQrCode").Text);
    }

    [Theory]
    [InlineData("facturx/to/ubl")]
    [InlineData("facturx/to/cii")]
    public async Task Convert_to_xml_sends_the_validation_language_and_no_language(string route)
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Xml, "application/xml", "invoice.xml");

        var file = await XmlRoutes[route].Bytes(client, TestData.Pdf, new ConvertOptions { ValidationLanguage = PdfLanguage.Fr });

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"/v1/convert/{route}", request.Uri.AbsolutePath);
        Assert.Equal(TestData.Pdf, request.Part("file").Body);
        Assert.Equal("invoice.pdf", request.Part("file").FileName);
        Assert.Equal("fr", request.Part("validationLanguage").Text);
        Assert.DoesNotContain(request.Parts, p => p.Name == "language");
        Assert.Equal("application/xml", file.ContentType);
    }

    [Theory]
    [InlineData("facturx/to/ubl")]
    [InlineData("facturx/to/cii")]
    public async Task Convert_to_xml_stream_overload_omits_unset_options(string route)
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Xml, "application/xml", "invoice.xml");

        using var stream = new MemoryStream(TestData.Pdf);
        await XmlRoutes[route].Stream(client, stream, null);

        Assert.Equal($"/v1/convert/{route}", handler.Requests[0].Uri.AbsolutePath);
        var part = Assert.Single(handler.Requests[0].Parts);
        Assert.Equal("file", part.Name);
        Assert.Equal(TestData.Pdf, part.Body);
    }

    [Theory]
    [InlineData("cii/to/facturx")]
    [InlineData("ubl/to/facturx")]
    [InlineData("xrechnung/to/facturx")]
    public async Task Convert_to_facturx_sends_both_languages(string route)
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");

        var file = await PdfRoutes[route].Bytes(
            client,
            TestData.Xml,
            new ConvertToPdfOptions { Language = PdfLanguage.Fr, ValidationLanguage = PdfLanguage.De });

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"/v1/convert/{route}", request.Uri.AbsolutePath);
        Assert.Equal(TestData.Xml, request.Part("file").Body);
        Assert.Equal("fr", request.Part("language").Text);
        Assert.Equal("de", request.Part("validationLanguage").Text);
        Assert.Equal("application/pdf", file.ContentType);
    }

    [Theory]
    [InlineData("cii/to/facturx")]
    [InlineData("ubl/to/facturx")]
    [InlineData("xrechnung/to/facturx")]
    public async Task Convert_to_facturx_stream_overload_omits_unset_options(string route)
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");

        using var stream = new MemoryStream(TestData.Xml);
        await PdfRoutes[route].Stream(client, stream, null);

        Assert.Equal($"/v1/convert/{route}", handler.Requests[0].Uri.AbsolutePath);
        Assert.Equal("file", Assert.Single(handler.Requests[0].Parts).Name);
    }

    [Fact]
    public async Task Convert_refusal_surfaces_the_findings()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(RefusedInvoice, HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() =>
            client.ConvertCiiToFacturXAsync(TestData.Xml, new ConvertToPdfOptions { ValidationLanguage = PdfLanguage.Fr }));

        Assert.Equal((int)InvoiceXmlErrorCode.XmlValidation, ex.ErrorCode);
        Assert.Equal("BR-CO-15", Assert.Single(ex.Findings).Rule);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, null)]
    public async Task ValidateFacturXReport_returns_the_pdf_and_the_verdict(string? header, bool? expected)
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyReport(TestData.Pdf, header);

        var report = await client.ValidateFacturXReportAsync(TestData.Pdf, "facture.pdf", validationLanguage: PdfLanguage.Fr);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/validate/facturx/report", request.Uri.AbsolutePath);
        Assert.Equal("facture.pdf", request.Part("file").FileName);
        Assert.Equal("fr", request.Part("validationLanguage").Text);
        Assert.DoesNotContain(request.Parts, p => p.Name == "rules");
        Assert.Equal(expected, report.Valid);
        Assert.Equal(TestData.Pdf, report.File.ToArray());
        Assert.Equal("application/pdf", report.File.ContentType);
    }

    [Fact]
    public async Task ValidateFacturXReport_sends_each_rule_set_as_a_rules_field()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyReport(TestData.Pdf, "true");

        await client.ValidateFacturXReportAsync(TestData.Pdf, rules: [FacturXRuleSets.BrFr, "another", " ", ""]);

        var rules = handler.Requests[0].Parts.Where(p => p.Name == "rules").Select(p => p.Text);
        Assert.Equal(["br-fr", "another"], rules);
    }

    [Fact]
    public async Task ValidateFacturXReport_omits_unset_options()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyReport(TestData.Pdf, "true");

        await client.ValidateFacturXReportAsync(TestData.Pdf);

        Assert.Equal("file", Assert.Single(handler.Requests[0].Parts).Name);
    }

    [Fact]
    public async Task Report_with_an_unreadable_verdict_is_null()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyReport(TestData.Pdf, "maybe");

        var report = await client.ValidateFacturXReportAsync(TestData.Pdf);

        Assert.Null(report.Valid);
    }

    [Fact]
    public async Task Report_stream_overload_uploads_the_stream()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyReport(TestData.Pdf, "false");

        using var stream = new MemoryStream(TestData.Pdf);
        var report = await client.ValidateFacturXReportAsync(stream, rules: [FacturXRuleSets.BrFr]);

        Assert.Equal(TestData.Pdf, handler.Requests[0].Part("file").Body);
        Assert.Equal("br-fr", handler.Requests[0].Part("rules").Text);
        Assert.False(report.Valid);
    }

    [Fact]
    public async Task Report_problem_response_throws()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(MissingFile, HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.ValidateFacturXReportAsync(TestData.Pdf));

        Assert.Equal((int)InvoiceXmlErrorCode.MissingFile, ex.ErrorCode);
    }

    [Fact]
    public async Task TransformToFacturX_sends_every_option()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "facture.pdf");

        var file = await client.TransformToFacturXAsync(
            TestData.Pdf,
            new TransformOptions
            {
                Language = PdfLanguage.Fr,
                PreserveOriginalPdf = true,
                IncludeAdvancedProperties = false,
                ValidationLanguage = PdfLanguage.De,
            },
            "facture.pdf");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/transform/to/facturx", request.Uri.AbsolutePath);
        Assert.Equal("facture.pdf", request.Part("file").FileName);
        Assert.Equal("fr", request.Part("language").Text);
        Assert.Equal("true", request.Part("preserveOriginalPdf").Text);
        Assert.Equal("false", request.Part("includeAdvancedProperties").Text);
        Assert.Equal("de", request.Part("validationLanguage").Text);
        Assert.Equal("facture.pdf", file.FileName);
    }

    [Fact]
    public async Task TransformToFacturX_omits_unset_options()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf").ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");

        await client.TransformToFacturXAsync(TestData.Pdf);
        using var stream = new MemoryStream(TestData.Pdf);
        await client.TransformToFacturXAsync(stream, new TransformOptions());

        Assert.All(handler.Requests, request => Assert.Equal("file", Assert.Single(request.Parts).Name));
        Assert.Equal(TestData.Pdf, handler.Requests[1].Part("file").Body);
    }

    [Fact]
    public async Task Transform_refusal_surfaces_the_findings()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(RefusedInvoice, HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.TransformToFacturXAsync(TestData.Pdf));

        Assert.Equal("BR-CO-15", Assert.Single(ex.Findings).Rule);
    }

    [Fact]
    public async Task Transform_too_many_requests_makes_one_request_and_throws()
    {
        var (client, handler) = TestData.CreateClient();
        var delays = new List<TimeSpan>();
        client.Transport.Delay = (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        };
        handler.ReplyTooManyRequests(TimeSpan.FromSeconds(7));

        var ex = await Assert.ThrowsAsync<InvoiceXmlRateLimitException>(() => client.TransformToFacturXAsync(TestData.Pdf));

        Assert.Equal("/v1/transform/to/facturx", Assert.Single(handler.Requests).Uri.AbsolutePath);
        Assert.Empty(delays);
        Assert.Equal((HttpStatusCode)429, ex.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), ex.RetryAfter);
    }

    [Fact]
    public async Task Render_convert_and_report_still_retry_too_many_requests()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyTooManyRequests(TimeSpan.FromSeconds(1)).ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");
        handler.ReplyTooManyRequests(TimeSpan.FromSeconds(1)).ReplyFile(TestData.Xml, "application/xml", "invoice.xml");
        handler.ReplyTooManyRequests(TimeSpan.FromSeconds(1)).ReplyReport(TestData.Pdf, "true");

        await client.RenderCiiToPdfAsync(TestData.Xml);
        await client.ConvertFacturXToUblAsync(TestData.Pdf);
        await client.ValidateFacturXReportAsync(TestData.Pdf);

        Assert.Equal(
            ["/v1/render/cii/to/pdf", "/v1/render/cii/to/pdf", "/v1/convert/facturx/to/ubl", "/v1/convert/facturx/to/ubl", "/v1/validate/facturx/report", "/v1/validate/facturx/report"],
            handler.Requests.Select(r => r.Uri.AbsolutePath));
    }

    [Fact]
    public async Task Null_files_are_refused_before_any_request()
    {
        var (client, handler) = TestData.CreateClient();

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.RenderCiiToPdfAsync((byte[])null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ConvertFacturXToUblAsync((byte[])null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ConvertCiiToFacturXAsync((byte[])null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ValidateFacturXReportAsync((byte[])null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.TransformToFacturXAsync((byte[])null!));

        Assert.Empty(handler.Requests);
    }

    private sealed record Route<TOptions>(ByteCall<TOptions> Bytes, StreamCall<TOptions> Stream)
        where TOptions : class;
}
