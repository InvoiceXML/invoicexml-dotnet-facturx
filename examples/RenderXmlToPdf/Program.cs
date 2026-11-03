// Turns any invoice XML into a readable PDF, for approval workflows and archives.
// CII (such as the XML inside a Factur-X invoice), UBL and XRechnung are all accepted;
// the format is detected from the XML.

using InvoiceXml.FacturX;

// Replace these values with your own.
const string ApiKey = "YOUR_API_KEY";
const string XmlPath = @"C:\factures\F-2026-001.xml";
const string OutputPath = "F-2026-001-apercu.pdf";

using var client = new FacturXClient(ApiKey);

try
{
    var xml = await File.ReadAllBytesAsync(XmlPath);

    var pdf = await client.RenderXmlToPdfAsync(xml, new RenderOptions
    {
        Language = PdfLanguage.Fr,                    // labels and date formats of the PDF
        IncludeQrCode = true,                         // optional: a payment QR code for banking apps
        // LogoUrl = "https://example.com/logo.png",  // optional: public URL of your logo, printed top left
    }, Path.GetFileName(XmlPath));

    await pdf.SaveAsync(OutputPath);
    Console.WriteLine($"Saved the PDF to {OutputPath}.");
}
catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
{
    Console.WriteLine($"Could not find {XmlPath}.");
    Console.WriteLine("Set XmlPath at the top of Program.cs to an invoice XML on your machine.");
}
catch (InvoiceXmlApiException ex)
{
    // For example an invalid API key, or a file that is not an invoice XML.
    Console.WriteLine($"The API refused the request: {ex.Message}");
}
