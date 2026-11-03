// Converts a UBL invoice XML, for example one received over Peppol, into a Factur-X invoice:
// a readable PDF with the invoice data embedded as CII XML.

using InvoiceXml.FacturX;

// Replace these values with your own.
const string ApiKey = "YOUR_API_KEY";
const string UblXmlPath = @"C:\factures\F-2026-001-ubl.xml";
const string OutputPath = "F-2026-001-facturx.pdf";

using var client = new FacturXClient(ApiKey);

try
{
    var ubl = await File.ReadAllBytesAsync(UblXmlPath);

    // Language sets the PDF layout and the default texts added when the UBL has none.
    var facturX = await client.ConvertUblToFacturXAsync(ubl, new ConvertToPdfOptions { Language = PdfLanguage.Fr }, Path.GetFileName(UblXmlPath));

    await facturX.SaveAsync(OutputPath);
    Console.WriteLine($"Saved the Factur-X invoice to {OutputPath}.");
}
catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
{
    Console.WriteLine($"Could not find {UblXmlPath}.");
    Console.WriteLine("Set UblXmlPath at the top of Program.cs to a UBL invoice XML on your machine.");
}
catch (InvoiceXmlApiException ex) when (ex.IsValidationError)
{
    // The result would not be a compliant Factur-X invoice. Each finding names the field to fix.
    Console.WriteLine("The converted invoice would not be compliant:");
    foreach (var finding in ex.Findings)
        Console.WriteLine($"  [{finding.Rule}] {finding.Message} ({string.Join(", ", finding.Fields)})");
}
catch (InvoiceXmlApiException ex)
{
    Console.WriteLine($"The API refused the request: {ex.Message}");
}
