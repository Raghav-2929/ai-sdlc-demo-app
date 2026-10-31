using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace InvoiceApp.Tests;

/// <summary>
/// The HTTP contract of the approvals endpoints, with the real app running in memory: identity and role come
/// from the session, refusals are 403 with the reason, and the invoice is unchanged when read back.
/// Demo accounts are the ones in appsettings.json (not real credentials).
/// </summary>
public class ApprovalsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string DemoPassword = "ChangeMe123!";
    private readonly WebApplicationFactory<Program> _factory;

    public ApprovalsControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> SignedInAs(string username)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = DemoPassword });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Creates and submits an invoice with a unique customer, so tests sharing the app do not collide.</summary>
    private static async Task<string> PendingInvoice(HttpClient client, decimal amount)
    {
        var created = await client.PostAsJsonAsync("/api/invoices", new { customerName = $"API Test {Guid.NewGuid():N}", amount });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        (await client.PostAsync($"/api/invoices/{id}/submit", null)).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<JsonElement> Read(HttpClient client, string invoiceId) =>
        await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{invoiceId}");

    private static async Task<string?> ErrorOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();

    [Fact]
    [Trait("Rule", "AL-6")]
    [Trait("Rule", "AL-5")]
    public async Task Approve_AsAdmin_Returns403WithTheReasonAndTheInvoiceStaysPending()
    {
        var admin = await SignedInAs("demo.admin");
        var invoiceId = await PendingInvoice(admin, 100m);

        var response = await admin.PostAsync($"/api/approvals/{invoiceId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("demo.admin has role 'Admin', which cannot approve invoices. A Manager or Director must approve it.", await ErrorOf(response));
        var invoice = await Read(admin, invoiceId);
        Assert.Equal("PendingApproval", invoice.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, invoice.GetProperty("decidedBy").ValueKind);
    }

    [Fact]
    [Trait("Rule", "AL-1")]
    [Trait("Rule", "AL-5")]
    public async Task Approve_ManagerAboveTheLimit_Returns403WithTheReasonAndTheInvoiceStaysPending()
    {
        var manager = await SignedInAs("demo.manager");
        var invoiceId = await PendingInvoice(manager, 75_000m);

        var response = await manager.PostAsync($"/api/approvals/{invoiceId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal($"Invoice {invoiceId} is 75,000.00, which is above the Manager limit of 50,000.00. A Director must approve it.", await ErrorOf(response));
        Assert.Equal("PendingApproval", (await Read(manager, invoiceId)).GetProperty("status").GetString());
    }

    // FR-007: a body claiming to be someone else changes nothing; the session decides who is approving.
    [Fact]
    public async Task Approve_IdentityComesFromTheSessionNotFromTheRequestBody()
    {
        var manager = await SignedInAs("demo.manager");
        var invoiceId = await PendingInvoice(manager, 75_000m);

        var spoofed = await manager.PostAsJsonAsync($"/api/approvals/{invoiceId}/approve", new { username = "demo.director", role = "Director" });
        var director = await SignedInAs("demo.director");
        var approved = await director.PostAsync($"/api/approvals/{invoiceId}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, spoofed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var invoice = await Read(director, invoiceId);
        Assert.Equal("Approved", invoice.GetProperty("status").GetString());
        Assert.Equal("demo.director", invoice.GetProperty("decidedBy").GetString());
    }

    [Fact]
    [Trait("Rule", "AL-3")]
    [Trait("Rule", "AL-4")]
    public async Task Approve_AboveFiveHundredThousand_NeedsASecondDifferentDirectorOverHttp()
    {
        var director = await SignedInAs("demo.director");
        var invoiceId = await PendingInvoice(director, 750_000m);

        var first = await director.PostAsync($"/api/approvals/{invoiceId}/approve", null);
        var sameAgain = await director.PostAsync($"/api/approvals/{invoiceId}/approve", null);
        var secondDirector = await SignedInAs("demo.director2");
        var second = await secondDirector.PostAsync($"/api/approvals/{invoiceId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("PendingApproval", (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, sameAgain.StatusCode);
        Assert.EndsWith("a different Director must give the second approval.", await ErrorOf(sameAgain));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var invoice = await Read(secondDirector, invoiceId);
        Assert.Equal("Approved", invoice.GetProperty("status").GetString());
        Assert.Equal("demo.director", invoice.GetProperty("firstApprovedBy").GetString());
        Assert.Equal("demo.director2", invoice.GetProperty("decidedBy").GetString());
    }

    [Fact]
    public async Task Approve_InvoiceThatIsNotPending_Returns409AndUnknownInvoiceReturns404()
    {
        var director = await SignedInAs("demo.director");
        var invoiceId = await PendingInvoice(director, 100m);
        (await director.PostAsync($"/api/approvals/{invoiceId}/approve", null)).EnsureSuccessStatusCode();

        var again = await director.PostAsync($"/api/approvals/{invoiceId}/approve", null);
        var unknown = await director.PostAsync("/api/approvals/INV-404/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal($"Invoice {invoiceId} is Approved; only pending invoices can be decided.", await ErrorOf(again));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // FR-009: Admin keeps the right to reject that it had before the feature.
    [Fact]
    public async Task Reject_AsAdmin_IsStillAllowed()
    {
        var admin = await SignedInAs("demo.admin");
        var invoiceId = await PendingInvoice(admin, 750_000m);

        var response = await admin.PostAsJsonAsync($"/api/approvals/{invoiceId}/reject", new { reason = "Duplicate invoice" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Rejected", (await Read(admin, invoiceId)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Approve_WithoutSigningIn_Returns401()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsync("/api/approvals/INV-1001/approve", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
