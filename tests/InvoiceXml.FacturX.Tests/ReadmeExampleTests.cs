using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceXml.FacturX.Tests;

/// <summary>Keeps the README snippets compiling against the real API surface.</summary>
public class ReadmeExampleTests
{
    [Fact]
    public async Task Quick_start_invoice_is_sent_as_documented()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyFile(TestData.Pdf, "application/pdf", "invoice-facturx-F-2026-001.pdf");

        // Same statements as the README quick start, on a mocked transport.
        var pdf = await client.CreateFacturXAsync(new InvoiceDocument
        {
            InvoiceNumber = "F-2026-001",
            Currency = CurrencyCode.EUR,
            Seller = new SellerParty { Name = "Atelier Dupont SARL", VatIdentifier = "FR32123456789", PostalAddress = new PostalAddress { City = "Paris", PostCode = "75011", Country = CountryCode.FR } },
            Buyer = new BuyerParty { Name = "Client Exemple SA", PostalAddress = new PostalAddress { City = "Lyon", PostCode = "69002", Country = CountryCode.FR } },
            Lines = { new InvoiceLine { Quantity = 10, UnitCode = UnitCodes.Hour, Item = new ItemInformation { Name = "Prestation de conseil" }, PriceDetails = new PriceDetails { NetPrice = 120m }, VatInformation = new LineVatInformation { CategoryCode = VatCategoryCode.S, Rate = 20 } } },
        });

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        var invoice = json.RootElement.GetProperty("invoice");
        Assert.Equal("Atelier Dupont SARL", invoice.GetProperty("seller").GetProperty("name").GetString());
        Assert.Equal("Prestation de conseil", invoice.GetProperty("lines")[0].GetProperty("item").GetProperty("name").GetString());
        Assert.Equal("invoice-facturx-F-2026-001.pdf", pdf.FileName);
    }

    [Fact]
    public async Task Email_example_is_sent_as_documented()
    {
        var (client, handler) = TestData.CreateClient();
        handler.ReplyJson("""{"status":"accepted","messageId":"0102019a-test"}""");
        handler.ReplyJson("""{"status":"accepted","messageId":"0102019a-test2"}""");
        var invoice = TestData.Invoice();

        // Same statements as the README email section, on a mocked transport.
        var email = new EmailMessage
        {
            From = "invoices@yourcompany.com",
            FromName = "Your Company Billing",
            To = "comptabilite@client.example",
            Cc = { "archive@yourcompany.com" },
            Subject = "Facture F-2026-001",
            Message = "Veuillez trouver ci-joint votre facture.",
        };

        var sent = await client.EmailFacturXAsync(invoice, email, new FacturXOptions { Language = PdfLanguage.Fr });
        await client.EmailEmbeddedFacturXAsync(TestData.Pdf, TestData.Xml, email, rules: new[] { FacturXRuleSets.BrFr });

        Assert.Equal("accepted", sent.Status);
        Assert.Equal("0102019a-test", sent.MessageId);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void Http_client_factory_registration_resolves_the_interface()
    {
        var services = new ServiceCollection();
        services.AddHttpClient<IFacturXClient, FacturXClient>((http, _) =>
            new FacturXClient(http, "YOUR_API_KEY"));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FacturXClient>(provider.GetRequiredService<IFacturXClient>());
    }
}
