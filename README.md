# Factur-X for .NET

[![NuGet](https://img.shields.io/nuget/v/InvoiceXml.FacturX.svg)](https://www.nuget.org/packages/InvoiceXml.FacturX)

Create, validate, embed and read Factur-X e-invoices from C#. `InvoiceXml.FacturX` is a thin, typed client for the [InvoiceXML API](https://invoicexml.com): you pass an EN 16931 invoice object and get back a Factur-X PDF/A-3 that has already passed the official rules.

- **Factur-X**: hybrid PDF/A-3 with the invoice XML embedded, EN 16931 profile by default, every official profile selectable, including EXTENDED-CTC-FR.
- **French e-invoicing reform**: the French BR-FR rules on request, the same set the certified platforms apply. Factur-X is a core format of the reform in force since September 2026 and is accepted by Chorus Pro.
- **Validation** of Factur-X PDFs from any producer, with plain-language findings and a field report.
- **Reading** of incoming invoices into the same object model, plus extraction of embedded attachments.

## Install

```bash
dotnet add package InvoiceXml.FacturX
```

Targets .NET 8 and .NET Standard 2.0 (.NET Framework 4.6.2+, .NET Core, Mono). The only dependency is `System.Text.Json` on .NET Standard.

## Quick start

```csharp
using InvoiceXml.FacturX;

using var client = new FacturXClient("YOUR_API_KEY");
var pdf = await client.CreateFacturXAsync(new InvoiceDocument
{
    InvoiceNumber = "F-2026-001",
    Currency = CurrencyCode.EUR,
    Seller = new SellerParty { Name = "Atelier Dupont SARL", VatIdentifier = "FR32123456789", PostalAddress = new PostalAddress { City = "Paris", PostCode = "75011", Country = CountryCode.FR } },
    Buyer = new BuyerParty { Name = "Client Exemple SA", PostalAddress = new PostalAddress { City = "Lyon", PostCode = "69002", Country = CountryCode.FR } },
    Lines = { new InvoiceLine { Quantity = 10, UnitCode = UnitCodes.Hour, Item = new ItemInformation { Name = "Prestation de conseil" }, PriceDetails = new PriceDetails { NetPrice = 120m }, VatInformation = new LineVatInformation { CategoryCode = VatCategoryCode.S, Rate = 20 } } },
});
await pdf.SaveAsync("facture.pdf");
```

Totals, VAT breakdowns, the issue date and default payment terms are filled in by the API when you leave them empty. Get an API key with the **30-day free trial: 100 credits, no credit card required** at [invoicexml.com/signup](https://invoicexml.com/signup).

## Why InvoiceXML

- **Rule sets updated on their legal effective dates.** New EN 16931, Factur-X and French reform rule releases switch server-side on the date they become binding. Your integration and this package stay the same: no package update, no redeploy.
- **Validated before delivery.** Every invoice is checked against the official rules before it is returned. Data that cannot yield a compliant invoice is refused with the violated rules and the fields to correct.
- **Zero data retention.** Documents are processed in memory and never stored. See [security](https://invoicexml.com/trust).
- **Processing in Frankfurt.** EU-only processing in Frankfurt am Main, Germany, GDPR compliant by design.
- **Integration assistance.** Our engineers help you map your data and get to production. [Contact us](https://invoicexml.com/contact).

## Usage

### An invoice for the French reform

Add `FacturXRuleSets.BrFr` and the invoice must also pass the French BR-FR rules. A domestic invoice then carries the SIREN of both parties, the billing mode in `BusinessProcessType`, and the mandatory payment mentions as coded notes.

```csharp
var invoice = new InvoiceDocument
{
    InvoiceNumber = "F-2026-0815",
    Currency = CurrencyCode.EUR,
    DueDate = new DateTime(2026, 11, 9),
    BusinessProcessType = "S1", // billing mode: services, simple invoice
    Notes =
    {
        new InvoiceNote("En cas de retard de paiement, une indemnite forfaitaire de 40 euros pour frais de recouvrement sera exigee.", "PMT"),
        new InvoiceNote("Penalites de retard : trois fois le taux d'interet legal.", "PMD"),
        new InvoiceNote("Pas d'escompte pour paiement anticipe.", "AAB"),
    },
    Seller = new SellerParty
    {
        Name = "Atelier Dupont SARL",
        VatIdentifier = "FR32123456789",
        LegalRegistration = new SchemeIdentifier("123456789", "0002"), // SIREN
        PostalAddress = new PostalAddress { Line1 = "12 rue de la Roquette", City = "Paris", PostCode = "75011", Country = CountryCode.FR },
        ElectronicAddress = new SchemeIdentifier("factures@atelier-dupont.fr", "EM"),
    },
    Buyer = new BuyerParty
    {
        Name = "Client Exemple SA",
        VatIdentifier = "FR40303265045",
        LegalRegistration = new SchemeIdentifier("303265045", "0002"),
        PostalAddress = new PostalAddress { Line1 = "5 place Bellecour", City = "Lyon", PostCode = "69002", Country = CountryCode.FR },
        ElectronicAddress = new SchemeIdentifier("comptabilite@client-exemple.fr", "EM"),
    },
    PaymentDetails = new PaymentDetails
    {
        PaymentMeansCode = PaymentMeansCodes.SepaCreditTransfer,
        PaymentAccountIdentifier = "FR7630006000011234567890189",
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

var pdf = await client.CreateFacturXAsync(invoice, new FacturXOptions
{
    Language = PdfLanguage.Fr,
    Rules = { FacturXRuleSets.BrFr },
});
await pdf.SaveAsync(pdf.FileName ?? "facture.pdf");
```

Set `InvoiceDocument.SpecificationId` to a value from `FacturXProfiles` to create a profile other than EN 16931, for example `FacturXProfiles.ExtendedCtcFr` or `FacturXProfiles.Basic`.

### Handle a refused invoice

When the data cannot produce a compliant invoice, `CreateFacturXAsync` throws `InvoiceXmlApiException` with every violated rule and the JSON path of the field to correct.

```csharp
try
{
    var pdf = await client.CreateFacturXAsync(invoice);
}
catch (InvoiceXmlApiException ex) when (ex.IsValidationError)
{
    foreach (var finding in ex.Findings)
        Console.WriteLine($"[{finding.Rule}] {finding.Message} ({string.Join(", ", finding.Fields)})");
}
```

### Branded PDFs, or your own layout

```csharp
var pdf = await client.CreateFacturXAsync(invoice, new FacturXOptions
{
    Language = PdfLanguage.Fr,
    BrandColor = "#0F766E",
    LogoUrl = "https://example.com/logo.png",
});

// Keep your existing PDF layout: the API embeds the generated XML into it.
var hybrid = await client.CreateFacturXAsync(invoice, new FacturXOptions { PdfUrl = "https://example.com/facture-1234.pdf" });

// Or combine a PDF and a CII XML you already have.
var embedded = await client.EmbedFacturXAsync(File.ReadAllBytes("facture.pdf"), File.ReadAllBytes("factur-x.xml"));
```

### Validate incoming invoices

A completed validation always returns a result. An invalid invoice is not an exception: check `Valid`.

```csharp
using var file = File.OpenRead("facture-fournisseur.pdf");
var result = await client.ValidateFacturXAsync(file, "facture-fournisseur.pdf", rules: new[] { FacturXRuleSets.BrFr });

if (result.Valid)
{
    Console.WriteLine($"Valid Factur-X, profile {result.Data?.Profile}");
}
else
{
    foreach (var error in result.Errors)
        Console.WriteLine($"[{error.Rule}] ({error.Layer}) {error.Message}");
}
```

Leave out `rules` to check against the declared profile only. `result.Report` lists every invoice field with its value and the findings that concern it, ready for a review screen.

### Read invoices and attachments

```csharp
// Factur-X PDF or CII XML in, the same InvoiceDocument model out.
var received = await client.ExtractJsonAsync(File.ReadAllBytes("facture-fournisseur.pdf"));
Console.WriteLine($"{received.InvoiceNumber} from {received.Seller?.Name}: {received.Totals?.DuePayableAmount} {received.Currency}");

// Embedded supporting documents (timesheets, delivery notes) as one ZIP.
var zip = await client.ExtractAttachmentsAsync(File.ReadAllBytes("facture-fournisseur.pdf"));
await zip.SaveAsync("pieces-jointes.zip");
```

An extracted `InvoiceDocument` can be passed straight back to `CreateFacturXAsync`.

### ASP.NET Core and IHttpClientFactory

```csharp
builder.Services.AddHttpClient<IFacturXClient, FacturXClient>((http, services) =>
    new FacturXClient(http, builder.Configuration["InvoiceXml:ApiKey"]!));
```

Inject `IFacturXClient` where you need it, and fake it in your own unit tests. The client never disposes an `HttpClient` you pass in.

### Rate limits and retries

When more requests of your account run at the same time than your plan allows, the API answers HTTP 429 with a `Retry-After` header. Such a request is not processed and not billed. The client waits the requested interval and retries, up to three times by default:

```csharp
var client = new FacturXClient(new InvoiceXmlClientOptions
{
    ApiKey = "YOUR_API_KEY",
    MaxRetries = 5,
    MaxRetryDelay = TimeSpan.FromSeconds(60),
});
```

If the API still answers 429, the client throws `InvoiceXmlRateLimitException` with the `RetryAfter` interval. Every method takes a `CancellationToken`, which also cancels a pending retry wait.

### Errors

Any other refused request throws `InvoiceXmlApiException` with the HTTP `StatusCode`, the API's numeric `ErrorCode` (see `InvoiceXmlErrorCode`), `Title`, `Detail` and a `TraceId` to quote when contacting support. The full list is in the [error handling docs](https://invoicexml.com/docs/error-handling).

## Endpoints

| Method | Endpoint | Docs |
|---|---|---|
| `CreateFacturXAsync` | `POST /v1/create/facturx` | [reference](https://invoicexml.com/docs/api/create/facturx) |
| `ValidateFacturXAsync` | `POST /v1/validate/facturx` | [reference](https://invoicexml.com/docs/api/validate/facturx) |
| `EmbedFacturXAsync` | `POST /v1/embed/facturx` | [reference](https://invoicexml.com/docs/api/embed/facturx) |
| `ExtractJsonAsync` | `POST /v1/extract/json` | [reference](https://invoicexml.com/docs/api/extract/json) |
| `ExtractAttachmentsAsync` | `POST /v1/extract/attachments` | [reference](https://invoicexml.com/docs/api/extract/attachments) |

The complete OpenAPI description is at [api.invoicexml.com/v1/openapi](https://api.invoicexml.com/v1/openapi), with an interactive explorer at [api.invoicexml.com/v1/scalar](https://api.invoicexml.com/v1/scalar/).

Invoicing German customers too? [InvoiceXml.Zugferd.XRechnung](https://www.nuget.org/packages/InvoiceXml.Zugferd.XRechnung) covers ZUGFeRD and XRechnung with the same object model.

## Support

Questions, or a field you need for your integration? [Contact us](https://invoicexml.com/contact) and we will help you get it working.

## License

MIT. See [LICENSE](https://github.com/InvoiceXML/invoicexml-dotnet-facturx/blob/main/LICENSE).
