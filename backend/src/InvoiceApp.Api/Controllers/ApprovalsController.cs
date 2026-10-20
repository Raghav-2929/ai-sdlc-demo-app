using InvoiceApp.Api.Auth;
using InvoiceApp.Domain;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceApp.Api.Controllers;

/// <summary>
/// Approve has no role filter on purpose: ApprovalService decides who may approve (AL-6), so a user
/// whose role cannot approve gets a 403 with the reason instead of a bare 403.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ApprovalsController : ApiControllerBase
{
    private readonly ApprovalService _approvals;

    public ApprovalsController(ApprovalService approvals)
    {
        _approvals = approvals;
    }

    public record RejectRequest(string Reason);

    [HttpPost("{invoiceId}/approve")]
    public ActionResult<Invoice> Approve(string invoiceId)
    {
        var session = HttpContext.GetSession();
        return Handle(() => _approvals.Approve(invoiceId, new Approver(session.Username, session.Role)));
    }

    [HttpPost("{invoiceId}/reject")]
    [RequireRole("Manager", "Director", "Admin")]
    public ActionResult<Invoice> Reject(string invoiceId, [FromBody] RejectRequest request) =>
        Handle(() => _approvals.Reject(invoiceId, HttpContext.GetSession().Username, request.Reason));
}
