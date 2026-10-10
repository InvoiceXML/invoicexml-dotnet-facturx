using System.Collections.Generic;

namespace InvoiceXml.FacturX;

/// <summary>The party issuing the invoice.</summary>
public sealed class SellerParty
{
    /// <summary>Legal name. Required.</summary>
    public string? Name { get; set; }

    /// <summary>Trading name, when different from the legal name.</summary>
    public string? TradingName { get; set; }

    /// <summary>Postal address. Required, with at least the country.</summary>
    public PostalAddress? PostalAddress { get; set; }

    /// <summary>Contact person or department.</summary>
    public Contact? Contact { get; set; }

    /// <summary>Seller identifiers, for example a GLN, each with its scheme.</summary>
    public IList<SchemeIdentifier> Identifiers { get; set; } = new List<SchemeIdentifier>();

    /// <summary>Legal registration, for example a SIREN with scheme <c>0002</c>.</summary>
    public SchemeIdentifier? LegalRegistration { get; set; }

    /// <summary>VAT identification number including the country prefix, for example <c>FR32123456789</c>.</summary>
    public string? VatIdentifier { get; set; }

    /// <summary>Local tax registration number, when the seller has no VAT identifier or shows both.</summary>
    public string? TaxRegistrationIdentifier { get; set; }

    /// <summary>Additional legal information, for example legal form and share capital.</summary>
    public string? AdditionalLegalInformation { get; set; }

    /// <summary>Electronic address for receiving documents.</summary>
    public SchemeIdentifier? ElectronicAddress { get; set; }
}

/// <summary>The party receiving the invoice.</summary>
public sealed class BuyerParty
{
    /// <summary>Legal name. Required.</summary>
    public string? Name { get; set; }

    /// <summary>Trading name, when different from the legal name.</summary>
    public string? TradingName { get; set; }

    /// <summary>Postal address. Required, with at least the country.</summary>
    public PostalAddress? PostalAddress { get; set; }

    /// <summary>Contact person or department.</summary>
    public Contact? Contact { get; set; }

    /// <summary>Buyer identifiers, each with its scheme.</summary>
    public IList<SchemeIdentifier> Identifiers { get; set; } = new List<SchemeIdentifier>();

    /// <summary>Legal registration, for example a SIREN with scheme <c>0002</c>.</summary>
    public SchemeIdentifier? LegalRegistration { get; set; }

    /// <summary>VAT identification number including the country prefix.</summary>
    public string? VatIdentifier { get; set; }

    /// <summary>Electronic address for receiving documents.</summary>
    public SchemeIdentifier? ElectronicAddress { get; set; }
}

/// <summary>A postal address.</summary>
public sealed class PostalAddress
{
    /// <summary>Main address line, usually street and number.</summary>
    public string? Line1 { get; set; }

    /// <summary>Additional address line.</summary>
    public string? Line2 { get; set; }

    /// <summary>Additional address line.</summary>
    public string? Line3 { get; set; }

    /// <summary>City.</summary>
    public string? City { get; set; }

    /// <summary>Post code.</summary>
    public string? PostCode { get; set; }

    /// <summary>Region or state.</summary>
    public string? CountrySubdivision { get; set; }

    /// <summary>Country. Required on seller and buyer addresses.</summary>
    public CountryCode? Country { get; set; }
}

/// <summary>A contact person or department.</summary>
public sealed class Contact
{
    /// <summary>Name of the person or department.</summary>
    public string? Name { get; set; }

    /// <summary>Phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Email address.</summary>
    public string? Email { get; set; }
}

/// <summary>
/// An identifier qualified by its scheme. Used for party identifiers, legal registrations,
/// electronic addresses, and item and object identifiers.
/// </summary>
public sealed class SchemeIdentifier
{
    /// <summary>Creates an empty identifier.</summary>
    public SchemeIdentifier()
    {
    }

    /// <summary>Creates an identifier with an optional scheme.</summary>
    /// <param name="identifier">The identifier value.</param>
    /// <param name="schemeId">The scheme, for example <c>0002</c> for a SIREN or <c>0088</c> for a GLN.</param>
    public SchemeIdentifier(string identifier, string? schemeId = null)
    {
        Identifier = identifier;
        SchemeId = schemeId;
    }

    /// <summary>The identifier value.</summary>
    public string? Identifier { get; set; }

    /// <summary>
    /// The identifier scheme: an ISO 6523 ICD code for party identifiers (for example <c>0002</c>
    /// for a SIREN, <c>0009</c> for a SIRET, <c>0088</c> for a GLN) or an EAS code for electronic
    /// addresses (for example <c>EM</c> for email).
    /// </summary>
    public string? SchemeId { get; set; }
}
