namespace InvoiceApp.Domain;

/// <summary>The signed-in person acting on an invoice. Built from the session, never from request data.</summary>
public record Approver(string Username, string Role);
