using InvoiceApp.Domain;

namespace InvoiceApp.Tests;

/// <summary>
/// Hands out the stored objects themselves, like InMemoryInvoiceRepository, so a field set before a
/// refusal shows up in tests. Records which invoices were saved, so tests can prove a refusal saved
/// nothing and a decision saved exactly the invoice it was about.
/// </summary>
internal class FakeInvoiceRepository : IInvoiceRepository
{
    private readonly List<Invoice> _invoices = new();
    private readonly List<string> _updatedIds = new();

    public FakeInvoiceRepository(params Invoice[] seed) => _invoices.AddRange(seed);

    public IReadOnlyList<string> UpdatedIds => _updatedIds;
    public int UpdateCount => _updatedIds.Count;

    public IReadOnlyList<Invoice> GetAll() => _invoices.ToList();
    public Invoice? GetById(string id) => _invoices.FirstOrDefault(i => i.Id == id);
    public IReadOnlyList<Invoice> GetByIds(IEnumerable<string> ids) => _invoices.Where(i => ids.Contains(i.Id)).ToList();
    public Invoice Add(Invoice invoice) { _invoices.Add(invoice); return invoice; }
    public bool Remove(string id) => _invoices.RemoveAll(i => i.Id == id) > 0;

    public void Update(Invoice invoice)
    {
        _updatedIds.Add(invoice.Id);
        var index = _invoices.FindIndex(i => i.Id == invoice.Id);
        if (index >= 0) _invoices[index] = invoice;
    }
}
