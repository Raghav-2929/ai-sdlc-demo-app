using System.Globalization;

namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// Approvals are limited by the approver's role (see ApprovalLimits); above the Director limit an
/// invoice needs two different approvers and the second must be a Director. Every rule is checked
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
        var limit = LimitOf(approver); // AL-6 first: a role without a limit cannot approve on either path.

        if (invoice.Amount <= ApprovalLimits.DirectorLimit)
        {
            EnsureWithinLimit(invoice, approver, limit);
            return MarkApproved(invoice, approver);
        }

        if (invoice.FirstApprovedBy is null)
        {
            return RecordFirstApproval(invoice, approver);
        }

        EnsureValidSecondApprover(invoice, approver);
        return MarkApproved(invoice, approver);
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

    private static decimal LimitOf(Approver approver) =>
        ApprovalLimits.LimitFor(approver.Role)
        ?? throw new ApprovalDeniedException(
            $"{approver.Username} has role '{approver.Role}', which cannot approve invoices. A Manager or Director must approve it.");

    private static void EnsureWithinLimit(Invoice invoice, Approver approver, decimal limit)
    {
        if (invoice.Amount > limit)
        {
            throw new ApprovalDeniedException(
                $"Invoice {invoice.Id} is {Money(invoice.Amount)}, which is above the {approver.Role} limit of {Money(limit)}. A Director must approve it.");
        }
    }

    private static void EnsureValidSecondApprover(Invoice invoice, Approver approver)
    {
        if (IsSamePerson(invoice.FirstApprovedBy!, approver.Username))
        {
            throw new ApprovalDeniedException(
                $"{approver.Username} already gave the first approval on invoice {invoice.Id}; a different Director must give the second approval.");
        }

        if (approver.Role != ApprovalLimits.DirectorRole)
        {
            throw new ApprovalDeniedException(
                $"Invoice {invoice.Id} is above {Money(ApprovalLimits.DirectorLimit)}, so the second approval must come from a Director; {approver.Username} is a {approver.Role}.");
        }
    }

    private Invoice RecordFirstApproval(Invoice invoice, Approver approver)
    {
        invoice.FirstApprovedBy = approver.Username;
        invoice.FirstApprovedAt = DateTime.UtcNow;
        _repository.Update(invoice);
        return invoice;
    }

    private Invoice MarkApproved(Invoice invoice, Approver approver)
    {
        invoice.Status = InvoiceStatus.Approved;
        invoice.DecidedBy = approver.Username;
        invoice.DecidedAt = DateTime.UtcNow;
        _repository.Update(invoice);
        return invoice;
    }

    private static bool IsSamePerson(string firstUsername, string secondUsername) =>
        string.Equals(firstUsername.Trim(), secondUsername.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
}
