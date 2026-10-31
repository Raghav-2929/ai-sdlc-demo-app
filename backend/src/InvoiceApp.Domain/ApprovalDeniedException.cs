namespace InvoiceApp.Domain;

/// <summary>
/// An approval rule refused the attempt. The message is the reason shown to the user.
/// Thrown before anything on the invoice changes. Derives from InvalidOperationException so an
/// endpoint that does not map it still answers 409 with the reason rather than 500.
/// </summary>
public class ApprovalDeniedException : InvalidOperationException
{
    public ApprovalDeniedException(string reason) : base(reason)
    {
    }
}
