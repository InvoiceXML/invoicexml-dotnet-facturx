using System.Text;

namespace InvoiceXml.FacturX.Tests;

internal static class TestData
{
    public static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%test\n");

    public static readonly byte[] Xml = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><rsm:CrossIndustryInvoice />");

    public static (FacturXClient Client, MockHandler Handler) CreateClient(InvoiceXmlClientOptions? options = null)
    {
        var handler = new MockHandler();
        var client = new FacturXClient(new HttpClient(handler), options ?? new InvoiceXmlClientOptions { ApiKey = "test-key" });
        client.Transport.Delay = (_, _) => Task.CompletedTask;
        return (client, handler);
    }

    public static InvoiceDocument Invoice() => new()
    {
        InvoiceNumber = "F-2026-001",
        IssueDate = new DateTime(2026, 10, 9),
        TypeCode = InvoiceTypeCode.Invoice,
        Currency = CurrencyCode.EUR,
        Notes = { "Merci pour votre confiance.", new InvoiceNote("Pas d'escompte pour paiement anticipe.", "AAB") },
        Seller = new SellerParty
        {
            Name = "Atelier Dupont SARL",
            VatIdentifier = "FR32123456789",
            LegalRegistration = new SchemeIdentifier("123456789", "0002"),
            PostalAddress = new PostalAddress { Line1 = "12 rue de la Roquette", City = "Paris", PostCode = "75011", Country = CountryCode.FR },
        },
        Buyer = new BuyerParty
        {
            Name = "Client Exemple SA",
            PostalAddress = new PostalAddress { City = "Lyon", PostCode = "69002", Country = CountryCode.FR },
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
                PriceDetails = new PriceDetails { NetPrice = 120m },
                VatInformation = new LineVatInformation { CategoryCode = VatCategoryCode.S, Rate = 20 },
                Item = new ItemInformation { Name = "Prestation de conseil" },
            },
        },
    };
}
