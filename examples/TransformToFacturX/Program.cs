// Turns an ordinary PDF invoice, for example one from an older billing system or a supplier,
// into a Factur-X invoice. AI reads the PDF, so a person must check the result before it is used.
// The rendered PDF ends with a section of technical properties to make that check easy.

using InvoiceXml.FacturX;

// Replace these values with your own.
const string ApiKey = "YOUR_API_KEY";
const string PdfPath = @"C:\factures\F-2026-001.pdf"; // an ordinary PDF invoice
const string OutputPath = "F-2026-001-facturx.pdf";

using var client = new FacturXClient(ApiKey);

try
{
    // Read the PDF first, so a wrong path stops the example before any API call.
    var pdf = await File.ReadAllBytesAsync(PdfPath);

    var facturX = await client.TransformToFacturXAsync(pdf, new TransformOptions
    {
        // Language = PdfLanguage.Fr,          // optional: left empty, the language the invoice is written in is used
        // PreserveOriginalPdf = true,         // optional: keep your PDF as the visual layer instead of a fresh layout
        // IncludeAdvancedProperties = false,  // optional: leave out the technical properties section
    }, Path.GetFileName(PdfPath));

    await facturX.SaveAsync(OutputPath);
    Console.WriteLine($"Saved the Factur-X invoice to {OutputPath}. Check it before you send or book it.");
}
catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
{
    Console.WriteLine($"Could not find {PdfPath}.");
    Console.WriteLine("Set PdfPath at the top of Program.cs to a PDF invoice on your machine.");
}
catch (InvoiceXmlApiException ex) when (ex.ErrorCode == (int)InvoiceXmlErrorCode.TotalsMismatch)
{
    // The amounts read from the PDF do not add up to its printed total, so no invoice was created.
    Console.WriteLine("The lines, allowances, charges and VAT read from the PDF do not add up to its printed total.");
    Console.WriteLine("Check the PDF, or create the invoice from your own data with CreateFacturXAsync instead.");
}
catch (InvoiceXmlRateLimitException ex)
{
    // Transform calls are not retried automatically, because a retry repeats the AI read.
    Console.WriteLine(ex.RetryAfter is { } wait
        ? $"Too many requests. Wait {wait.TotalSeconds:0} seconds before trying again."
        : "Too many requests. Wait a moment before trying again.");
}
catch (InvoiceXmlApiException ex) when (ex.IsValidationError)
{
    // The data read from the PDF would not make a compliant Factur-X invoice.
    Console.WriteLine("The invoice read from the PDF is not compliant:");
    foreach (var finding in ex.Findings)
        Console.WriteLine($"  [{finding.Rule}] {finding.Message} ({string.Join(", ", finding.Fields)})");
}
catch (InvoiceXmlApiException ex)
{
    // For example an invalid API key, or a file that is not a PDF invoice.
    Console.WriteLine($"The API refused the request: {ex.Message}");
}
