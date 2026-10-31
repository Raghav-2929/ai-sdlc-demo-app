namespace InvoiceApp.Domain;

/// <summary>
/// The largest invoice amount each role may approve on its own (inclusive).
/// This is the only place the limits are defined. A role that is not listed cannot approve.
/// </summary>
public static class ApprovalLimits
{
    public const string ManagerRole = "Manager";
    public const string DirectorRole = "Director";

    public const decimal ManagerLimit = 50_000m;
    public const decimal DirectorLimit = 500_000m;

    /// <summary>The role's approval limit, or null when the role has no approval authority.</summary>
    public static decimal? LimitFor(string? role) => role switch
    {
        ManagerRole => ManagerLimit,
        DirectorRole => DirectorLimit,
        _ => null
    };
}
