using System.Globalization;
using System.Text.Json;
using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    // Role names and amounts are written out on purpose, not read from ApprovalLimits,
    // so a wrong constant in production code cannot hide behind the same constant in a test.
    private static readonly Approver Manager = new("demo.manager", "Manager");
    private static readonly Approver Director = new("demo.director", "Director");
    private static readonly Approver SecondDirector = new("demo.director2", "Director");

    private static Invoice InvoiceWith(string id, InvoiceStatus status, decimal amount = 100m) =>
        new() { Id = id, CustomerName = "Test School", Amount = amount, Status = status };

    private static Invoice PendingInvoice(decimal amount) => InvoiceWith("INV-1", InvoiceStatus.PendingApproval, amount);

    private static Invoice InvoiceWithFirstApproval(decimal amount, string firstApprover)
    {
        var invoice = PendingInvoice(amount);
        invoice.FirstApprovedBy = firstApprover;
        invoice.FirstApprovedAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        return invoice;
    }

    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    /// <summary>Every public property of the invoice, so a field added later is covered automatically.</summary>
    private static string Snapshot(Invoice invoice) => JsonSerializer.Serialize(invoice);

    /// <summary>AL-5: a refused approval gives a reason and leaves the invoice and the repository untouched.</summary>
    private static ApprovalDeniedException AssertDeniedAndUnchanged(Invoice invoice, Approver approver)
    {
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        var before = Snapshot(invoice);

        var denial = Assert.Throws<ApprovalDeniedException>(() => service.Approve(invoice.Id, approver));

        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
        return denial;
    }

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var repository = new FakeInvoiceRepository(invoice);

        var result = new ApprovalService(repository).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal(new[] { "INV-1" }, repository.UpdatedIds);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraftWithItsStatusAndChangesNothing()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Approved);
        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(invoice);

        var error = Assert.Throws<InvalidOperationException>(() => new ApprovalService(repository).Submit("INV-1"));

        Assert.Equal("Invoice INV-1 is Approved; only drafts can be submitted.", error.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var before = DateTime.UtcNow;

        var result = service.Approve("INV-1", Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.Equal(DateTimeKind.Utc, result.DecidedAt!.Value.Kind);
        Assert.InRange(result.DecidedAt.Value, before, DateTime.UtcNow);
    }

    // FR-009: "only pending invoices" still answers before any limit (409, not 403), with the status in the reason.
    // The amount is above the Manager limit on purpose, so swapping the two checks would change the answer.
    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Approve_InvoiceThatIsNotPending_IsRefusedWithItsStatusBeforeAnyLimitAndChangesNothing(InvoiceStatus status)
    {
        var invoice = InvoiceWith("INV-1", status, amount: 600_000m);
        invoice.DecidedBy = status == InvoiceStatus.Draft ? null : "demo.director";
        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(invoice);

        var error = Assert.Throws<InvalidOperationException>(() => new ApprovalService(repository).Approve("INV-1", Manager));

        Assert.IsNotType<ApprovalDeniedException>(error);
        Assert.Equal($"Invoice INV-1 is {status}; only pending invoices can be decided.", error.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        var error = Assert.Throws<KeyNotFoundException>(() => service.Approve("INV-404", Manager));

        Assert.Equal("Invoice INV-404 was not found.", error.Message);
    }

    [Fact]
    [Trait("Rule", "AL-1")]
    public void Approve_ManagerAtExactlyFiftyThousand_ApprovesAndSavesOnlyThatInvoice()
    {
        var invoice = PendingInvoice(50_000.00m);
        var otherInvoice = InvoiceWith("INV-2", InvoiceStatus.PendingApproval, 50_000.00m);
        var otherBefore = Snapshot(otherInvoice);
        var repository = new FakeInvoiceRepository(invoice, otherInvoice);

        var result = new ApprovalService(repository).Approve("INV-1", Manager);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.Null(result.DecisionNote);
        Assert.Equal(new[] { "INV-1" }, repository.UpdatedIds);
        Assert.Equal(otherBefore, Snapshot(otherInvoice));
    }

    [Fact]
    [Trait("Rule", "AL-1")]
    [Trait("Rule", "AL-5")]
    public void Approve_ManagerOneCentAboveFiftyThousand_IsDeniedWithReasonAndChangesNothing()
    {
        var denial = AssertDeniedAndUnchanged(PendingInvoice(50_000.01m), Manager);

        Assert.Equal(
            "Invoice INV-1 is 50,000.01, which is above the Manager limit of 50,000.00. A Director must approve it.",
            denial.Message);
    }

    [Fact]
    [Trait("Rule", "AL-1")]
    [Trait("Rule", "AL-2")]
    public void Approve_AmountBetweenTheLimits_IsDeniedForManagerAndApprovedByDirector()
    {
        var invoice = PendingInvoice(120_000m);
        var service = ServiceFor(invoice);

        Assert.Throws<ApprovalDeniedException>(() => service.Approve("INV-1", Manager));
        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        var result = service.Approve("INV-1", Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
    }

    [Fact]
    [Trait("Rule", "AL-1")]
    [Trait("Rule", "AL-5")]
    public void Approve_ManagerAtExactlyFiveHundredThousand_IsDeniedAndChangesNothing()
    {
        var denial = AssertDeniedAndUnchanged(PendingInvoice(500_000.00m), Manager);

        Assert.Equal(
            "Invoice INV-1 is 500,000.00, which is above the Manager limit of 50,000.00. A Director must approve it.",
            denial.Message);
    }

    // AGENTS.md: money in reasons uses the invariant culture, whatever the server's culture is.
    [Fact]
    [Trait("Rule", "AL-5")]
    public void Approve_DenialReason_FormatsMoneyTheSameUnderAnyServerCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var denial = AssertDeniedAndUnchanged(PendingInvoice(120_000.50m), Manager);

            Assert.Contains("is 120,000.50, which is above the Manager limit of 50,000.00", denial.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    [Trait("Rule", "AL-2")]
    public void Approve_DirectorAtExactlyFiveHundredThousand_ApprovesAlone()
    {
        var invoice = PendingInvoice(500_000.00m);
        var repository = new FakeInvoiceRepository(invoice);

        var result = new ApprovalService(repository).Approve("INV-1", Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Theory]
    [Trait("Rule", "AL-6")]
    [Trait("Rule", "AL-5")]
    [InlineData("Admin")]
    [InlineData("Approver")]
    [InlineData("manager")]
    [InlineData("Manager ")]
    [InlineData("")]
    [InlineData(null)]
    public void Approve_RoleWithoutApprovalLimit_IsDeniedWithReasonAndChangesNothing(string? role)
    {
        var denial = AssertDeniedAndUnchanged(PendingInvoice(100m), new Approver("someone", role!));

        Assert.Equal(
            $"someone has role '{role}', which cannot approve invoices. A Manager or Director must approve it.",
            denial.Message);
    }

    // ---- Above 500,000.00: two different people, the second a Director (AL-3, AL-4) ----

    [Fact]
    [Trait("Rule", "AL-3")]
    public void Approve_DirectorFirstAboveFiveHundredThousand_RecordsFirstApprovalAndStaysPending()
    {
        var invoice = PendingInvoice(750_000m);
        var repository = new FakeInvoiceRepository(invoice);
        var before = DateTime.UtcNow;

        var result = new ApprovalService(repository).Approve("INV-1", Director);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal("demo.director", result.FirstApprovedBy);
        Assert.Equal(DateTimeKind.Utc, result.FirstApprovedAt!.Value.Kind);
        Assert.InRange(result.FirstApprovedAt.Value, before, DateTime.UtcNow);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Null(result.DecisionNote);
        Assert.Equal(new[] { "INV-1" }, repository.UpdatedIds);
    }

    [Fact]
    [Trait("Rule", "AL-3")]
    public void Approve_DifferentDirectorSecond_ApprovesAndKeepsTheFirstApproval()
    {
        var invoice = PendingInvoice(750_000m);
        var repository = new FakeInvoiceRepository(invoice);
        var service = new ApprovalService(repository);
        service.Approve("INV-1", Director);

        var result = service.Approve("INV-1", SecondDirector);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director2", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal("demo.director", result.FirstApprovedBy);
        Assert.Equal(2, repository.UpdateCount);
    }

    // AL-3: the first approver may be a Manager, even though a Manager cannot give a final approval above 50,000.00.
    [Fact]
    [Trait("Rule", "AL-1")]
    [Trait("Rule", "AL-3")]
    public void Approve_ManagerFirstOneCentAboveFiveHundredThousandThenDirector_ApprovesTheInvoice()
    {
        var service = ServiceFor(PendingInvoice(500_000.01m));

        var afterFirst = service.Approve("INV-1", Manager);
        Assert.Equal(InvoiceStatus.PendingApproval, afterFirst.Status);
        Assert.Equal("demo.manager", afterFirst.FirstApprovedBy);

        var result = service.Approve("INV-1", Director);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
    }

    // AL-2 + AL-3: one cent above the Director limit, a Director alone only gives the first approval.
    [Fact]
    [Trait("Rule", "AL-2")]
    [Trait("Rule", "AL-3")]
    public void Approve_DirectorAloneOneCentAboveFiveHundredThousand_OnlyRecordsTheFirstApproval()
    {
        var result = ServiceFor(PendingInvoice(500_000.01m)).Approve("INV-1", Director);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Equal("demo.director", result.FirstApprovedBy);
        Assert.Null(result.DecidedBy);
    }

    [Fact]
    [Trait("Rule", "AL-4")]
    [Trait("Rule", "AL-5")]
    public void Approve_SameDirectorTwice_IsDeniedWithReasonAndChangesNothing()
    {
        var denial = AssertDeniedAndUnchanged(InvoiceWithFirstApproval(750_000m, "demo.director"), Director);

        Assert.Equal(
            "demo.director already gave the first approval on invoice INV-1; a different person must give the second approval.",
            denial.Message);
    }

    // AL-4: letter case and surrounding spaces do not make a different person, on either side.
    [Theory]
    [Trait("Rule", "AL-4")]
    [Trait("Rule", "AL-5")]
    [InlineData("demo.director", "Demo.Director")]
    [InlineData("demo.director", "DEMO.DIRECTOR")]
    [InlineData("demo.director", " demo.director ")]
    [InlineData(" Demo.Director ", "demo.director")]
    public void Approve_SamePersonWithDifferentCaseOrSpaces_IsDeniedAndChangesNothing(string firstApprover, string secondApprover)
    {
        var invoice = InvoiceWithFirstApproval(750_000m, firstApprover);

        var denial = AssertDeniedAndUnchanged(invoice, new Approver(secondApprover, "Director"));

        Assert.EndsWith("a different person must give the second approval.", denial.Message);
    }

    [Fact]
    [Trait("Rule", "AL-3")]
    [Trait("Rule", "AL-5")]
    public void Approve_ManagerAsSecondApprover_IsDeniedWithReasonAndChangesNothing()
    {
        var denial = AssertDeniedAndUnchanged(InvoiceWithFirstApproval(750_000m, "demo.director"), Manager);

        Assert.Equal(
            "Invoice INV-1 is above 500,000.00, so the second approval must come from a Director; demo.manager is a Manager.",
            denial.Message);
    }

    // FR-011: when the same Manager tries again, "same person" is the reason, not "must be a Director".
    [Fact]
    [Trait("Rule", "AL-4")]
    [Trait("Rule", "AL-5")]
    public void Approve_SameManagerTwice_IsDeniedAsTheSamePersonFirst()
    {
        var denial = AssertDeniedAndUnchanged(InvoiceWithFirstApproval(750_000m, "demo.manager"), Manager);

        Assert.Equal(
            "demo.manager already gave the first approval on invoice INV-1; a different person must give the second approval.",
            denial.Message);
    }

    // AL-6: a role without a limit can give neither the first nor the second approval.
    [Theory]
    [Trait("Rule", "AL-6")]
    [Trait("Rule", "AL-5")]
    [InlineData(null, "Admin")]
    [InlineData(null, "Auditor")]
    [InlineData("demo.director", "Admin")]
    [InlineData("demo.director", "Auditor")]
    public void Approve_RoleWithoutLimitAboveFiveHundredThousand_IsDeniedAndChangesNothing(string? firstApprover, string role)
    {
        var invoice = firstApprover is null ? PendingInvoice(750_000m) : InvoiceWithFirstApproval(750_000m, firstApprover);

        var denial = AssertDeniedAndUnchanged(invoice, new Approver("demo.admin", role));

        Assert.Equal(
            $"demo.admin has role '{role}', which cannot approve invoices. A Manager or Director must approve it.",
            denial.Message);
    }

    // FR-009: rejecting after a first approval works and keeps the first-approval record as history.
    [Fact]
    public void Reject_AfterAFirstApproval_RejectsAndKeepsTheFirstApproval()
    {
        var result = ServiceFor(InvoiceWithFirstApproval(750_000m, "demo.director"))
            .Reject("INV-1", "demo.director2", "Duplicate of INV-0999");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("demo.director", result.FirstApprovedBy);
        Assert.NotNull(result.FirstApprovedAt);
    }

    [Fact]
    public void Approve_WithoutApprover_ThrowsArgumentNullException()
    {
        var invoice = PendingInvoice(100m);
        var repository = new FakeInvoiceRepository(invoice);

        Assert.Throws<ArgumentNullException>(() => new ApprovalService(repository).Approve("INV-1", null!));

        Assert.Equal(InvoiceStatus.PendingApproval, invoice.Status);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Approve_WithoutApproverName_ThrowsArgumentExceptionAndChangesNothing(string username)
    {
        var invoice = PendingInvoice(100m);
        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(invoice);

        var error = Assert.Throws<ArgumentException>(() => new ApprovalService(repository).Approve("INV-1", new Approver(username, "Manager")));

        Assert.StartsWith("An approver name is required to approve an invoice.", error.Message);

        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var repository = new FakeInvoiceRepository(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));
        var before = DateTime.UtcNow;

        var result = new ApprovalService(repository).Reject("INV-1", "demo.manager", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.InRange(result.DecidedAt!.Value, before, DateTime.UtcNow);
        Assert.Equal(new[] { "INV-1" }, repository.UpdatedIds);
    }

    // Survivor in the baseline and after the feature: Reject never checked that the invoice is still pending.
    [Theory]
    [InlineData(InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Approved)]
    [InlineData(InvoiceStatus.Rejected)]
    public void Reject_InvoiceThatIsNotPending_IsRefusedWithItsStatusAndChangesNothing(InvoiceStatus status)
    {
        var invoice = InvoiceWith("INV-1", status);
        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(invoice);

        var error = Assert.Throws<InvalidOperationException>(
            () => new ApprovalService(repository).Reject("INV-1", "demo.manager", "Duplicate invoice"));

        Assert.Equal($"Invoice INV-1 is {status}; only pending invoices can be decided.", error.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }

    // FR-009: approval limits do not apply to rejecting.
    [Fact]
    public void Reject_ManagerCanRejectAnInvoiceAboveTheirApprovalLimit()
    {
        var result = ServiceFor(PendingInvoice(750_000m)).Reject("INV-1", "demo.manager", "Wrong customer");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.PendingApproval);
        var repository = new FakeInvoiceRepository(invoice);
        var before = Snapshot(invoice);

        var error = Assert.Throws<ArgumentException>(() => new ApprovalService(repository).Reject("INV-1", "demo.manager", "  "));

        Assert.StartsWith("A reason is required to reject an invoice.", error.Message);
        Assert.Equal(before, Snapshot(invoice));
        Assert.Equal(0, repository.UpdateCount);
    }
}
