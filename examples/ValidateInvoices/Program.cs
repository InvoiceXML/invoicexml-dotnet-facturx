// Validates an incoming Factur-X invoice against the rules of the profile it declares, and
// optionally the French BR-FR rules. A failed validation is a normal result, not an exception:
// you get the verdict and every finding with the rule, the message and the field it concerns.
// The example also saves the printable validation report PDF, for example to send back to a supplier.

using InvoiceXml.FacturX;

// Replace these values with your own.
const string ApiKey = "YOUR_API_KEY";
const string FacturXPdfPath = @"C:\factures\facture-fournisseur.pdf";
const string ReportPath = "facture-fournisseur-rapport.pdf";

// Optional: also check the French BR-FR rules, the set the French platforms apply to domestic invoices.
string[] rules = []; // for example [FacturXRuleSets.BrFr]

using var client = new FacturXClient(ApiKey);

try
{
    var pdf = await File.ReadAllBytesAsync(FacturXPdfPath);
    var fileName = Path.GetFileName(FacturXPdfPath);

    // 1. The verdict and the findings as data.
    var result = await client.ValidateFacturXAsync(pdf, fileName, rules);
    Console.WriteLine($"{(result.Valid ? "Valid" : "Not valid")}. {result.Detail}");

    foreach (var error in result.Errors)
        Console.WriteLine($"  error   [{error.Rule}] {error.Message} ({string.Join(", ", error.Fields)})");

    // Warnings never make an invoice invalid, but are worth reviewing.
    foreach (var warning in result.Warnings)
        Console.WriteLine($"  warning [{warning.Rule}] {warning.Message} ({string.Join(", ", warning.Fields)})");

    // 2. The same check as a printable PDF report, worded in French.
    var report = await client.ValidateFacturXReportAsync(pdf, fileName, rules, PdfLanguage.Fr);
    await report.File.SaveAsync(ReportPath);
    Console.WriteLine($"Saved the validation report to {ReportPath} (valid: {report.Valid}).");
}
catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
{
    Console.WriteLine($"Could not find {FacturXPdfPath}.");
    Console.WriteLine("Set FacturXPdfPath at the top of Program.cs to a Factur-X PDF on your machine.");
}
catch (InvoiceXmlApiException ex)
{
    // For example an invalid API key, or a file that is not a PDF.
    Console.WriteLine($"The API refused the request: {ex.Message}");
}
