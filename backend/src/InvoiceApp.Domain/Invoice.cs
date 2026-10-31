namespace InvoiceApp.Domain;

public class Invoice
{
    public string Id { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }

    /// <summary>Above the Director limit: who gave the first of the two approvals, and when. Kept after the decision.</summary>
    public string? FirstApprovedBy { get; set; }
    public DateTime? FirstApprovedAt { get; set; }
}
