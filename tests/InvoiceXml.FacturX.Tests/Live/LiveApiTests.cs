using System.Text;
using Xunit.Abstractions;

namespace InvoiceXml.FacturX.Tests.Live;

/// <summary>
/// End-to-end checks against the production API. Skipped without an API key.
/// Run only these with: dotnet test --framework net8.0 --filter "FullyQualifiedName~Live"
/// </summary>
public class LiveApiTests(ITestOutputHelper output)
{
    [LiveFact]
    public async Task Readme_quick_start_creates_a_valid_facturx_that_reads_back()
    {
        using var client = new FacturXClient(LiveApi.ApiKey!);

        var pdf = await Run(() => client.CreateFacturXAsync(LiveInvoices.QuickStart()));
        await LiveApi.SaveAsync(pdf, "quick-start-facturx.pdf");
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf.ToArray(), 0, 4));
        output.WriteLine($"Created {pdf.FileName} ({pdf.Length} bytes)");

        var validation = await client.ValidateFacturXAsync(pdf.ToArray(), pdf.FileName);
        Report(validation);
        Assert.True(validation.Valid, validation.Detail);
        Assert.True(validation.Data?.HasEmbeddedXml);
        Assert.Equal("en16931", validation.Data?.Profile);
        Assert.NotNull(validation.Report);

        var extracted = await client.ExtractJsonAsync(pdf.ToArray(), pdf.FileName);
        Assert.Equal("F-2026-001", extracted.InvoiceNumber);
        Assert.Equal(CurrencyCode.EUR, extracted.Currency);
        Assert.Equal("Atelier Dupont SARL", extracted.Seller?.Name);
        Assert.Equal(CountryCode.FR, extracted.Buyer?.PostalAddress?.Country);
        Assert.Equal(1440.00m, extracted.Totals?.GrandTotalAmount);
        output.WriteLine($"Extracted {extracted.InvoiceNumber}, due {extracted.Totals?.DuePayableAmount} {extracted.Currency}");
    }

    [LiveFact]
    public async Task Readme_french_reform_invoice_passes_br_fr()
    {
        using var client = new FacturXClient(LiveApi.ApiKey!);

        var pdf = await Run(() => client.CreateFacturXAsync(
            LiveInvoices.FrenchReform(),
            new FacturXOptions { Language = PdfLanguage.Fr, Rules = { FacturXRuleSets.BrFr } }));
        await LiveApi.SaveAsync(pdf, "french-reform-facturx.pdf");

        var validation = await client.ValidateFacturXAsync(pdf.ToArray(), pdf.FileName, rules: [FacturXRuleSets.BrFr]);
        Report(validation);
        Assert.True(validation.Valid, validation.Detail);
        Assert.Empty(validation.Warnings);
    }

    [LiveFact]
    public async Task Own_pdf_and_cii_xml_become_a_valid_facturx()
    {
        using var client = new FacturXClient(LiveApi.ApiKey!);
        var source = await Run(() => client.CreateFacturXAsync(LiveInvoices.QuickStart()));

        // Test input: the CII XML of that invoice and a plain PDF rendering of it.
        var xml = await LiveApi.PostFileAsync("v1/extract/xml", source.ToArray(), "source.pdf", "application/pdf");
        var plainPdf = await LiveApi.PostFileAsync("v1/render/cii/to/pdf", xml, "factur-x.xml", "application/xml");

        var hybrid = await Run(() => client.EmbedFacturXAsync(plainPdf, xml, fileName: "facture.pdf"));
        await LiveApi.SaveAsync(hybrid, "embedded-facturx.pdf");
        Assert.Equal("application/pdf", hybrid.ContentType);

        var validation = await client.ValidateFacturXAsync(hybrid.ToArray(), hybrid.FileName);
        Report(validation);
        Assert.True(validation.Valid, validation.Detail);
    }

    [LiveFact]
    public async Task Embedded_supporting_document_comes_back_as_zip()
    {
        using var client = new FacturXClient(LiveApi.ApiKey!);
        var invoice = LiveInvoices.QuickStart();
        invoice.SupportingDocuments.Add(new SupportingDocument
        {
            Reference = "BL-2026-07",
            Description = "Bon de livraison juillet 2026",
            Attachment = Encoding.UTF8.GetBytes("Date;Heures\n2026-07-01;8\n"),
            AttachmentMimeCode = "text/csv",
            AttachmentFilename = "bon-de-livraison.csv",
        });

        var pdf = await Run(() => client.CreateFacturXAsync(invoice));
        var zip = await Run(() => client.ExtractAttachmentsAsync(pdf.ToArray(), pdf.FileName));
        await LiveApi.SaveAsync(zip, "attachments.zip");

        Assert.Equal("application/zip", zip.ContentType);
        Assert.Equal((byte)'P', zip.ToArray()[0]);
        Assert.Equal((byte)'K', zip.ToArray()[1]);
    }

    [LiveFact]
    public async Task Invoice_breaking_the_rules_is_refused_with_findings()
    {
        using var client = new FacturXClient(LiveApi.ApiKey!);
        var invoice = LiveInvoices.QuickStart();
        invoice.Seller!.VatIdentifier = null;

        var ex = await Assert.ThrowsAsync<InvoiceXmlApiException>(() => client.CreateFacturXAsync(invoice));

        output.WriteLine(ex.Message);
        Assert.True(ex.IsValidationError);
        Assert.NotEmpty(ex.Findings);
        Assert.NotNull(ex.TraceId);
    }

    // Prints the API's findings before failing, so a refused request is easy to diagnose.
    private async Task<T> Run<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (InvoiceXmlApiException ex)
        {
            output.WriteLine(ex.Message);
            foreach (var finding in ex.Findings)
                output.WriteLine($"  {finding} -> {string.Join(", ", finding.Fields)}");
            throw;
        }
    }

    private void Report(InvoiceValidationResult result)
    {
        output.WriteLine($"{(result.Valid ? "VALID" : "INVALID")}: {result.Detail} (profile {result.Data?.Profile})");
        foreach (var error in result.Errors)
            output.WriteLine($"  error {error} -> {string.Join(", ", error.Fields)}");
        foreach (var warning in result.Warnings)
            output.WriteLine($"  warning {warning}");
    }
}
