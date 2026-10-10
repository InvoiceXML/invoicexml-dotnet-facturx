using System.Net;
using System.Text.Json;

namespace InvoiceXml.FacturX.Tests;

public class CreateTests
{
    [Fact]
    public async Task CreateFacturX_posts_invoice_envelope_with_bearer_key_and_returns_pdf()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice-facturx-F-2026-001.pdf");

        var file = await client.CreateFacturXAsync(
            TestData.Invoice(),
            new FacturXOptions { Language = PdfLanguage.Fr, BrandColor = "#0F766E", IncludeAdvancedProperties = true });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.invoicexml.com/v1/create/facturx", request.Uri.ToString());
        Assert.Equal("Bearer", request.Authorization?.Scheme);
        Assert.Equal("test-key", request.Authorization?.Parameter);
        Assert.StartsWith("InvoiceXml.FacturX/", request.UserAgent);
        Assert.Equal("application/json", request.ContentType);

        Assert.Equal(TestData.Pdf, file.ToArray());
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("invoice-facturx-F-2026-001.pdf", file.FileName);
    }

    [Fact]
    public async Task CreateFacturX_serializes_the_api_wire_format()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");

        await client.CreateFacturXAsync(TestData.Invoice(), new FacturXOptions { Language = PdfLanguage.Fr });

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        var invoice = json.RootElement.GetProperty("invoice");
        Assert.Equal("F-2026-001", invoice.GetProperty("invoiceNumber").GetString());
        Assert.Equal("2026-10-09", invoice.GetProperty("issueDate").GetString());
        Assert.Equal("380", invoice.GetProperty("typeCode").GetString());
        Assert.Equal("EUR", invoice.GetProperty("currency").GetString());
        Assert.Equal("FR", invoice.GetProperty("seller").GetProperty("postalAddress").GetProperty("country").GetString());
        Assert.Equal("0002", invoice.GetProperty("seller").GetProperty("legalRegistration").GetProperty("schemeId").GetString());
        Assert.Equal("58", invoice.GetProperty("paymentDetails").GetProperty("paymentMeansCode").GetString());

        var notes = invoice.GetProperty("notes");
        Assert.Equal("Merci pour votre confiance.", notes[0].GetString());
        Assert.Equal("AAB", notes[1].GetProperty("subjectCode").GetString());

        var line = invoice.GetProperty("lines")[0];
        Assert.Equal(10m, line.GetProperty("quantity").GetDecimal());
        Assert.Equal("HUR", line.GetProperty("unitCode").GetString());
        Assert.Equal(120m, line.GetProperty("priceDetails").GetProperty("netPrice").GetDecimal());
        Assert.Equal("S", line.GetProperty("vatInformation").GetProperty("categoryCode").GetString());

        // Unset values are omitted rather than sent as null.
        Assert.False(invoice.TryGetProperty("dueDate", out _));
        Assert.False(invoice.TryGetProperty("totals", out _));

        var options = json.RootElement.GetProperty("options");
        Assert.Equal("fr", options.GetProperty("language").GetString());
        Assert.False(options.TryGetProperty("rules", out _));
    }

    [Fact]
    public async Task CreateFacturX_sends_extra_rule_sets_in_options()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");

        await client.CreateFacturXAsync(TestData.Invoice(), new FacturXOptions { Rules = { FacturXRuleSets.BrFr } });

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        var rules = json.RootElement.GetProperty("options").GetProperty("rules");
        Assert.Equal("br-fr", Assert.Single(rules.EnumerateArray()).GetString());
    }

    [Fact]
    public async Task CreateFacturX_with_a_profile_declares_its_identifier()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice.pdf");
        var invoice = TestData.Invoice();
        invoice.SpecificationId = FacturXProfiles.ExtendedCtcFr;

        await client.CreateFacturXAsync(invoice);

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(
            "urn:cen.eu:en16931:2017#conformant#urn.cpro.gouv.fr:1p0:extended-ctc-fr",
            json.RootElement.GetProperty("invoice").GetProperty("specificationId").GetString());
        Assert.False(json.RootElement.TryGetProperty("options", out _));
    }

    [Fact]
    public async Task Create_rejected_by_validation_throws_with_findings()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(
            """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "detail": "The invoice failed validation with 1 error(s).",
              "valid": false,
              "errorCode": 4001,
              "traceId": "00-abc-01",
              "errors": [
                {
                  "rule": "BR-CO-15",
                  "layer": "en16931",
                  "line": null,
                  "message": "The total amount with VAT does not match.",
                  "fields": ["totals.grandTotalAmount"],
                  "raw": "Original validator output."
                }
              ],
              "warnings": []
            }
            """,
            HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.CreateFacturXAsync(TestData.Invoice()));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal((int)InvoiceXmlErrorCode.XmlValidation, ex.ErrorCode);
        Assert.True(ex.IsValidationError);
        Assert.Equal("00-abc-01", ex.TraceId);
        var finding = Assert.Single(ex.Findings);
        Assert.Equal("BR-CO-15", finding.Rule);
        Assert.Equal(ValidationLayers.En16931, finding.Layer);
        Assert.Null(finding.Line);
        Assert.Equal(["totals.grandTotalAmount"], finding.Fields);
        Assert.Contains("BR-CO-15", ex.Message);
        Assert.Contains("4001", ex.Message);
    }

    [Fact]
    public async Task Model_validation_errors_carry_field_paths_without_rule()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(
            """{"title":"One or more validation errors occurred.","status":400,"errorCode":4002,"errors":[{"rule":null,"layer":null,"line":1,"message":"The value is not a valid currency code.","fields":["currency"],"raw":null}]}""",
            HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.CreateFacturXAsync(TestData.Invoice()));

        Assert.True(ex.IsValidationError);
        var finding = Assert.Single(ex.Findings);
        Assert.Null(finding.Rule);
        Assert.Equal(1, finding.Line);
        Assert.Equal("The value is not a valid currency code.", finding.ToString());
    }

    [Fact]
    public async Task Non_json_error_body_still_produces_a_useful_exception()
    {
        var (client, handler) = TestData.CreateClient();
        handler.Reply(() => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>Bad gateway</html>") });

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.CreateFacturXAsync(TestData.Invoice()));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Null(ex.ErrorCode);
        Assert.Empty(ex.Findings);
        Assert.Contains("502", ex.Message);
        Assert.Equal("<html>Bad gateway</html>", ex.ResponseBody);
    }

    [Fact]
    public async Task Create_rejects_null_invoice()
    {
        var (client, _) = TestData.CreateClient();

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.CreateFacturXAsync(null!));
    }
}
