using System.Net;
using System.Text.Json;

namespace InvoiceXml.FacturX.Tests;

public class EmailTests
{
    private const string Accepted = """{"status":"accepted","messageId":"0102019a-test"}""";

    private static EmailMessage FullEmail() => new()
    {
        From = "factures@atelier-dupont.fr",
        FromName = "Atelier Dupont",
        To = "comptabilite@client-exemple.fr",
        Cc = { "archive@atelier-dupont.fr", "direction@atelier-dupont.fr" },
        Bcc = { "audit@atelier-dupont.fr" },
        ReplyTo = "contact@atelier-dupont.fr",
        Subject = "Facture F-2026-001",
        Message = "Veuillez trouver ci-joint votre facture.",
    };

    private static EmailMessage MinimalEmail() => new() { From = "factures@atelier-dupont.fr", To = "comptabilite@client-exemple.fr" };

    [Fact]
    public async Task EmailFacturX_posts_invoice_options_and_email_and_returns_the_message_id()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(Accepted);

        var result = await client.EmailFacturXAsync(
            TestData.Invoice(),
            FullEmail(),
            new FacturXOptions { Language = PdfLanguage.Fr, Rules = { FacturXRuleSets.BrFr } });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.invoicexml.com/v1/create/facturx/email", request.Uri.ToString());
        Assert.Equal("test-key", request.Authorization?.Parameter);
        Assert.Equal("application/json", request.ContentType);

        using var json = JsonDocument.Parse(request.Body);
        Assert.Equal("F-2026-001", json.RootElement.GetProperty("invoice").GetProperty("invoiceNumber").GetString());
        var options = json.RootElement.GetProperty("options");
        Assert.Equal("fr", options.GetProperty("language").GetString());
        Assert.Equal(FacturXRuleSets.BrFr, options.GetProperty("rules")[0].GetString());

        var email = json.RootElement.GetProperty("email");
        Assert.Equal("factures@atelier-dupont.fr", email.GetProperty("from").GetString());
        Assert.Equal("Atelier Dupont", email.GetProperty("fromName").GetString());
        Assert.Equal("comptabilite@client-exemple.fr", email.GetProperty("to").GetString());
        Assert.Equal("archive@atelier-dupont.fr", email.GetProperty("cc")[0].GetString());
        Assert.Equal("direction@atelier-dupont.fr", email.GetProperty("cc")[1].GetString());
        Assert.Equal("audit@atelier-dupont.fr", email.GetProperty("bcc")[0].GetString());
        Assert.Equal("contact@atelier-dupont.fr", email.GetProperty("replyTo").GetString());
        Assert.Equal("Facture F-2026-001", email.GetProperty("subject").GetString());
        Assert.Equal("Veuillez trouver ci-joint votre facture.", email.GetProperty("message").GetString());

