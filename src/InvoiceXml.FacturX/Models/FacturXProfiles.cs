namespace InvoiceXml.FacturX;

/// <summary>
/// Official Factur-X profile identifiers for <see cref="InvoiceDocument.SpecificationId"/>.
/// Leave the specification identifier empty to get the recommended EN 16931 profile.
/// </summary>
public static class FacturXProfiles
{
    /// <summary>MINIMUM: header-only booking aid. The PDF is the legal invoice.</summary>
    public const string Minimum = "urn:factur-x.eu:1p0:minimum";

    /// <summary>BASIC WL: header-only booking aid without lines. The PDF is the legal invoice.</summary>
    public const string BasicWl = "urn:factur-x.eu:1p0:basicwl";

    /// <summary>BASIC: a subset of EN 16931 with line items.</summary>
    public const string Basic = "urn:cen.eu:en16931:2017#compliant#urn:factur-x.eu:1p0:basic";

    /// <summary>EN 16931 (COMFORT): the full European standard. The default and recommended profile.</summary>
    public const string En16931 = "urn:cen.eu:en16931:2017";

    /// <summary>EXTENDED: EN 16931 plus additional structured content.</summary>
    public const string Extended = "urn:cen.eu:en16931:2017#conformant#urn:factur-x.eu:1p0:extended";

    /// <summary>EXTENDED-CTC-FR: the reference profile for the French B2B e-invoicing reform.</summary>
    public const string ExtendedCtcFr = "urn:cen.eu:en16931:2017#conformant#urn.cpro.gouv.fr:1p0:extended-ctc-fr";
}

/// <summary>
/// Additional national rule sets for <see cref="FacturXOptions.Rules"/> and the validate and
/// embed methods. They run on top of the profile's own rules without changing the declared profile.
/// </summary>
public static class FacturXRuleSets
{
    /// <summary>
    /// The French BR-FR rules, the set the French platforms apply to every domestic invoice under
    /// the B2B e-invoicing reform. Requires at least the BASIC profile.
    /// </summary>
    public const string BrFr = "br-fr";
}
