using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class InvoiceTests
{
    // InMemoryInvoiceRepository.Add only assigns an id when Id is empty, and the approval rules read the
    // first-approval fields as "no approval yet" when they are null. A new invoice must start that way.
    [Fact]
    public void NewInvoice_StartsAsAnUnnumberedDraftWithNoDecisionOrApproval()
    {
        var invoice = new Invoice();

        Assert.Equal(string.Empty, invoice.Id);
        Assert.Equal(string.Empty, invoice.CustomerName);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Null(invoice.DecidedBy);
        Assert.Null(invoice.DecidedAt);
        Assert.Null(invoice.DecisionNote);
        Assert.Null(invoice.FirstApprovedBy);
        Assert.Null(invoice.FirstApprovedAt);
    }
}
