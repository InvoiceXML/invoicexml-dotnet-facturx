using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace InvoiceXml.FacturX.Tests.Live;

/// <summary>
/// Access to the real API for the live tests. The key comes from user secrets
/// (<c>InvoiceXml:ApiKey</c>) or the <c>INVOICEXML_API_KEY</c> environment variable; without
/// one, every live test is skipped. Live calls consume credits on the key's account.
/// </summary>
internal static class LiveApi
{
    public static readonly string? ApiKey = ResolveApiKey();

    /// <summary>Files produced by the live run, for manual inspection (git-ignored).</summary>
    public static readonly string OutputDirectory = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "live");

    private static string? ResolveApiKey()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("INVOICEXML_API_KEY");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(LiveApi).Assembly, optional: true)
            .Build();
        var fromSecrets = configuration["InvoiceXml:ApiKey"];
        return string.IsNullOrWhiteSpace(fromSecrets) ? null : fromSecrets;
    }

    public static async Task SaveAsync(InvoiceFile file, string name)
    {
        Directory.CreateDirectory(OutputDirectory);
        await file.SaveAsync(Path.Combine(OutputDirectory, name));
    }

    /// <summary>
    /// Calls an endpoint outside this package's surface to prepare test input (for example a plain
    /// PDF and a CII XML for the embed test).
    /// </summary>
    public static async Task<byte[]> PostFileAsync(string path, byte[] file, string fileName, string mediaType)
    {
        using var http = new HttpClient();
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        form.Add(content, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(InvoiceXmlClientOptions.DefaultBaseAddress, path)) { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        using var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{path} returned {(int)response.StatusCode}: {System.Text.Encoding.UTF8.GetString(body)}");
        return body;
    }
}

/// <summary>A fact that only runs when an API key is configured.</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (LiveApi.ApiKey is null)
            Skip = "No InvoiceXML API key configured (user secret InvoiceXml:ApiKey or INVOICEXML_API_KEY).";
    }
}

/// <summary>Invoices taken from the README, so the documented examples are checked against the real API.</summary>
internal static class LiveInvoices
{
    public static InvoiceDocument QuickStart() => new()
    {
        InvoiceNumber = "F-2026-001",
        Currency = CurrencyCode.EUR,
        Seller = new SellerParty { Name = "Atelier Dupont SARL", VatIdentifier = "FR32123456789", PostalAddress = new PostalAddress { City = "Paris", PostCode = "75011", Country = CountryCode.FR } },
        Buyer = new BuyerParty { Name = "Client Exemple SA", PostalAddress = new PostalAddress { City = "Lyon", PostCode = "69002", Country = CountryCode.FR } },
        Lines = { new InvoiceLine { Quantity = 10, UnitCode = UnitCodes.Hour, Item = new ItemInformation { Name = "Prestation de conseil" }, PriceDetails = new PriceDetails { NetPrice = 120m }, VatInformation = new LineVatInformation { CategoryCode = VatCategoryCode.S, Rate = 20 } } },
    };

    public static InvoiceDocument FrenchReform() => new()
    {
        InvoiceNumber = "F-2026-0815",
        Currency = CurrencyCode.EUR,
        DueDate = new DateTime(2026, 11, 9),
        BusinessProcessType = "S1", // French billing mode: services, simple invoice
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
            LegalRegistration = new SchemeIdentifier("123456789", "0002"),
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
}
