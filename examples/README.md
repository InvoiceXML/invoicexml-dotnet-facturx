# Examples

Small console apps, one per common task. Each one is a complete `Program.cs` you can read from top to bottom and adapt.

| Example | What it shows |
|---|---|
| [CreateFacturXWithOwnPdf](CreateFacturXWithOwnPdf/Program.cs) | Create a Factur-X invoice that keeps your own PDF layout, passed as a public URL (`PdfUrl`). |
| [CreateCiiAndEmbed](CreateCiiAndEmbed/Program.cs) | Build a CII XML from your invoice data with `CreateCiiAsync`, then embed it into a PDF you already have with `EmbedFacturXAsync`. |
| [ValidateInvoices](ValidateInvoices/Program.cs) | Validate a Factur-X PDF, optionally against the French BR-FR rules, print every finding, and save the printable validation report. |
| [RenderXmlToPdf](RenderXmlToPdf/Program.cs) | Turn any invoice XML (CII, UBL or XRechnung) into a readable PDF. |
| [ConvertUblToFacturX](ConvertUblToFacturX/Program.cs) | Convert a UBL invoice XML into a Factur-X invoice. |
| [TransformToFacturX](TransformToFacturX/Program.cs) | Turn an ordinary PDF invoice into a Factur-X invoice. AI reads the PDF, so a person checks the result. |
| [ParsePdfInvoice](ParsePdfInvoice/Program.cs) | Read an ordinary PDF invoice, typed, scanned or photographed, into the invoice model with AI, and see how sure the read was per area. |
| [ExtractJsonAndAttachments](ExtractJsonAndAttachments/Program.cs) | Read a received Factur-X invoice as a typed `InvoiceDocument` and save its embedded attachments as a ZIP. |

## Run an example

1. Open the example's `Program.cs` and replace the values at the top: `YOUR_API_KEY` with your API key, and the file paths or URLs with your own. An example whose file is missing tells you which value to change.
2. Run it from the repository root, for example:

```bash
dotnet run --project examples/CreateCiiAndEmbed
```

The examples reference the library source in this repository. To use one in your own project, copy its `Program.cs` and install the package instead:

```bash
dotnet add package InvoiceXml.FacturX
```

Get an API key with the **30-day free trial: 100 credits, no credit card required** at [www.invoicexml.com/account/signup](https://www.invoicexml.com/account/signup). The calls use credits from your account, so run the examples with test data.
