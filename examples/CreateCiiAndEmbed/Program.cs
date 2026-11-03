// Builds a CII XML from your invoice data, then embeds it into a PDF you already have.
// Use this when your own system prints the invoice PDF and you want it to become a
// Factur-X invoice. The CII XML declares the EN 16931 profile, so it embeds as it is.

using InvoiceXml.FacturX;

// Replace these values with your own.
const string ApiKey = "YOUR_API_KEY";
const string PdfPath = @"C:\factures\F-2026-001.pdf"; // the PDF your system printed
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
    // Read the PDF first, so a wrong path stops the example before any API call.
    var pdf = await File.ReadAllBytesAsync(PdfPath);

    // 1. Build the CII XML from the invoice data. Language words the default texts, such as the payment terms.
    var cii = await client.CreateCiiAsync(invoice, new CiiOptions
    {
        Language = PdfLanguage.Fr,
        // Rules = { FacturXRuleSets.BrFr }, // optional: also check the French BR-FR rules (they need more data, see the README)
    });

    // 2. Embed the XML into your PDF. The result is a Factur-X PDF/A-3.
    var facturX = await client.EmbedFacturXAsync(pdf, cii.ToArray(), fileName: Path.GetFileName(PdfPath));

    await facturX.SaveAsync(OutputPath);
    Console.WriteLine($"Saved the Factur-X invoice to {OutputPath}.");
}
catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
{
    Console.WriteLine($"Could not find {PdfPath}.");
    Console.WriteLine("Set PdfPath at the top of Program.cs to a PDF on your machine.");
}
catch (InvoiceXmlApiException ex) when (ex.IsValidationError)
{
    // The invoice data would not produce a compliant invoice. Each finding names the field to fix.
    Console.WriteLine("The invoice is not compliant:");
    foreach (var finding in ex.Findings)
        Console.WriteLine($"  [{finding.Rule}] {finding.Message} ({string.Join(", ", finding.Fields)})");
}
catch (InvoiceXmlApiException ex)
{
    Console.WriteLine($"The API refused the request: {ex.Message}");
}