        Assert.Equal("accepted", result.Status);
        Assert.Equal("0102019a-test", result.MessageId);
    }

    [Fact]
    public async Task Create_email_omits_unset_fields()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(Accepted);

        await client.EmailFacturXAsync(TestData.Invoice(), MinimalEmail());

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.False(json.RootElement.TryGetProperty("options", out _));
        var names = json.RootElement.GetProperty("email").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(["from", "to"], names);
    }

    [Fact]
    public async Task Blank_copy_addresses_are_dropped()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(Accepted);
        var email = MinimalEmail();
        email.Cc = [" ", " archive@atelier-dupont.fr "];
        email.Bcc = [""];

        await client.EmailFacturXAsync(TestData.Invoice(), email);

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        var body = json.RootElement.GetProperty("email");
        Assert.Equal("archive@atelier-dupont.fr", Assert.Single(body.GetProperty("cc").EnumerateArray()).GetString());
        Assert.False(body.TryGetProperty("bcc", out _));
    }

    [Fact]
    public async Task EmailEmbeddedFacturX_sends_files_rules_and_email_fields()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(Accepted);

        var result = await client.EmailEmbeddedFacturXAsync(TestData.Pdf, TestData.Xml, FullEmail(), "facture.pdf", [FacturXRuleSets.BrFr]);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/embed/facturx/email", request.Uri.AbsolutePath);
        Assert.Equal(TestData.Pdf, request.Part("pdf").Body);
        Assert.Equal("facture.pdf", request.Part("pdf").FileName);
        Assert.Equal(TestData.Xml, request.Part("xml").Body);
        Assert.Equal("factur-x.xml", request.Part("xml").FileName);
        Assert.Equal(FacturXRuleSets.BrFr, request.Part("rules").Text);
        Assert.Equal("factures@atelier-dupont.fr", request.Part("email.from").Text);
        Assert.Equal("Atelier Dupont", request.Part("email.fromName").Text);
        Assert.Equal("comptabilite@client-exemple.fr", request.Part("email.to").Text);
        Assert.Equal("audit@atelier-dupont.fr", request.Part("email.bcc").Text);
        Assert.Equal("contact@atelier-dupont.fr", request.Part("email.replyTo").Text);
        Assert.Equal("Facture F-2026-001", request.Part("email.subject").Text);
        Assert.Equal("Veuillez trouver ci-joint votre facture.", request.Part("email.message").Text);

        // These routes always validate.
        Assert.DoesNotContain(request.Parts, p => p.Name == "skipValidation");

        Assert.Equal("accepted", result.Status);
        Assert.Equal("0102019a-test", result.MessageId);
    }

    [Fact]
    public async Task EmailEmbeddedFacturX_sends_one_form_part_per_cc_address()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(Accepted);

        await client.EmailEmbeddedFacturXAsync(TestData.Pdf, TestData.Xml, FullEmail());

        var cc = handler.Requests[0].Parts.Where(p => p.Name == "email.cc").Select(p => p.Text).ToArray();
        Assert.Equal(["archive@atelier-dupont.fr", "direction@atelier-dupont.fr"], cc);
    }

    [Fact]
    public async Task EmailEmbeddedFacturX_omits_unset_fields()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(Accepted);

        await client.EmailEmbeddedFacturXAsync(TestData.Pdf, TestData.Xml, MinimalEmail());

        var names = handler.Requests[0].Parts.Select(p => p.Name!).ToArray();
        Assert.Equal(["pdf", "xml", "email.from", "email.to"], names);
        Assert.Equal("invoice.pdf", handler.Requests[0].Part("pdf").FileName);
    }

    [Fact]
    public async Task Stream_overload_of_embed_email_buffers_the_uploads()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(Accepted);

        using var pdf = new MemoryStream(TestData.Pdf);
        using var xml = new MemoryStream(TestData.Xml);
        await client.EmailEmbeddedFacturXAsync(pdf, xml, MinimalEmail(), rules: [FacturXRuleSets.BrFr]);

        Assert.Equal(TestData.Pdf, handler.Requests[0].Part("pdf").Body);
        Assert.Equal(TestData.Xml, handler.Requests[0].Part("xml").Body);
        Assert.Equal(FacturXRuleSets.BrFr, handler.Requests[0].Part("rules").Text);
    }

    [Fact]
    public async Task Unverified_sender_domain_surfaces_through_the_exception()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(
            """{"title":"Sending domain not verified","status":400,"detail":"The sending domain atelier-dupont.fr is not verified yet.","errorCode":4022,"traceId":"00-abc-01"}""",
            HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.EmailFacturXAsync(TestData.Invoice(), MinimalEmail()));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal((int)InvoiceXmlErrorCode.EmailDomainNotVerified, ex.ErrorCode);
        Assert.False(ex.IsValidationError);
        Assert.Equal("The sending domain atelier-dupont.fr is not verified yet.", ex.Detail);
        Assert.Contains("4022", ex.Message);
    }

    [Theory]
    [InlineData(4020, InvoiceXmlErrorCode.EmailNotInPlan)]
    [InlineData(4021, InvoiceXmlErrorCode.EmailSendingDisabled)]
    public async Task Refused_email_sending_surfaces_its_error_code(int code, InvoiceXmlErrorCode expected)
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson($$"""{"title":"Refused","status":403,"detail":"Refused.","errorCode":{{code}}}""", HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.EmailEmbeddedFacturXAsync(TestData.Pdf, TestData.Xml, MinimalEmail()));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal((int)expected, ex.ErrorCode);
    }

    [Fact]
    public async Task Bad_address_surfaces_as_a_field_finding()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson(
            """{"title":"One or more validation errors occurred.","status":400,"errorCode":4002,"errors":[{"rule":null,"layer":null,"line":null,"message":"The address is not valid.","fields":["email.to"],"raw":null}]}""",
            HttpStatusCode.BadRequest);

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.EmailFacturXAsync(TestData.Invoice(), MinimalEmail()));

        Assert.True(ex.IsValidationError);
        Assert.Equal(["email.to"], Assert.Single(ex.Findings).Fields);
    }

    [Fact]
    public async Task Too_many_requests_is_retried_because_nothing_was_sent()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyTooManyRequests(TimeSpan.FromSeconds(1));
        handler.ReplyJson(Accepted);

        var result = await client.EmailEmbeddedFacturXAsync(TestData.Pdf, TestData.Xml, FullEmail());

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("comptabilite@client-exemple.fr", handler.Requests[1].Part("email.to").Text);
        Assert.Equal("accepted", result.Status);
    }

    [Fact]
    public async Task Server_error_is_not_retried_because_the_email_may_have_gone_out()
    {
        var (client, handler) = TestData.CreateClient();
        handler.Reply(() => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>Bad gateway</html>") });

        await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.EmailFacturXAsync(TestData.Invoice(), MinimalEmail()));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Network_failure_is_not_retried_because_the_email_may_have_gone_out()
    {
        var (client, handler) = TestData.CreateClient();
        handler.Reply(() => throw new HttpRequestException("Connection reset."));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.EmailEmbeddedFacturXAsync(TestData.Pdf, TestData.Xml, MinimalEmail()));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Email_methods_reject_null_arguments()
    {
        var (client, _) = TestData.CreateClient();

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.EmailFacturXAsync(null!, MinimalEmail()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.EmailFacturXAsync(TestData.Invoice(), null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.EmailEmbeddedFacturXAsync((byte[])null!, TestData.Xml, MinimalEmail()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.EmailEmbeddedFacturXAsync(TestData.Pdf, (byte[])null!, MinimalEmail()));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.EmailEmbeddedFacturXAsync(TestData.Pdf, TestData.Xml, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.EmailEmbeddedFacturXAsync(Stream.Null, Stream.Null, null!));
    }
}
