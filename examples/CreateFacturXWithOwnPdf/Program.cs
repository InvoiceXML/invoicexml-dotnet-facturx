// Creates a Factur-X invoice that keeps your own PDF layout.
// The API downloads your PDF from a public URL, embeds the XML it generates from the invoice
// data into it, and returns a Factur-X PDF/A-3. Nothing about your PDF's look changes.

using InvoiceXml.FacturX;

// Replace these values with your own.
const string ApiKey = "YOUR_API_KEY";
const string PdfUrl = "https://example.com/factures/F-2026-001.pdf"; // public http(s) URL of your PDF
const string OutputPath = "F-2026-001-facturx.pdf";

using var client = new FacturXClient(ApiKey);

var invoice = new InvoiceDocument
{
    InvoiceNumber = "F-2026-001",
    Currency = CurrencyCode.EUR,
    Seller = new SellerParty
    {
        Name = "Atelier Dupont SARL",
        VatIdentifier = "FR32123456789",
        PostalAddress = new PostalAddress { City = "Paris", PostCode = "75011", Country = CountryCode.FR },
    },
    Buyer = new BuyerParty
    {
        Name = "Client Exemple SA",
        PostalAddress = new PostalAddress { City = "Lyon", PostCode = "69002", Country = CountryCode.FR },
    },
    Lines =
    {
        new InvoiceLine
        {
            Quantity = 10,
            UnitCode = UnitCodes.Hour,
            Item = new ItemInformation { Name = "Prestation de conseil" },
            PriceDetails = new PriceDetails { NetPrice = 120m },
            VatInformation = new LineVatInformation { CategoryCode = VatCategoryCode.S, Rate = 20 },
        },
    },
};

try
{
    // PdfUrl makes your PDF the visual layer. Without it, the API renders its own invoice layout.
    var facturX = await client.CreateFacturXAsync(invoice, new FacturXOptions
    {
        PdfUrl = PdfUrl,
        // Rules = { FacturXRuleSets.BrFr }, // optional: also check the French BR-FR rules (they need more data, see the README)
    });

    await facturX.SaveAsync(OutputPath);
    Console.WriteLine($"Saved the Factur-X invoice to {OutputPath}.");
}
catch (InvoiceXmlApiException ex) when (ex.IsValidationError)
{
    // The invoice data would not produce a compliant Factur-X invoice. Each finding names the field to fix.
    Console.WriteLine("The invoice is not compliant:");
    foreach (var finding in ex.Findings)
        Console.WriteLine($"  [{finding.Rule}] {finding.Message} ({string.Join(", ", finding.Fields)})");
}
catch (InvoiceXmlApiException ex)
{
    // For example an invalid API key, or a PDF URL the API could not download.
    Console.WriteLine($"The API refused the request: {ex.Message}");
}
