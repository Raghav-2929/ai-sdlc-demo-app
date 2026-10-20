using System.Globalization;

namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// Approvals are limited by the approver's role (see ApprovalLimits). Every rule is checked
/// before anything on the invoice is assigned, so a refused approval changes nothing.
/// </summary>
public class ApprovalService
{
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Approve(string invoiceId, Approver approver)
    {
        ArgumentNullException.ThrowIfNull(approver);
        if (string.IsNullOrWhiteSpace(approver.Username))
        {
            throw new ArgumentException("An approver name is required to approve an invoice.", nameof(approver));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);
        EnsureWithinLimit(invoice, approver);

        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver.Username;
        invoice.DecidedAt = DateTime.UtcNow;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }

    private static void EnsureWithinLimit(Invoice invoice, Approver approver)
    {
        var limit = ApprovalLimits.LimitFor(approver.Role)
            ?? throw new ApprovalDeniedException(
                $"{approver.Username} has role '{approver.Role}', which cannot approve invoices. A Manager or Director must approve it.");

        if (invoice.Amount > limit)
        {
            var whoCanApprove = invoice.Amount <= ApprovalLimits.DirectorLimit
                ? "A Director must approve it."
                : $"Invoices above {Money(ApprovalLimits.DirectorLimit)} need two approvers.";
            throw new ApprovalDeniedException(
                $"Invoice {invoice.Id} is {Money(invoice.Amount)}, which is above the {approver.Role} limit of {Money(limit)}. {whoCanApprove}");
        }
    }

    private static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
}
